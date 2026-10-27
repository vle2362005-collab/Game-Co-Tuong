namespace GameCoTuong.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public string Mode { get; set; } = "pvp";
    public string RuleSetVersion { get; set; } = "gct-core-1";
    public string Status { get; set; } = "playing";
    public long Version { get; set; } = 1;
    public int MoveNumber { get; set; }
    public string SideToMove { get; set; } = "red";
    public string InitialBoardJson { get; set; } = string.Empty;
    public string CurrentBoardJson { get; set; } = string.Empty;
    public int RedRemainingMs { get; set; }
    public int BlackRemainingMs { get; set; }
    public DateTimeOffset? TurnStartedAt { get; set; }
    public string? RecoveryReason { get; set; }
    public int RedRecoveryRemainingMs { get; set; } = 60000;
    public DateTimeOffset? RedDisconnectedAt { get; set; }
    public DateTimeOffset? RedDeadlineAt { get; set; }
    public int BlackRecoveryRemainingMs { get; set; } = 60000;
    public DateTimeOffset? BlackDisconnectedAt { get; set; }
    public DateTimeOffset? BlackDeadlineAt { get; set; }
    public Guid? DrawOfferId { get; set; }
    public string? DrawOfferedBy { get; set; }
    public DateTimeOffset? DrawCreatedAt { get; set; }
    public DateTimeOffset? DrawExpiresAt { get; set; }
    public string? Winner { get; set; }
    public string? ResultReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
    public ICollection<Move> Moves { get; set; } = new List<Move>();
}
