using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;

namespace GameCoTuong.Engine.Model;

public sealed class GameState
{
    public Board Board { get; private set; }
    public Side SideToMove { get; private set; }
    public int MoveNumber { get; private set; }
    public long Version { get; private set; }

    public GameState(Board board, Side sideToMove = Side.Red, int moveNumber = 0, long version = 1)
    {
        Board = board ?? throw new ArgumentNullException(nameof(board));
        SideToMove = sideToMove;
        MoveNumber = moveNumber;
        Version = version;
    }

    public static GameState CreateInitial() =>
        new(BoardInitializer.CreateInitialBoard(), Side.Red, moveNumber: 0, version: 1);

    public GameState ApplyMove(string pieceId, Position to, out Piece? capturedPiece)
    {
        var piece = Board.GetPieceById(pieceId)
            ?? throw new ArgumentException($"Không tìm thấy quân cờ '{pieceId}' trên bàn cờ.", nameof(pieceId));

        if (piece.Side != SideToMove)
        {
            throw new InvalidOperationException($"Hiện tại là lượt của bên {SideToMove}, quân '{pieceId}' thuộc bên {piece.Side}.");
        }

        var newBoard = Board.ApplyMove(pieceId, to, out capturedPiece);
        var nextSide = SideToMove == Side.Red ? Side.Black : Side.Red;

        return new GameState(newBoard, nextSide, MoveNumber + 1, Version + 1);
    }
}
