using GameCoTuong.Domain.Entities;

namespace GameCoTuong.Application.Services;

public sealed class MatchReconnectService(ReconnectTimeoutRegistry timeoutRegistry)
{
    private const string Red = "red";
    private const string Black = "black";
    private const string Host = "host";
    private const string Guest = "guest";
    private const int RoomReconnectTimeoutSeconds = 60;

    public bool MarkDisconnected(Room room, RoomMembership membership, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(membership);
        ValidateMembership(room, membership);
        ProcessRoomTimeouts([room], now);

        if (room.Status != "waiting" || membership.LeftAt is not null)
        {
            return false;
        }

        if (!membership.IsConnected)
        {
            return true;
        }

        membership.IsConnected = false;
        foreach (var activeMembership in room.Memberships.Where(candidate => candidate.LeftAt is null))
        {
            activeMembership.IsReady = false;
        }

        membership.ReconnectDeadlineAt = now.AddSeconds(RoomReconnectTimeoutSeconds);
        timeoutRegistry.Schedule(
            ReconnectTimeoutScope.Room,
            room.Id,
            GetRoomParticipant(room, membership),
            membership.ReconnectDeadlineAt.Value);
        return true;
    }

    public bool TryReconnect(Room room, RoomMembership membership, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(membership);
        ValidateMembership(room, membership);
        ProcessRoomTimeouts([room], now);

        if (room.Status != "waiting" || membership.LeftAt is not null)
        {
            return false;
        }

        if (membership.IsConnected)
        {
            return true;
        }

        if (membership.ReconnectDeadlineAt is not { } deadline || now >= deadline)
        {
            return false;
        }

        membership.IsConnected = true;
        membership.ReconnectDeadlineAt = null;
        timeoutRegistry.Cancel(ReconnectTimeoutScope.Room, room.Id, GetRoomParticipant(room, membership));
        return true;
    }

    public void ProcessRoomTimeouts(IEnumerable<Room> rooms, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        var roomList = rooms.ToArray();
        foreach (var room in roomList)
        {
            foreach (var membership in room.Memberships.Where(
                         candidate => candidate.LeftAt is null && candidate.ReconnectDeadlineAt is not null))
            {
                timeoutRegistry.Schedule(
                    ReconnectTimeoutScope.Room,
                    room.Id,
                    GetRoomParticipant(room, membership),
                    membership.ReconnectDeadlineAt!.Value);
            }
        }

        var expiredKeys = timeoutRegistry.GetExpired(now)
            .Where(timeout => timeout.Scope == ReconnectTimeoutScope.Room)
            .Select(timeout => (timeout.Scope, timeout.EntityId, timeout.Participant))
            .ToHashSet();

        foreach (var room in roomList)
        {
            if (room.Status != "waiting")
            {
                timeoutRegistry.Clear(ReconnectTimeoutScope.Room, room.Id);
                continue;
            }

            var expiredMemberships = room.Memberships.Where(membership =>
                membership.LeftAt is null &&
                membership.ReconnectDeadlineAt is { } deadline &&
                now >= deadline &&
                expiredKeys.Contains((ReconnectTimeoutScope.Room, room.Id, GetRoomParticipant(room, membership)))
            ).ToArray();

            if (expiredMemberships.Length == 0)
            {
                continue;
            }

            if (expiredMemberships.Any(membership => membership.UserId == room.HostUserId))
            {
                room.Status = "closed";
                room.ClosedAt = now;
                timeoutRegistry.Clear(ReconnectTimeoutScope.Room, room.Id);
            }
            else
            {
                foreach (var membership in expiredMemberships)
                {
                    membership.IsConnected = false;
                    membership.IsReady = false;
                    membership.LeftAt = now;
                    membership.ReconnectDeadlineAt = null;
                    timeoutRegistry.Cancel(
                        ReconnectTimeoutScope.Room,
                        room.Id,
                        GetRoomParticipant(room, membership));
                }
            }
        }
    }

    public bool MarkDisconnected(Match match, string side, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(match);
        var participant = NormalizeSide(side);
        ProcessMatchTimeouts([match], now);

        if (match.Status is not ("playing" or "paused"))
        {
            return false;
        }

        if (GetDisconnectedAt(match, participant) is not null)
        {
            return true;
        }

        if (match.Status == "playing" && !PauseTurnClock(match, now))
        {
            return false;
        }

        SetDisconnectedAt(match, participant, now);
        var remainingMs = Math.Max(0, GetRecoveryRemainingMs(match, participant));
        var deadline = now.AddMilliseconds(remainingMs);
        SetDeadline(match, participant, deadline);
        match.Status = "paused";
        match.RecoveryReason = "disconnect";
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;
        timeoutRegistry.Schedule(ReconnectTimeoutScope.Match, match.Id, participant, deadline);
        return true;
    }

    public bool TryReconnect(Match match, string side, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(match);
        var participant = NormalizeSide(side);
        ProcessMatchTimeouts([match], now);

        if (match.Status != "paused" || GetDisconnectedAt(match, participant) is null)
        {
            return false;
        }

        var deadline = GetDeadline(match, participant);
        if (deadline is null || now >= deadline.Value)
        {
            return false;
        }

        var remainingMs = (int)Math.Min(
            Math.Max(0, GetRecoveryRemainingMs(match, participant)),
            Math.Floor((deadline.Value - now).TotalMilliseconds));
        SetRecoveryRemainingMs(match, participant, remainingMs);
        SetDisconnectedAt(match, participant, null);
        SetDeadline(match, participant, null);
        timeoutRegistry.Cancel(ReconnectTimeoutScope.Match, match.Id, participant);

        if (match.RedDisconnectedAt is null && match.BlackDisconnectedAt is null)
        {
            match.Status = "playing";
            match.RecoveryReason = null;
            match.TurnStartedAt = now;
        }

        ProcessMatchTimeouts([match], now);
        return match.Status == "playing" || GetDisconnectedAt(match, participant) is null;
    }

    public void ProcessMatchTimeouts(IEnumerable<Match> matches, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(matches);
        var matchList = matches.ToArray();
        foreach (var match in matchList)
        {
            if (match.Status is not ("playing" or "paused"))
            {
                timeoutRegistry.Clear(ReconnectTimeoutScope.Match, match.Id);
                continue;
            }

            SchedulePendingMatchTimeout(match, Red);
            SchedulePendingMatchTimeout(match, Black);
        }

        var expiredMatchIds = timeoutRegistry.GetExpired(now)
            .Where(timeout => timeout.Scope == ReconnectTimeoutScope.Match)
            .Select(timeout => timeout.EntityId)
            .ToHashSet();

        foreach (var match in matchList.Where(candidate => expiredMatchIds.Contains(candidate.Id)))
        {
            ResolveExpiredMatch(match, now);
        }
    }

    private bool PauseTurnClock(Match match, DateTimeOffset now)
    {
        if (match.TurnStartedAt is not { } turnStartedAt)
        {
            return true;
        }

        var elapsedMs = Math.Max(0, Math.Ceiling((now - turnStartedAt).TotalMilliseconds));
        var remainingMs = Math.Max(0, GetTurnRemainingMs(match) - elapsedMs);
        SetTurnRemainingMs(match, (int)remainingMs);
        match.TurnStartedAt = null;

        if (remainingMs > 0)
        {
            return true;
        }

        FinishMatch(match, match.SideToMove == Red ? Black : Red, "timeExpired", now);
        return false;
    }

    private void ResolveExpiredMatch(Match match, DateTimeOffset now)
    {
        if (match.Status is not ("playing" or "paused"))
        {
            timeoutRegistry.Clear(ReconnectTimeoutScope.Match, match.Id);
            return;
        }

        var redExpired = IsExpired(match.RedDisconnectedAt, match.RedDeadlineAt, now);
        var blackExpired = IsExpired(match.BlackDisconnectedAt, match.BlackDeadlineAt, now);
        if (redExpired && blackExpired)
        {
            match.Status = "cancelled";
            match.Winner = null;
            match.ResultReason = "bothReconnectTimeout";
            match.EndedAt = now;
            match.TurnStartedAt = null;
            timeoutRegistry.Clear(ReconnectTimeoutScope.Match, match.Id);
            return;
        }

        if (redExpired && match.BlackDisconnectedAt is null)
        {
            FinishMatch(match, Black, "reconnectTimeout", now);
        }
        else if (blackExpired && match.RedDisconnectedAt is null)
        {
            FinishMatch(match, Red, "reconnectTimeout", now);
        }
    }

    private void FinishMatch(Match match, string winner, string reason, DateTimeOffset now)
    {
        match.Status = "finished";
        match.Winner = winner;
        match.ResultReason = reason;
        match.EndedAt = now;
        match.TurnStartedAt = null;
        timeoutRegistry.Clear(ReconnectTimeoutScope.Match, match.Id);
    }

    private void SchedulePendingMatchTimeout(Match match, string participant)
    {
        var deadline = GetDeadline(match, participant);
        if (GetDisconnectedAt(match, participant) is not null && deadline is not null)
        {
            timeoutRegistry.Schedule(ReconnectTimeoutScope.Match, match.Id, participant, deadline.Value);
        }
    }

    private static bool IsExpired(
        DateTimeOffset? disconnectedAt,
        DateTimeOffset? deadline,
        DateTimeOffset now) =>
        disconnectedAt is not null && deadline is { } value && now >= value;

    private static void ValidateMembership(Room room, RoomMembership membership)
    {
        if (membership.RoomId != room.Id)
        {
            throw new ArgumentException("The membership does not belong to this room.", nameof(membership));
        }
    }

    private static string GetRoomParticipant(Room room, RoomMembership membership) =>
        membership.UserId == room.HostUserId ? Host : Guest;

    private static string NormalizeSide(string side) =>
        side?.ToLowerInvariant() switch
        {
            Red => Red,
            Black => Black,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Side must be 'red' or 'black'.")
        };

    private static DateTimeOffset? GetDisconnectedAt(Match match, string participant) =>
        participant == Red ? match.RedDisconnectedAt : match.BlackDisconnectedAt;

    private static void SetDisconnectedAt(Match match, string participant, DateTimeOffset? value)
    {
        if (participant == Red)
        {
            match.RedDisconnectedAt = value;
        }
        else
        {
            match.BlackDisconnectedAt = value;
        }
    }

    private static DateTimeOffset? GetDeadline(Match match, string participant) =>
        participant == Red ? match.RedDeadlineAt : match.BlackDeadlineAt;

    private static void SetDeadline(Match match, string participant, DateTimeOffset? value)
    {
        if (participant == Red)
        {
            match.RedDeadlineAt = value;
        }
        else
        {
            match.BlackDeadlineAt = value;
        }
    }

    private static int GetRecoveryRemainingMs(Match match, string participant) =>
        participant == Red ? match.RedRecoveryRemainingMs : match.BlackRecoveryRemainingMs;

    private static void SetRecoveryRemainingMs(Match match, string participant, int value)
    {
        if (participant == Red)
        {
            match.RedRecoveryRemainingMs = value;
        }
        else
        {
            match.BlackRecoveryRemainingMs = value;
        }
    }

    private static int GetTurnRemainingMs(Match match) =>
        match.SideToMove == Red ? match.RedRemainingMs : match.BlackRemainingMs;

    private static void SetTurnRemainingMs(Match match, int value)
    {
        if (match.SideToMove == Red)
        {
            match.RedRemainingMs = value;
        }
        else
        {
            match.BlackRemainingMs = value;
        }
    }
}
