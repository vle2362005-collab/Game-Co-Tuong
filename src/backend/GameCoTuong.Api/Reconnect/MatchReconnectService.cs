using System.Data;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Reconnect;

public sealed class MatchReconnectService(
    GameDbContext dbContext,
    TimeProvider timeProvider,
    ReconnectTimeoutRegistry timeoutRegistry,
    ILogger<MatchReconnectService> logger)
{
    public async Task<MatchReconnectState> ReconnectAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var player = await dbContext.MatchPlayers
            .Include(candidate => candidate.Match)
            .FirstOrDefaultAsync(candidate => candidate.MatchId == matchId && candidate.UserId == userId,
                cancellationToken);

        if (player is null
            || player.Side is not ("red" or "black")
            || player.Match is not { } match
            || match.Status is not ("playing" or "paused"))
        {
            throw new HubException("Không tìm thấy trận đang diễn ra cho tài khoản này.");
        }

        var membership = await dbContext.RoomMemberships.FirstOrDefaultAsync(candidate =>
            candidate.RoomId == match.RoomId
            && candidate.UserId == userId
            && candidate.LeftAt == null, cancellationToken);
        if (membership is null)
        {
            throw new HubException("Bạn không còn ghế trong phòng của trận đấu này.");
        }

        var now = timeProvider.GetUtcNow();
        var deadline = EarlierDeadline(membership.ReconnectDeadlineAt, GetDeadline(match, player.Side));
        if ((deadline is { } reconnectDeadline && now >= reconnectDeadline)
            || (!membership.IsConnected && deadline is null))
        {
            logger.LogInformation(
                "Reconnect rejected after its deadline for match {MatchId} and user {UserId}.",
                matchId, userId);
            throw new HubException("Thời hạn kết nối lại đã hết.");
        }

        if (!membership.IsConnected)
        {
            var disconnectedAt = GetDisconnectedAt(match, player.Side) ?? now;
            var elapsedMs = Math.Max(0, (now - disconnectedAt).TotalMilliseconds);
            var remainingMs = Math.Max(0, GetRecoveryRemainingMs(match, player.Side) - (int)Math.Ceiling(elapsedMs));
            SetRecoveryRemainingMs(match, player.Side, remainingMs);
            SetDisconnectedAt(match, player.Side, null);
            SetDeadline(match, player.Side, null);
        }

        membership.IsConnected = true;
        membership.ReconnectDeadlineAt = null;

        var players = await dbContext.MatchPlayers
            .Where(candidate => candidate.MatchId == matchId)
            .Select(candidate => candidate.UserId)
            .ToListAsync(cancellationToken);
        var connectedUsers = await dbContext.RoomMemberships
            .Where(candidate => candidate.RoomId == match.RoomId
                && candidate.LeftAt == null
                && candidate.IsConnected)
            .Select(candidate => candidate.UserId)
            .ToListAsync(cancellationToken);

        if (deadline is { } confirmedDeadline && timeProvider.GetUtcNow() >= confirmedDeadline)
        {
            throw new HubException("Thời hạn kết nối lại đã hết.");
        }

        if (match.Status == "paused" && players.All(connectedUsers.Contains))
        {
            match.Status = "playing";
            match.RecoveryReason = null;
            match.TurnStartedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        timeoutRegistry.Cancel(matchId, userId);

        logger.LogInformation(
            "User {UserId} reconnected to match {MatchId} on side {Side} at version {Version}.",
            userId, matchId, player.Side, match.Version);

        return new MatchReconnectState(
            match.Id,
            player.Side,
            match.CurrentBoardJson,
            match.SideToMove,
            match.MoveNumber,
            match.Version,
            match.Status,
            match.RedRemainingMs,
            match.BlackRemainingMs);
    }

    public async Task HandleDisconnectAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.MatchPlayers
            .Include(candidate => candidate.Match)
            .FirstOrDefaultAsync(candidate => candidate.MatchId == matchId && candidate.UserId == userId,
                cancellationToken);
        if (player?.Match is not { Status: "playing" } match)
        {
            return;
        }

        var membership = await dbContext.RoomMemberships.FirstOrDefaultAsync(candidate =>
            candidate.RoomId == match.RoomId
            && candidate.UserId == userId
            && candidate.LeftAt == null, cancellationToken);
        if (membership is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var remainingMs = GetRecoveryRemainingMs(match, player.Side);
        var deadline = now.AddMilliseconds(remainingMs);
        if (match.TurnStartedAt is { } turnStartedAt)
        {
            var elapsedTurnMs = (int)Math.Clamp(
                Math.Ceiling((now - turnStartedAt).TotalMilliseconds), 0, int.MaxValue);
            if (string.Equals(match.SideToMove, "red", StringComparison.OrdinalIgnoreCase))
            {
                match.RedRemainingMs = Math.Max(0, match.RedRemainingMs - elapsedTurnMs);
            }
            else
            {
                match.BlackRemainingMs = Math.Max(0, match.BlackRemainingMs - elapsedTurnMs);
            }
        }

        membership.IsConnected = false;
        membership.ReconnectDeadlineAt = deadline;
        SetDisconnectedAt(match, player.Side, now);
        SetDeadline(match, player.Side, deadline);
        match.Status = "paused";
        match.RecoveryReason = "disconnect";
        match.TurnStartedAt = null;
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        timeoutRegistry.Schedule(matchId, userId, deadline);
    }

    private static DateTimeOffset? EarlierDeadline(DateTimeOffset? first, DateTimeOffset? second) =>
        first is null ? second
        : second is null ? first
        : first < second ? first : second;

    private static DateTimeOffset? GetDeadline(Match match, string side) =>
        IsRed(side) ? match.RedDeadlineAt : match.BlackDeadlineAt;

    private static DateTimeOffset? GetDisconnectedAt(Match match, string side) =>
        IsRed(side) ? match.RedDisconnectedAt : match.BlackDisconnectedAt;

    private static int GetRecoveryRemainingMs(Match match, string side) =>
        IsRed(side) ? match.RedRecoveryRemainingMs : match.BlackRecoveryRemainingMs;

    private static void SetRecoveryRemainingMs(Match match, string side, int remainingMs)
    {
        if (IsRed(side)) match.RedRecoveryRemainingMs = remainingMs;
        else match.BlackRecoveryRemainingMs = remainingMs;
    }

    private static void SetDisconnectedAt(Match match, string side, DateTimeOffset? value)
    {
        if (IsRed(side)) match.RedDisconnectedAt = value;
        else match.BlackDisconnectedAt = value;
    }

    private static void SetDeadline(Match match, string side, DateTimeOffset? value)
    {
        if (IsRed(side)) match.RedDeadlineAt = value;
        else match.BlackDeadlineAt = value;
    }

    private static bool IsRed(string side) =>
        string.Equals(side, "red", StringComparison.OrdinalIgnoreCase);
}
