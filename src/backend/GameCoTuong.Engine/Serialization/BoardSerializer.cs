using System.Text.Json;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;

namespace GameCoTuong.Engine.Serialization;

public static class BoardSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(IEnumerable<Piece> pieces) =>
        JsonSerializer.Serialize(pieces, JsonOptions);

    public static string Serialize(Board board) =>
        Serialize((IEnumerable<Piece>)board);

    public static Board Deserialize(string json)
    {
        var pieces = JsonSerializer.Deserialize<List<Piece>>(json, JsonOptions);
        if (pieces is null)
        {
            throw new JsonException("Không thể giải mã dữ liệu bàn cờ từ JSON null.");
        }

        return new Board(pieces);
    }
}
