namespace GameCoTuong.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "game-co-tuong-local";
    public string Audience { get; set; } = "game-co-tuong-web-local";
    public string SigningKey { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 1440; // 24 hours
}
