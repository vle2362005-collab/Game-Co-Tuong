namespace GameCoTuong.Domain.Entities;

public class Room
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public User? HostUser { get; set; }
    public string Status { get; set; } = "waiting";
    public long Version { get; set; } = 1;
    public int InitialMinutes { get; set; } = 15;
    public int IncrementSeconds { get; set; } = 10;
    public Guid? CurrentMatchId { get; set; }
    public Match? CurrentMatch { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<RoomMembership> Memberships { get; set; } = new List<RoomMembership>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
