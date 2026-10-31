using System.Text.Json.Serialization;

namespace GameCoTuong.Application.Auth.Models;

public sealed record RegisterRequest(
    [property: JsonPropertyName("requestId")] Guid RequestId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("email")] string Email
);
