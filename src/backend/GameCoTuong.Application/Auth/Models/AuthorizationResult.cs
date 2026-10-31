namespace GameCoTuong.Application.Auth.Models;

public record AuthorizationResult(
    bool IsAuthorized,
    string? ErrorCode = null,
    string? ErrorMessage = null
)
{
    public static AuthorizationResult Succeeded() => new(true);
    public static AuthorizationResult Failed(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
}
