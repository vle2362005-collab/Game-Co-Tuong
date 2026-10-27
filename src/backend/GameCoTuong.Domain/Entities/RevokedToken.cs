namespace GameCoTuong.Domain.Entities;

public class RevokedToken
{
    public string Jti { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
}
