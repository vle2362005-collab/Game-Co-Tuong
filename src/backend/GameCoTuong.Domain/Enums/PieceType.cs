using System.Text.Json.Serialization;

namespace GameCoTuong.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<PieceType>))]
public enum PieceType
{
    [JsonPropertyName("general")]
    General = 1,

    [JsonPropertyName("advisor")]
    Advisor = 2,

    [JsonPropertyName("elephant")]
    Elephant = 3,

    [JsonPropertyName("horse")]
    Horse = 4,

    [JsonPropertyName("chariot")]
    Chariot = 5,

    [JsonPropertyName("cannon")]
    Cannon = 6,

    [JsonPropertyName("soldier")]
    Soldier = 7
}
