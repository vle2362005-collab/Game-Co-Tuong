namespace GameCoTuong.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public long Sequence { get; set; }
    public Guid SenderUserId { get; set; }
    public User? SenderUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
