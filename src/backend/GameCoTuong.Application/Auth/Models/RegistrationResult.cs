namespace GameCoTuong.Application.Auth.Models;

public sealed class RegistrationResult
{
    public bool IsSuccess { get; init; }
    public UserPublicDto? User { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string[]>? FieldErrors { get; init; }

    public static RegistrationResult Success(UserPublicDto user) =>
        new() { IsSuccess = true, User = user };

    public static RegistrationResult ValidationFailed(Dictionary<string, string[]> fieldErrors, string message = "Dữ liệu đăng ký không hợp lệ.") =>
        new()
        {
            IsSuccess = false,
            ErrorCode = "VALIDATION_ERROR",
            ErrorMessage = message,
            FieldErrors = fieldErrors
        };

    public static RegistrationResult UsernameTaken(string message = "Tên đăng nhập đã được sử dụng.") =>
        new()
        {
            IsSuccess = false,
            ErrorCode = "USERNAME_TAKEN",
            ErrorMessage = message,
            FieldErrors = new Dictionary<string, string[]>()
        };

    public static RegistrationResult EmailTaken(string message = "Email đã được sử dụng.") =>
        new()
        {
            IsSuccess = false,
            ErrorCode = "EMAIL_TAKEN",
            ErrorMessage = message,
            FieldErrors = new Dictionary<string, string[]>()
        };
}
