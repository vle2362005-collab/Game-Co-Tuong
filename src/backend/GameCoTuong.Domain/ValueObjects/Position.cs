using System.Text.Json.Serialization;

namespace GameCoTuong.Domain.ValueObjects;

public readonly record struct Position
{
    public const int MinCol = 0;
    public const int MaxCol = 8;
    public const int MinRow = 0;
    public const int MaxRow = 9;

    [JsonPropertyName("col")]
    public int Col { get; init; }

    [JsonPropertyName("row")]
    public int Row { get; init; }

    [JsonConstructor]
    public Position(int col, int row)
    {
        if (!IsValid(col, row))
        {
            throw new ArgumentOutOfRangeException(
                $"Tọa độ ({col}, {row}) không hợp lệ. Cột phải từ {MinCol} đến {MaxCol}, hàng từ {MinRow} đến {MaxRow}.");
        }

        Col = col;
        Row = row;
    }

    public static bool IsValid(int col, int row) =>
        col is >= MinCol and <= MaxCol && row is >= MinRow and <= MaxRow;

    public static bool TryCreate(int col, int row, out Position position)
    {
        if (IsValid(col, row))
        {
            position = new Position(col, row);
            return true;
        }

        position = default;
        return false;
    }

    public override string ToString() => $"({Col}, {Row})";
}
