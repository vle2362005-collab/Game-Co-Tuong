using GameCoTuong.Domain.ValueObjects;

namespace GameCoTuong.Api.Realtime;

public sealed record MatchReconnectState(
    Guid MatchId,
    string Status,
    string YourSide,
    long Version,
    int MoveNumber,
    string SideToMove,
    IReadOnlyList<Piece> Board,
    bool IsPaused,
    bool RedDisconnected,
    DateTimeOffset? RedDisconnectedAt,
    DateTimeOffset? RedReconnectDeadline,
    int RedRecoveryRemainingMs,
    bool BlackDisconnected,
    DateTimeOffset? BlackDisconnectedAt,
    DateTimeOffset? BlackReconnectDeadline,
    int BlackRecoveryRemainingMs,
    string? Winner,
    string? ResultReason);

public sealed record MatchReconnectResult(
    bool Accepted,
    bool WasReconnected,
    MatchReconnectState State);
