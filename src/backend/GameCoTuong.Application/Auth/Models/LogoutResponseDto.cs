namespace GameCoTuong.Application.Auth.Models;

public record LogoutResponseDto(
    bool Acknowledged,
    bool Revoked,
    string PolicyMessage
);
