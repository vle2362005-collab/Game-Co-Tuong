using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;

namespace GameCoTuong.Engine.Model;

public static class BoardInitializer
{
    public static Board CreateInitialBoard() => new(CreateInitialPieces());

    public static IReadOnlyList<Piece> CreateInitialPieces()
    {
        var pieces = new List<Piece>(32)
        {
            // === ĐEN (Black) - Hàng 0, 2, 3 ===
            new("black-chariot-1", PieceType.Chariot, Side.Black, new Position(0, 0)),
            new("black-horse-1", PieceType.Horse, Side.Black, new Position(1, 0)),
            new("black-elephant-1", PieceType.Elephant, Side.Black, new Position(2, 0)),
            new("black-advisor-1", PieceType.Advisor, Side.Black, new Position(3, 0)),
            new("black-general-1", PieceType.General, Side.Black, new Position(4, 0)),
            new("black-advisor-2", PieceType.Advisor, Side.Black, new Position(5, 0)),
            new("black-elephant-2", PieceType.Elephant, Side.Black, new Position(6, 0)),
            new("black-horse-2", PieceType.Horse, Side.Black, new Position(7, 0)),
            new("black-chariot-2", PieceType.Chariot, Side.Black, new Position(8, 0)),

            new("black-cannon-1", PieceType.Cannon, Side.Black, new Position(1, 2)),
            new("black-cannon-2", PieceType.Cannon, Side.Black, new Position(7, 2)),

            new("black-soldier-1", PieceType.Soldier, Side.Black, new Position(0, 3)),
            new("black-soldier-2", PieceType.Soldier, Side.Black, new Position(2, 3)),
            new("black-soldier-3", PieceType.Soldier, Side.Black, new Position(4, 3)),
            new("black-soldier-4", PieceType.Soldier, Side.Black, new Position(6, 3)),
            new("black-soldier-5", PieceType.Soldier, Side.Black, new Position(8, 3)),

            // === ĐỎ (Red) - Hàng 6, 7, 9 ===
            new("red-soldier-1", PieceType.Soldier, Side.Red, new Position(0, 6)),
            new("red-soldier-2", PieceType.Soldier, Side.Red, new Position(2, 6)),
            new("red-soldier-3", PieceType.Soldier, Side.Red, new Position(4, 6)),
            new("red-soldier-4", PieceType.Soldier, Side.Red, new Position(6, 6)),
            new("red-soldier-5", PieceType.Soldier, Side.Red, new Position(8, 6)),

            new("red-cannon-1", PieceType.Cannon, Side.Red, new Position(1, 7)),
            new("red-cannon-2", PieceType.Cannon, Side.Red, new Position(7, 7)),

            new("red-chariot-1", PieceType.Chariot, Side.Red, new Position(0, 9)),
            new("red-horse-1", PieceType.Horse, Side.Red, new Position(1, 9)),
            new("red-elephant-1", PieceType.Elephant, Side.Red, new Position(2, 9)),
            new("red-advisor-1", PieceType.Advisor, Side.Red, new Position(3, 9)),
            new("red-general-1", PieceType.General, Side.Red, new Position(4, 9)),
            new("red-advisor-2", PieceType.Advisor, Side.Red, new Position(5, 9)),
            new("red-elephant-2", PieceType.Elephant, Side.Red, new Position(6, 9)),
            new("red-horse-2", PieceType.Horse, Side.Red, new Position(7, 9)),
            new("red-chariot-2", PieceType.Chariot, Side.Red, new Position(8, 9))
        };

        return pieces;
    }
}
