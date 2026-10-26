using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Serialization;

namespace GameCoTuong.Foundation.Tests;

public sealed class EngineModelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 9)]
    [InlineData(4, 5)]
    [InlineData(3, 2)]
    public void Position_valid_coordinates_succeed(int col, int row)
    {
        var pos = new Position(col, row);
        Assert.Equal(col, pos.Col);
        Assert.Equal(row, pos.Row);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(9, 0)]
    [InlineData(0, 10)]
    [InlineData(-5, 15)]
    public void Position_out_of_bounds_throws_ArgumentOutOfRangeException(int col, int row)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(col, row));
        Assert.False(Position.IsValid(col, row));
        Assert.False(Position.TryCreate(col, row, out _));
    }

    [Fact]
    public void Piece_valid_id_and_side_succeeds()
    {
        var piece = new Piece("red-general-1", PieceType.General, Side.Red, new Position(4, 9));
        Assert.Equal("red-general-1", piece.Id);
        Assert.Equal(PieceType.General, piece.Type);
        Assert.Equal(Side.Red, piece.Side);
        Assert.Equal(new Position(4, 9), piece.Position);
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("red-soldier-6")]
    [InlineData("red-cannon-0")]
    [InlineData("")]
    public void Piece_invalid_id_format_throws_ArgumentException(string id)
    {
        Assert.Throws<ArgumentException>(() => new Piece(id, PieceType.Soldier, Side.Red, new Position(0, 6)));
    }

    [Fact]
    public void Piece_mismatched_id_and_side_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Piece("black-general-1", PieceType.General, Side.Red, new Position(4, 9)));
    }

    [Fact]
    public void Board_initial_state_has_32_pieces_and_correct_generals()
    {
        var board = BoardInitializer.CreateInitialBoard();

        Assert.Equal(32, board.Count);
        Assert.Equal(16, board.Count(p => p.Side == Side.Red));
        Assert.Equal(16, board.Count(p => p.Side == Side.Black));

        var redGeneral = board.GetGeneral(Side.Red);
        Assert.Equal("red-general-1", redGeneral.Id);
        Assert.Equal(new Position(4, 9), redGeneral.Position);

        var blackGeneral = board.GetGeneral(Side.Black);
        Assert.Equal("black-general-1", blackGeneral.Id);
        Assert.Equal(new Position(4, 0), blackGeneral.Position);
    }

    [Fact]
    public void Board_duplicate_positions_throw_ArgumentException()
    {
        var pieces = new List<Piece>
        {
            new("red-general-1", PieceType.General, Side.Red, new Position(4, 9)),
            new("black-general-1", PieceType.General, Side.Black, new Position(4, 9)) // trùng ô (4, 9)
        };

        Assert.Throws<ArgumentException>(() => new Board(pieces));
    }

    [Fact]
    public void Board_duplicate_ids_throw_ArgumentException()
    {
        var pieces = new List<Piece>
        {
            new("red-general-1", PieceType.General, Side.Red, new Position(4, 9)),
            new("red-general-1", PieceType.General, Side.Red, new Position(4, 8)) // trùng ID
        };

        Assert.Throws<ArgumentException>(() => new Board(pieces));
    }

    [Theory]
    [InlineData(3, 0, Side.Black, true)]
    [InlineData(4, 1, Side.Black, true)]
    [InlineData(5, 2, Side.Black, true)]
    [InlineData(2, 1, Side.Black, false)] // ngoài cột cung
    [InlineData(4, 3, Side.Black, false)] // ngoài hàng cung
    [InlineData(3, 9, Side.Red, true)]
    [InlineData(4, 8, Side.Red, true)]
    [InlineData(5, 7, Side.Red, true)]
    [InlineData(4, 6, Side.Red, false)]   // ngoài hàng cung Đỏ
    public void BoardGeometry_palace_detection(int col, int row, Side side, bool expectedInPalace)
    {
        var pos = new Position(col, row);
        Assert.Equal(expectedInPalace, BoardGeometry.IsInPalace(side, pos));
    }

    [Theory]
    [InlineData(0, 3, Side.Black, false)]
    [InlineData(0, 4, Side.Black, false)]
    [InlineData(0, 5, Side.Black, true)]  // Đen qua sông tại hàng >= 5
    [InlineData(0, 6, Side.Red, false)]
    [InlineData(0, 5, Side.Red, false)]
    [InlineData(0, 4, Side.Red, true)]   // Đỏ qua sông tại hàng <= 4
    public void BoardGeometry_river_crossing_detection(int col, int row, Side side, bool expectedCrossed)
    {
        var pos = new Position(col, row);
        Assert.Equal(expectedCrossed, BoardGeometry.HasCrossedRiver(side, pos));
    }

    [Fact]
    public void BoardSerializer_roundtrip_preserves_all_pieces_and_positions()
    {
        var originalBoard = BoardInitializer.CreateInitialBoard();
        var json = BoardSerializer.Serialize(originalBoard);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("red-general-1", json);
        Assert.Contains("black-general-1", json);

        var deserializedBoard = BoardSerializer.Deserialize(json);
        Assert.Equal(originalBoard.Count, deserializedBoard.Count);

        foreach (var originalPiece in originalBoard)
        {
            var matchingPiece = deserializedBoard.GetPieceById(originalPiece.Id);
            Assert.NotNull(matchingPiece);
            Assert.Equal(originalPiece.Type, matchingPiece.Type);
            Assert.Equal(originalPiece.Side, matchingPiece.Side);
            Assert.Equal(originalPiece.Position, matchingPiece.Position);
        }
    }

    [Fact]
    public void GameState_initial_and_move_progression()
    {
        var state = GameState.CreateInitial();
        Assert.Equal(Side.Red, state.SideToMove);
        Assert.Equal(0, state.MoveNumber);
        Assert.Equal(1, state.Version);

        // Nước đi đầu tiên: Đỏ đi Tốt biên (0, 6) -> (0, 5)
        var nextState = state.ApplyMove("red-soldier-1", new Position(0, 5), out var captured);
        Assert.Null(captured);
        Assert.Equal(Side.Black, nextState.SideToMove);
        Assert.Equal(1, nextState.MoveNumber);
        Assert.Equal(2, nextState.Version);
        Assert.Equal(new Position(0, 5), nextState.Board.GetPieceById("red-soldier-1")!.Position);

        // Không thể đi quân đối thủ khi không đúng lượt
        Assert.Throws<InvalidOperationException>(() =>
            nextState.ApplyMove("red-soldier-2", new Position(2, 5), out _));
    }
}
