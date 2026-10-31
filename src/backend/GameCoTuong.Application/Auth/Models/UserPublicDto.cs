using System.Text.Json.Serialization;

namespace GameCoTuong.Application.Auth.Models;

public sealed record UserPublicDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("displayName")] string DisplayName
);
