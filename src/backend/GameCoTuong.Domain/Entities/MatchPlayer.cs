namespace GameCoTuong.Domain.Entities;

public class MatchPlayer
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match? Match { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Side { get; set; } = "red";
}
