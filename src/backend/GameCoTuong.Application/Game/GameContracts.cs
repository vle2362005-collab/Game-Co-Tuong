namespace GameCoTuong.Application.Game;

public sealed record CreateRoomInput(Guid RequestId, string Name = "Phòng cờ");
public sealed record CommandInput(Guid RequestId);
public sealed record ReadyInput(bool Ready);
public sealed record MoveInput(Guid RequestId, long ExpectedVersion, string PieceId, int ToCol, int ToRow);
public sealed record ChatInput(Guid RequestId, string Content);
public sealed record SignalInput(Guid CallId, Guid RecipientId, string Type, string Data);
public sealed record MemberView(Guid UserId, string DisplayName, string Side, bool Ready, bool Connected);
public sealed record RoomView(Guid Id, string Code, string Name, Guid HostUserId, string Status, long Version,
    Guid? MatchId, IReadOnlyList<MemberView> Members);
public sealed record ChatView(Guid Id, Guid RoomId, Guid SenderId, string SenderName, string Content, DateTimeOffset SentAt, long Sequence);
public sealed class GameFault(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
