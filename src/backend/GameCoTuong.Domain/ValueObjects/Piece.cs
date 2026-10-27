using System.Text.Json.Serialization;
using GameCoTuong.Domain.Enums;

namespace GameCoTuong.Domain.ValueObjects;

public sealed record Piece
{
    public static bool IsValidPieceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var firstDash = id.IndexOf('-');
        var lastDash = id.LastIndexOf('-');
        if (firstDash <= 0 || lastDash <= firstDash || lastDash == id.Length - 1) return false;

        var prefix = id.Substring(0, firstDash);
        if (prefix is not ("red" or "black")) return false;

        var numChar = id[lastDash + 1];
        if (id.Length != lastDash + 2 || numChar is < '1' or > '5') return false;

        var name = id.Substring(firstDash + 1, lastDash - firstDash - 1);
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var c in name)
        {
            if (c is < 'a' or > 'z') return false;
        }

        return true;
    }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("type")]
    public PieceType Type { get; init; }

    [JsonPropertyName("side")]
    public Side Side { get; init; }

    [JsonPropertyName("position")]
    public Position Position { get; init; }

    [JsonConstructor]
    public Piece(string id, PieceType type, Side side, Position position)
    {
        if (string.IsNullOrWhiteSpace(id) || !IsValidPieceId(id))
        {
            throw new ArgumentException($"Mã quân cờ '{id}' không đúng định dạng ^(red|black)-[a-z]+-[1-5]$.", nameof(id));
        }

        var expectedPrefix = side == Side.Red ? "red-" : "black-";
        if (!id.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Mã quân cờ '{id}' không khớp với màu {side}.", nameof(id));
        }

        Id = id;
        Type = type;
        Side = side;
        Position = position;
    }

    public Piece MoveTo(Position newPosition) => new(Id, Type, Side, newPosition);

    public override string ToString() => $"{Side} {Type} [{Id}] at {Position}";
}
