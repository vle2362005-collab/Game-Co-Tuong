using System.Text.Json.Serialization;

namespace GameCoTuong.Application.Auth.Models;

public sealed record LoginRequest(
    [property: JsonPropertyName("requestId")] Guid RequestId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password
);
