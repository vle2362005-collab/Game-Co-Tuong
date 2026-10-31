namespace GameCoTuong.Application.Auth.Models;

public record StartRoomRequest(
    Guid RequestId,
    Guid? SpoofedUserId = null
);

public record CloseRoomRequest(
    Guid RequestId,
    Guid? SpoofedUserId = null
);

public record SubmitMoveRequest(
    Guid RequestId,
    string MoveNotation,
    Guid? SpoofedUserId = null
);

public record VideoSignalRequest(
    Guid RequestId,
    string SignalType,
    string SignalData,
    Guid? SpoofedUserId = null
);
