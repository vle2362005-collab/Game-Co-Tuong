using System.Data;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Realtime;

public sealed class MatchReconnectService(GameDbContext db, TimeProvider timeProvider)
{
    public const int RecoveryWindowMs = 60_000;

    public async Task<MatchReconnectResult> ReconnectAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var match = await LoadMatchAsync(matchId, cancellationToken);
        var player = match?.Players.SingleOrDefault(candidate => candidate.UserId == userId);
        if (match is null || player is null)
        {
            throw new MatchAccessDeniedException();
        }

        var now = timeProvider.GetUtcNow();
        if (match.Status != "playing")
        {
            return new MatchReconnectResult(false, false, CreateState(match, player.Side, now));
        }

        var ownDeadline = GetDeadline(match, player.Side);
        var opponentSide = Opposite(player.Side);
        var opponentDeadline = GetDeadline(match, opponentSide);
        var ownExpired = ownDeadline <= now;
        var opponentExpired = opponentDeadline <= now;
        var wasReconnected = ownDeadline is not null;

        if (ownExpired)
        {
            var decision = MatchReconnectExpiryPolicy.Decide(
                redExpired: player.Side == "red" ? ownExpired : opponentExpired,
                blackExpired: player.Side == "black" ? ownExpired : opponentExpired,
                redDisconnected: match.RedDeadlineAt is not null,
                blackDisconnected: match.BlackDeadlineAt is not null);
            if (decision is not null)
            {
                EndMatch(match, now, decision.Status, decision.Winner, decision.Reason);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new MatchReconnectResult(false, wasReconnected, CreateState(match, player.Side, now));
        }

        if (opponentExpired)
        {
            EndMatch(match, now, status: "ended", winner: player.Side, reason: "disconnect_timeout");
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MatchReconnectResult(false, wasReconnected, CreateState(match, player.Side, now));
        }

        if (ownDeadline is not null)
        {
            SetRemaining(match, player.Side, RemainingMilliseconds(ownDeadline.Value, now));
            SetDeadline(match, player.Side, null);
            SetDisconnectedAt(match, player.Side, null);
        }

        if (GetDeadline(match, opponentSide) is null)
        {
            match.RecoveryReason = null;
            match.TurnStartedAt ??= now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MatchReconnectResult(true, wasReconnected, CreateState(match, player.Side, now));
    }

    public async Task<MatchReconnectState?> MarkDisconnectedAsync(
        Guid matchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var match = await LoadMatchAsync(matchId, cancellationToken);
        var player = match?.Players.SingleOrDefault(candidate => candidate.UserId == userId);
        if (match is null || player is null || match.Status != "playing")
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (GetDeadline(match, player.Side) is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return CreateState(match, player.Side, timeProvider.GetUtcNow());
        }

        var now = timeProvider.GetUtcNow();
        if (GetDeadline(match, Opposite(player.Side)) is null && match.TurnStartedAt is { } turnStartedAt)
        {
            var elapsed = Math.Max(0, (int)(now - turnStartedAt).TotalMilliseconds);
            if (match.SideToMove == "red")
            {
                match.RedRemainingMs = Math.Max(0, match.RedRemainingMs - elapsed);
            }
            else
            {
                match.BlackRemainingMs = Math.Max(0, match.BlackRemainingMs - elapsed);
            }

            match.TurnStartedAt = null;
        }

        var remaining = Math.Clamp(GetRemaining(match, player.Side), 0, RecoveryWindowMs);
        SetRemaining(match, player.Side, remaining);
        SetDeadline(match, player.Side, ReconnectWindow.StartDeadline(now, remaining));
        SetDisconnectedAt(match, player.Side, now);
        match.RecoveryReason = $"disconnected:{player.Side}";

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateState(match, player.Side, now);
    }

    public async Task<MatchReconnectState?> ProcessExpiredAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var match = await LoadMatchAsync(matchId, cancellationToken);
        if (match is null || match.Status != "playing")
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var redExpired = match.RedDeadlineAt is { } redDeadline && redDeadline <= now;
        var blackExpired = match.BlackDeadlineAt is { } blackDeadline && blackDeadline <= now;
        var decision = MatchReconnectExpiryPolicy.Decide(
            redExpired,
            blackExpired,
            redDisconnected: match.RedDeadlineAt is not null,
            blackDisconnected: match.BlackDeadlineAt is not null);
        if (decision is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        EndMatch(match, now, decision.Status, decision.Winner, decision.Reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateState(match, "red", now);
    }

    public async Task<IReadOnlyList<Guid>> FindExpiredMatchIdsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await db.Matches
            .AsNoTracking()
            .Where(match => match.Status == "playing"
                && ((match.RedDeadlineAt != null && match.RedDeadlineAt <= now)
                    || (match.BlackDeadlineAt != null && match.BlackDeadlineAt <= now)))
            .OrderBy(match => match.Id)
            .Select(match => match.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private Task<Match?> LoadMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        db.Matches
            .Include(match => match.Players)
            .SingleOrDefaultAsync(match => match.Id == matchId, cancellationToken);

    private static MatchReconnectState CreateState(Match match, string yourSide, DateTimeOffset now)
    {
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson).ToArray();
        var redRemaining = match.RedDeadlineAt is { } redDeadline
            ? RemainingMilliseconds(redDeadline, now)
            : Math.Clamp(match.RedRecoveryRemainingMs, 0, RecoveryWindowMs);
        var blackRemaining = match.BlackDeadlineAt is { } blackDeadline
            ? RemainingMilliseconds(blackDeadline, now)
            : Math.Clamp(match.BlackRecoveryRemainingMs, 0, RecoveryWindowMs);

        return new MatchReconnectState(
            match.Id,
            match.Status,
            yourSide,
            match.Version,
            match.MoveNumber,
            match.SideToMove,
            board,
            match.RedDeadlineAt is not null || match.BlackDeadlineAt is not null,
            match.RedDeadlineAt is not null,
            match.RedDisconnectedAt,
            match.RedDeadlineAt,
            redRemaining,
            match.BlackDeadlineAt is not null,
            match.BlackDisconnectedAt,
            match.BlackDeadlineAt,
            blackRemaining,
            match.Winner,
            match.ResultReason);
    }

    private static void EndMatch(Match match, DateTimeOffset now, string status, string? winner, string reason)
    {
        match.Status = status;
        match.Winner = winner;
        match.ResultReason = reason;
        match.EndedAt = now;
        match.TurnStartedAt = null;
    }

    private static string Opposite(string side) => side == "red" ? "black" : "red";

    private static DateTimeOffset? GetDeadline(Match match, string side) =>
        side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt;

    private static int GetRemaining(Match match, string side) =>
        side == "red" ? match.RedRecoveryRemainingMs : match.BlackRecoveryRemainingMs;

    private static void SetRemaining(Match match, string side, int value)
    {
        if (side == "red")
        {
            match.RedRecoveryRemainingMs = value;
        }
        else
        {
            match.BlackRecoveryRemainingMs = value;
        }
    }

    private static void SetDeadline(Match match, string side, DateTimeOffset? value)
    {
        if (side == "red")
        {
            match.RedDeadlineAt = value;
        }
        else
        {
            match.BlackDeadlineAt = value;
        }
    }

    private static void SetDisconnectedAt(Match match, string side, DateTimeOffset? value)
    {
        if (side == "red")
        {
            match.RedDisconnectedAt = value;
        }
        else
        {
            match.BlackDisconnectedAt = value;
        }
    }

    private static int RemainingMilliseconds(DateTimeOffset deadline, DateTimeOffset now) =>
        ReconnectWindow.GetRemainingMilliseconds(deadline, now);
}

public sealed class MatchAccessDeniedException : Exception
{
}
