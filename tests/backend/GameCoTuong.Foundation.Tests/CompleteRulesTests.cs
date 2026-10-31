using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;

namespace GameCoTuong.Foundation.Tests;

public sealed class CompleteRulesTests
{
    private static Piece P(string id, PieceType type, Side side, int col, int row) => new(id,type,side,new(col,row));
    internal static Board Mirror(Board board) => new(board.Select(p => new Piece(
        p.Side == Side.Red ? "black-"+p.Id[4..] : "red-"+p.Id[6..], p.Type, XiangqiRules.Opponent(p.Side), new(8-p.Position.Col,9-p.Position.Row))));

    // Explicit expected positions, including captures and own-colour destinations.
    // type, from, to, blocker (-1 = none), target (0 = none, 1 = own, 2 = enemy), expected
    public static IEnumerable<object[]> GeometryCases()
    {
        object[][] rows = [
            [PieceType.Soldier,0,6,0,5,-1,-1,0,true], [PieceType.Soldier,0,6,1,6,-1,-1,0,false],
            [PieceType.Soldier,0,4,1,4,-1,-1,0,true], [PieceType.Soldier,0,4,0,5,-1,-1,0,false],
            [PieceType.Soldier,0,4,0,2,-1,-1,0,false], [PieceType.Soldier,0,4,0,3,-1,-1,2,true],
            [PieceType.Soldier,0,4,0,3,-1,-1,1,false],
            [PieceType.Chariot,0,9,0,0,-1,-1,0,true], [PieceType.Chariot,0,9,1,8,-1,-1,0,false],
            [PieceType.Chariot,0,9,0,2,0,5,2,false], [PieceType.Chariot,0,9,0,2,-1,-1,2,true],
            [PieceType.Chariot,0,9,0,2,-1,-1,1,false],
            [PieceType.Horse,1,9,2,7,-1,-1,0,true], [PieceType.Horse,1,9,3,8,-1,-1,2,true],
            [PieceType.Horse,1,9,2,7,1,8,0,false], [PieceType.Horse,1,9,3,8,2,9,0,false],
            [PieceType.Horse,1,9,2,8,-1,-1,0,false], [PieceType.Horse,1,9,2,7,-1,-1,1,false],
            [PieceType.Cannon,0,7,0,2,-1,-1,0,true], [PieceType.Cannon,0,7,0,2,0,4,0,false],
            [PieceType.Cannon,0,7,0,2,-1,-1,2,false], [PieceType.Cannon,0,7,0,2,0,4,2,true],
            [PieceType.Cannon,0,7,1,6,-1,-1,0,false], [PieceType.Cannon,0,7,0,2,0,4,1,false],
            [PieceType.Elephant,2,9,0,7,-1,-1,0,true], [PieceType.Elephant,2,9,0,7,1,8,0,false],
            [PieceType.Elephant,2,5,0,3,-1,-1,0,false], [PieceType.Elephant,2,9,1,8,-1,-1,0,false],
            [PieceType.Elephant,2,9,0,7,-1,-1,2,true], [PieceType.Elephant,2,9,0,7,-1,-1,1,false],
            [PieceType.Advisor,3,9,4,8,-1,-1,0,true], [PieceType.Advisor,3,9,2,8,-1,-1,0,false],
            [PieceType.Advisor,3,9,3,8,-1,-1,0,false], [PieceType.Advisor,3,9,4,8,-1,-1,2,true],
            [PieceType.Advisor,3,9,4,8,-1,-1,1,false],
            [PieceType.General,4,9,3,9,-1,-1,0,true], [PieceType.General,4,9,4,8,-1,-1,2,true],
            [PieceType.General,4,7,4,6,-1,-1,0,false], [PieceType.General,4,9,3,8,-1,-1,0,false],
            [PieceType.General,4,9,3,9,-1,-1,1,false]
        ];
        foreach (var row in rows) foreach (var mirrored in new[] { false,true }) yield return row.Concat(new object[] { mirrored }).ToArray();
    }
    [Theory] [MemberData(nameof(GeometryCases))]
    public void Seven_piece_rules_for_both_colours(PieceType type,int x,int y,int tx,int ty,int bx,int by,int target,bool expected,bool mirrored)
    {
        var moving = P(type==PieceType.General?"red-general-1":"red-test-1",type,Side.Red,x,y);
        var pieces = new List<Piece> { P("black-general-1",PieceType.General,Side.Black,4,0),P("red-soldier-5",PieceType.Soldier,Side.Red,4,5),moving };
        if(type!=PieceType.General) pieces.Add(P("red-general-1",PieceType.General,Side.Red,4,9));
        if(bx>=0) pieces.Add(P("black-blocker-1",PieceType.Soldier,Side.Black,bx,by));
        if(target!=0) pieces.Add(P(target==1?"red-target-1":"black-target-1",PieceType.Horse,target==1?Side.Red:Side.Black,tx,ty));
        var board=new Board(pieces); var to=new Position(tx,ty);
        if(mirrored) { board=Mirror(board); moving=board.GetPieceAt(8-x,9-y)!; to=new(8-tx,9-ty); }
        Assert.Equal(expected,XiangqiRules.IsPseudoLegal(board,moving,to));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Removing_the_only_screen_is_self_check_and_generals_cannot_be_captured(bool mirrored)
    {
        var board=new Board(new[] {P("red-general-1",PieceType.General,Side.Red,4,9),P("black-general-1",PieceType.General,Side.Black,4,0),P("red-chariot-1",PieceType.Chariot,Side.Red,4,5)});
        if(mirrored)board=Mirror(board);
        var side=mirrored?Side.Black:Side.Red; var rook=board.Single(p=>p.Type==PieceType.Chariot);
        var sidestep=new Position(mirrored?5:3,mirrored?4:5);
        Assert.True(XiangqiRules.IsPseudoLegal(board,rook,sidestep)); Assert.False(XiangqiRules.IsLegal(board,side,new(rook.Id,sidestep)));
        Assert.False(XiangqiRules.IsLegal(board,side,new(rook.Id,board.GetGeneral(XiangqiRules.Opponent(side)).Position)));
        Assert.Equal(3,board.Count);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Double_check_requires_a_move_that_resolves_both_attacks(bool mirrored)
    {
        var board=new Board(new[] {P("red-general-1",PieceType.General,Side.Red,4,9),P("black-general-1",PieceType.General,Side.Black,3,0),
            P("black-chariot-1",PieceType.Chariot,Side.Black,4,3),P("black-horse-1",PieceType.Horse,Side.Black,2,8),P("red-chariot-1",PieceType.Chariot,Side.Red,0,5)});
        if(mirrored)board=Mirror(board);
        var side=mirrored?Side.Black:Side.Red; var rook=board.Single(p=>p.Type==PieceType.Chariot&&p.Side==side);
        Assert.True(XiangqiRules.IsInCheck(board,side));
        Assert.False(XiangqiRules.IsLegal(board,side,new(rook.Id,new(mirrored?4:4,mirrored?4:5))));
        var legal=XiangqiRules.Generate(board,side); Assert.NotEmpty(legal);
        Assert.All(legal,m=>Assert.Equal(PieceType.General,board.GetPieceById(m.PieceId)!.Type));
        Assert.All(legal,m=>Assert.False(XiangqiRules.IsInCheck(board.ApplyMove(m.PieceId,m.To,out _),side)));
    }
    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void Checkmate_and_stalemate_lose_for_either_colour(bool check,bool mirrored)
    {
        var board=XiangqiRulesTests.Mate(check); var turn=Side.Black;
        if(mirrored){board=Mirror(board);turn=Side.Red;}
        var result=XiangqiRules.Result(board,turn);
        Assert.True(result.Finished); Assert.Equal(XiangqiRules.Opponent(turn),result.Winner); Assert.Equal(check?"checkmate":"stalemate",result.Reason);
    }
}
