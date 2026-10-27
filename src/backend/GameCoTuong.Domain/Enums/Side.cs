using System.Text.Json.Serialization;

namespace GameCoTuong.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<Side>))]
public enum Side
{
    [JsonPropertyName("red")]
    Red = 1,

    [JsonPropertyName("black")]
    Black = 2
}
