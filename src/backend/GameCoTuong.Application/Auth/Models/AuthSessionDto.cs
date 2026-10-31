using System.Text.Json.Serialization;

namespace GameCoTuong.Application.Auth.Models;

public sealed record AuthSessionDto(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("user")] UserPublicDto User
);
