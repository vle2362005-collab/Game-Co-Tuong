namespace GameCoTuong.Domain.Entities;

public class Move
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match? Match { get; set; }
    public int Ply { get; set; }
    public Guid RequestId { get; set; }
    public long MatchVersion { get; set; }
    public string Side { get; set; } = "red";
    public string PieceId { get; set; } = string.Empty;
    public int FromCol { get; set; }
    public int FromRow { get; set; }
    public int ToCol { get; set; }
    public int ToRow { get; set; }
    public string? CapturedPieceId { get; set; }
    public string? BoardSnapshotJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
