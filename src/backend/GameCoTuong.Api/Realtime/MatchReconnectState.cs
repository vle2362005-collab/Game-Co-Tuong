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
    int RedRecoveryRemainingMs,
    bool BlackDisconnected,
    int BlackRecoveryRemainingMs,
    string? Winner,
    string? ResultReason);

public sealed record MatchReconnectResult(
    bool Accepted,
    bool WasReconnected,
    MatchReconnectState State);
