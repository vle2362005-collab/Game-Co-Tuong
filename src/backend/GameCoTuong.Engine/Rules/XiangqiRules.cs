using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;

namespace GameCoTuong.Engine.Rules;

public sealed record LegalMove(string PieceId, Position To);
public sealed record PositionResult(bool Finished, Side? Winner, string? Reason);

/// <summary>Red starts at rows 5..9 and advances toward row 0. Generals are never captured.</summary>
public static class XiangqiRules
{
    public static Side Opponent(Side side) => side == Side.Red ? Side.Black : Side.Red;

    public static bool IsPseudoLegal(Board board, Piece piece, Position to)
    {
        var from = piece.Position;
        var target = board.GetPieceAt(to);
        if (from == to || target?.Side == piece.Side) return false;
        int dx = to.Col - from.Col, dy = to.Row - from.Row;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        return piece.Type switch
        {
            PieceType.Soldier => (dx == 0 && dy == (piece.Side == Side.Red ? -1 : 1)) ||
                (BoardGeometry.HasCrossedRiver(piece.Side, from) && ay == 0 && ax == 1),
            PieceType.Chariot => (dx == 0 || dy == 0) && Screens(board, from, to) == 0,
            PieceType.Cannon => (dx == 0 || dy == 0) && Screens(board, from, to) == (target is null ? 0 : 1),
            PieceType.Horse => (ax == 2 && ay == 1 && board.GetPieceAt(from.Col + Math.Sign(dx), from.Row) is null) ||
                (ax == 1 && ay == 2 && board.GetPieceAt(from.Col, from.Row + Math.Sign(dy)) is null),
            PieceType.Elephant => ax == 2 && ay == 2 &&
                (piece.Side == Side.Red ? to.Row >= 5 : to.Row <= 4) &&
                board.GetPieceAt(from.Col + dx / 2, from.Row + dy / 2) is null,
            PieceType.Advisor => ax == 1 && ay == 1 && BoardGeometry.IsInPalace(piece.Side, to),
            PieceType.General => ax + ay == 1 && BoardGeometry.IsInPalace(piece.Side, to),
            _ => false
        };
    }

    private static int Screens(Board board, Position from, Position to)
    {
        int count = 0, dx = Math.Sign(to.Col - from.Col), dy = Math.Sign(to.Row - from.Row);
        for (int x = from.Col + dx, y = from.Row + dy; x != to.Col || y != to.Row; x += dx, y += dy)
            if (board.GetPieceAt(x, y) is not null) count++;
        return count;
    }

    public static bool GeneralsFace(Board board)
    {
        var red = board.GetGeneral(Side.Red).Position;
        var black = board.GetGeneral(Side.Black).Position;
        return red.Col == black.Col && Screens(board, red, black) == 0;
    }

    public static bool IsInCheck(Board board, Side side) => GeneralsFace(board) ||
        board.Any(p => p.Side != side && IsPseudoLegal(board, p, board.GetGeneral(side).Position));

    public static bool IsLegal(Board board, Side side, LegalMove move)
    {
        var piece = board.GetPieceById(move.PieceId);
        if (piece is null || piece.Side != side || board.GetPieceAt(move.To)?.Type == PieceType.General ||
            !IsPseudoLegal(board, piece, move.To)) return false;
        return !IsInCheck(board.ApplyMove(piece.Id, move.To, out _), side);
    }

    public static IReadOnlyList<LegalMove> Generate(Board board, Side side)
    {
        var moves = new List<LegalMove>();
        foreach (var piece in board.Where(p => p.Side == side))
            for (int row = 0; row < 10; row++)
                for (int col = 0; col < 9; col++)
                {
                    var move = new LegalMove(piece.Id, new Position(col, row));
                    if (IsLegal(board, side, move)) moves.Add(move);
                }
        return moves;
    }

    public static PositionResult Result(Board board, Side sideToMove) => Generate(board, sideToMove).Count != 0
        ? new(false, null, null)
        : new(true, Opponent(sideToMove), IsInCheck(board, sideToMove) ? "checkmate" : "stalemate");
}
