using System.Collections;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;

namespace GameCoTuong.Engine.Model;

public sealed class Board : IReadOnlyCollection<Piece>
{
    private readonly Dictionary<string, Piece> piecesById = new(StringComparer.Ordinal);
    private readonly Piece?[,] grid = new Piece?[BoardGeometry.TotalColumns, BoardGeometry.TotalRows];

    public int Count => piecesById.Count;

    public Board(IEnumerable<Piece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        foreach (var piece in pieces)
        {
            if (piecesById.ContainsKey(piece.Id))
            {
                throw new ArgumentException($"Quân cờ có ID '{piece.Id}' bị trùng lặp trên bàn cờ.", nameof(pieces));
            }

            if (grid[piece.Position.Col, piece.Position.Row] is not null)
            {
                throw new ArgumentException(
                    $"Tọa độ {piece.Position} bị trùng bởi hai quân cờ: '{grid[piece.Position.Col, piece.Position.Row]!.Id}' và '{piece.Id}'.",
                    nameof(pieces));
            }

            piecesById[piece.Id] = piece;
            grid[piece.Position.Col, piece.Position.Row] = piece;
        }

        ValidateBoardIntegrity();
    }

    public Piece? GetPieceAt(Position pos) => grid[pos.Col, pos.Row];

    public Piece? GetPieceAt(int col, int row) =>
        Position.IsValid(col, row) ? grid[col, row] : null;

    public Piece? GetPieceById(string pieceId) =>
        piecesById.GetValueOrDefault(pieceId);

    public Piece GetGeneral(Side side) =>
        piecesById.Values.FirstOrDefault(p => p.Side == side && p.Type == PieceType.General)
        ?? throw new InvalidOperationException($"Không tìm thấy Tướng của phe {side} trên bàn cờ.");

    public bool IsOccupied(Position pos) => grid[pos.Col, pos.Row] is not null;

    public Board Clone() => new(piecesById.Values);

    public Board ApplyMove(string pieceId, Position to, out Piece? capturedPiece)
    {
        if (!piecesById.TryGetValue(pieceId, out var movingPiece))
        {
            throw new ArgumentException($"Không tìm thấy quân cờ '{pieceId}' trên bàn cờ.", nameof(pieceId));
        }

        capturedPiece = grid[to.Col, to.Row];
        if (capturedPiece?.Side == movingPiece.Side)
        {
            throw new InvalidOperationException($"Không thể đi vào ô {to} vì đã có quân cùng màu '{capturedPiece.Id}'.");
        }

        var newPieces = new List<Piece>(piecesById.Count);
        foreach (var p in piecesById.Values)
        {
            if (p.Id == pieceId)
            {
                newPieces.Add(p.MoveTo(to));
            }
            else if (capturedPiece is not null && p.Id == capturedPiece.Id)
            {
                // Quân bị ăn thì loại khỏi bàn cờ
                continue;
            }
            else
            {
                newPieces.Add(p);
            }
        }

        return new Board(newPieces);
    }

    private void ValidateBoardIntegrity()
    {
        if (piecesById.Count < 2)
        {
            throw new InvalidOperationException($"Bàn cờ phải có tối thiểu 2 quân cờ (hiện có {piecesById.Count}).");
        }

        if (piecesById.Count > 32)
        {
            throw new InvalidOperationException($"Bàn cờ không được vượt quá 32 quân cờ (hiện có {piecesById.Count}).");
        }

        var redGeneral = piecesById.Values.Count(p => p.Side == Side.Red && p.Type == PieceType.General);
        var blackGeneral = piecesById.Values.Count(p => p.Side == Side.Black && p.Type == PieceType.General);

        if (redGeneral != 1 || blackGeneral != 1)
        {
            throw new InvalidOperationException(
                $"Bàn cờ phải có đúng 1 Tướng Đỏ và 1 Tướng Đen (hiện có {redGeneral} Tướng Đỏ, {blackGeneral} Tướng Đen).");
        }
    }

    public IEnumerator<Piece> GetEnumerator() => piecesById.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
