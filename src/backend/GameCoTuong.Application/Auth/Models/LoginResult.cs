namespace GameCoTuong.Application.Auth.Models;

public sealed class LoginResult
{
    public bool IsSuccess { get; }
    public AuthSessionDto? Session { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Dictionary<string, string[]>? FieldErrors { get; }

    private LoginResult(
        bool isSuccess,
        AuthSessionDto? session,
        string? errorCode,
        string? errorMessage,
        Dictionary<string, string[]>? fieldErrors)
    {
        IsSuccess = isSuccess;
        Session = session;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        FieldErrors = fieldErrors;
    }

    public static LoginResult Success(AuthSessionDto session) =>
        new(true, session, null, null, null);

    public static LoginResult Failed(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage, null);

    public static LoginResult ValidationFailed(Dictionary<string, string[]> fieldErrors, string errorMessage = "Dữ liệu yêu cầu không hợp lệ.") =>
        new(false, null, "VALIDATION_ERROR", errorMessage, fieldErrors);
}
