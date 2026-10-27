namespace GameCoTuong.Api.Reconnect;

public sealed record MatchReconnectState(
    Guid MatchId,
    string Side,
    string CurrentBoardJson,
    string SideToMove,
    int MoveNumber,
    long Version,
    string Status,
    int RedRemainingMs,
    int BlackRemainingMs);
