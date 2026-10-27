namespace GameCoTuong.Domain.Entities;

public class RoomMembership
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Side { get; set; } = "red";
    public bool IsReady { get; set; }
    public bool IsConnected { get; set; } = true;
    public DateTimeOffset? ReconnectDeadlineAt { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}
