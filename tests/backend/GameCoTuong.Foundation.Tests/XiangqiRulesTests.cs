using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Engine.AI;
using GameCoTuong.Engine.Serialization;

namespace GameCoTuong.Foundation.Tests;

public sealed class XiangqiRulesTests
{
    internal static Piece P(string name, PieceType type, Side side, int col, int row) => new(name, type, side, new(col, row));
    internal static Board Fixture(params Piece[] extra) => new(new[] {
        P("red-general-1", PieceType.General, Side.Red, 4, 9), P("black-general-1", PieceType.General, Side.Black, 4, 0),
        P("red-soldier-5", PieceType.Soldier, Side.Red, 4, 5)
    }.Concat(extra));
    internal static Board Mate(bool inCheck) => Fixture(
        P("red-chariot-1", PieceType.Chariot, Side.Red, 3, 1),
        P("red-chariot-2", PieceType.Chariot, Side.Red, 5, 1),
        P("red-soldier-1", PieceType.Soldier, Side.Red, inCheck ? 4 : 0, inCheck ? 1 : 4));

    [Theory]
    [InlineData(PieceType.Soldier, 0,6,0,5,true)]
    [InlineData(PieceType.Soldier, 0,6,1,6,false)]
    [InlineData(PieceType.Soldier, 0,4,1,4,true)]
    [InlineData(PieceType.Soldier, 0,4,0,5,false)]
    [InlineData(PieceType.Chariot, 0,8,0,1,true)]
    [InlineData(PieceType.Chariot, 0,8,1,7,false)]
    [InlineData(PieceType.Horse, 1,9,2,7,true)]
    [InlineData(PieceType.Horse, 1,9,2,8,false)]
    [InlineData(PieceType.Cannon, 1,7,1,2,true)]
    [InlineData(PieceType.Elephant, 2,9,0,7,true)]
    [InlineData(PieceType.Elephant, 2,5,4,3,false)]
    [InlineData(PieceType.Advisor, 3,9,4,8,true)]
    [InlineData(PieceType.Advisor, 3,9,2,8,false)]
    public void Piece_geometry(PieceType type, int x, int y, int tx, int ty, bool expected)
    {
        var piece = P("red-test-1", type, Side.Red, x, y);
        Assert.Equal(expected, XiangqiRules.IsPseudoLegal(Fixture(piece), piece, new(tx, ty)));
    }

    [Fact] public void Blockers_stop_rook_horse_elephant_and_cannon_capture_requires_exactly_one_screen()
    {
        foreach (var (type, from, to, leg) in new[] {
            (PieceType.Chariot, new Position(0,9), new Position(0,6), new Position(0,8)),
            (PieceType.Horse, new Position(1,9), new Position(2,7), new Position(1,8)),
            (PieceType.Elephant, new Position(2,9), new Position(0,7), new Position(1,8)) })
        {
            var piece = P("red-test-1", type, Side.Red, from.Col, from.Row);
            Assert.False(XiangqiRules.IsPseudoLegal(Fixture(piece, P("red-test-2", PieceType.Soldier, Side.Red, leg.Col, leg.Row)), piece, to));
        }
        var cannon = P("red-cannon-1", PieceType.Cannon, Side.Red, 0,7);
        var target = P("black-horse-1", PieceType.Horse, Side.Black, 0,2);
        var screen = P("black-soldier-1", PieceType.Soldier, Side.Black, 0,4);
        Assert.False(XiangqiRules.IsPseudoLegal(Fixture(cannon,target), cannon, target.Position));
        Assert.True(XiangqiRules.IsPseudoLegal(Fixture(cannon,target,screen), cannon, target.Position));
        Assert.False(XiangqiRules.IsPseudoLegal(Fixture(cannon,target,screen,P("red-soldier-1",PieceType.Soldier,Side.Red,0,5)), cannon,target.Position));
    }

    [Fact] public void General_palace_and_exposed_facing_generals_are_rejected()
    {
        var board = Fixture(); var general = board.GetGeneral(Side.Red);
        Assert.True(XiangqiRules.IsPseudoLegal(board, general, new(3,9)));
        Assert.False(XiangqiRules.IsPseudoLegal(board, general, new(4,6)));
        Assert.False(XiangqiRules.IsLegal(board, Side.Red, new("red-soldier-5",new(3,5))));
        var exposed = board.ApplyMove("red-soldier-5",new(3,5),out _);
        Assert.True(XiangqiRules.GeneralsFace(exposed));
    }
    [Fact] public void Legal_generation_excludes_self_check_and_does_not_change_original()
    {
        var board = Fixture(P("black-chariot-1",PieceType.Chariot,Side.Black,4,3));
        var original = BoardSerializer.Serialize(board);
        var moves = XiangqiRules.Generate(board,Side.Red);
        Assert.All(moves, m => Assert.False(XiangqiRules.IsInCheck(board.ApplyMove(m.PieceId,m.To,out _),Side.Red)));
        Assert.Equal(original,BoardSerializer.Serialize(board));
    }
    [Theory] [InlineData(true,"checkmate")] [InlineData(false,"stalemate")]
    public void No_legal_moves_loses_even_without_check(bool check, string reason)
    {
        var result = XiangqiRules.Result(Mate(check),Side.Black);
        Assert.True(result.Finished); Assert.Equal(Side.Red,result.Winner); Assert.Equal(reason,result.Reason);
    }
    [Fact] public void Simulation_preserves_board_turn_and_capture_for_sibling_branches()
    {
        var original = GameState.CreateInitial(); var before = BoardSerializer.Serialize(original.Board);
        var a = original.ApplyMove("red-soldier-1",new(0,5),out _);
        var b = original.ApplyMove("red-soldier-2",new(2,5),out _);
        Assert.Equal(before,BoardSerializer.Serialize(original.Board)); Assert.Equal(Side.Red,original.SideToMove);
        Assert.Equal(Side.Black,a.SideToMove); Assert.Equal(32,b.Board.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(-1,0));
    }
    [Fact] public void Evaluation_is_symmetric_and_minimax_returns_legal_move_without_mutation()
    {
        var board = BoardInitializer.CreateInitialBoard(); var before = BoardSerializer.Serialize(board);
        Assert.Equal(-Minimax.Evaluate(board,Side.Black),Minimax.Evaluate(board,Side.Red));
        var move = Minimax.FindBest(board,Side.Red,1);
        Assert.NotNull(move); Assert.True(XiangqiRules.IsLegal(board,Side.Red,move));
        Assert.Equal(before,BoardSerializer.Serialize(board)); Assert.Null(Minimax.FindBest(Mate(true),Side.Black));
    }
    [Fact] public void Minimax_prefers_immediate_checkmate()
    {
        var board = Fixture(P("red-chariot-1",PieceType.Chariot,Side.Red,3,1),
            P("red-chariot-2",PieceType.Chariot,Side.Red,5,1), P("red-soldier-1",PieceType.Soldier,Side.Red,4,2));
        var move = Minimax.FindBest(board,Side.Red,1);
        Assert.NotNull(move);
        var result = XiangqiRules.Result(board.ApplyMove(move.PieceId,move.To,out _),Side.Black);
        Assert.True(result.Finished); Assert.Equal(Side.Red,result.Winner);
    }
}
