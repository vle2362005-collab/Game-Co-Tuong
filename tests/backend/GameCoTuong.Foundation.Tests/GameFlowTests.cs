using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Game;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Engine.Model;
using GameCoTuong.Api.Hubs;

namespace GameCoTuong.Foundation.Tests;

public sealed class GameFlowTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"gct-flow-{Guid.NewGuid():N}.db");
    private readonly Guid red = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid black = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid outsider = Guid.NewGuid();
    private GameDbContext Db() => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite($"Data Source={path}").Options);
    public GameFlowTests()
    {
        using var db = Db(); db.Database.EnsureCreated();
        db.Users.Add(new User { Id = outsider, Username = "outsider", NormalizedUsername = "outsider", Email = "out@test.local", NormalizedEmail = "out@test.local", DisplayName = "Outsider", PasswordHash = "test" }); db.SaveChanges();
    }
    private async Task<T> Run<T>(Func<GameService, Task<T>> action) { await using var db = Db(); return await action(new GameService(db)); }
    private async Task<(RoomView Room, Guid Match)> Start()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(),"Test room")));
        await Run(s => s.Join(black, room.Code));
        await Run(s => s.Presence(room.Id,red,true)); await Run(s => s.Presence(room.Id,black,true));
        await Run(s => s.Ready(room.Id,red,true)); await Run(s => s.Ready(room.Id,black,true));
        room = await Run(s => s.Start(room.Id,red,new(Guid.NewGuid())));
        return (room,room.MatchId!.Value);
    }
    [Fact] public async Task Create_retry_join_race_and_access_checks()
    {
        var input = new CreateRoomInput(Guid.NewGuid(),"Race room");
        var a = await Run(s => s.Create(red,input)); var retry = await Run(s => s.Create(red,input));
        Assert.Equal(a.Id,retry.Id);
        Assert.Equal("ALREADY_IN_ROOM",(await Assert.ThrowsAsync<GameFault>(() => Run(s => s.Create(red,new(Guid.NewGuid(),"Second"))))).Code);
        async Task<bool> Join(Guid user) { try { await Run(s => s.Join(user,a.Code)); return true; } catch (GameFault e) { Assert.Equal("ROOM_FULL",e.Code); return false; } }
        var outcomes = await Task.WhenAll(Join(black),Join(outsider)); Assert.Single(outcomes,x=>x);
        var room = await Run(s=>s.Snapshot(a.Id,red)); Assert.Equal(2,room.Members.Count);
        var denied = room.Members.Any(m=>m.UserId==black) ? outsider : black;
        Assert.Equal(403,(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Snapshot(a.Id,denied)))).Status);
        Assert.Equal("NOT_READY",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Start(a.Id,red,new(Guid.NewGuid()))))).Code);
    }
    [Fact] public async Task Start_and_move_retries_commit_once_and_history_replays()
    {
        var (room,id) = await Start();
        var starts = await Task.WhenAll(Run(s=>s.Start(room.Id,red,new(Guid.NewGuid()))),Run(s=>s.Start(room.Id,red,new(Guid.NewGuid()))));
        Assert.All(starts,s=>Assert.Equal(id,s.MatchId));
        var bad = new MoveInput(Guid.NewGuid(),1,"red-chariot-1",1,8);
        Assert.Equal("INVALID_MOVE",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,bad)))).Code);
        var move = new MoveInput(Guid.NewGuid(),1,"red-soldier-1",0,5);
        await Task.WhenAll(Run(s=>s.Move(id,red,move)),Run(s=>s.Move(id,red,move)));
        await using var db = Db();
        Assert.Equal(1,await db.Moves.CountAsync(m=>m.MatchId==id)); Assert.Equal(1,await db.Matches.CountAsync(m=>m.RoomId==room.Id));
        Assert.Equal("REQUEST_CONFLICT",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,move with { ToRow=4 })))).Code);
        Assert.Equal("VERSION_CONFLICT",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,black,new(Guid.NewGuid(),1,"black-soldier-1",0,4))))).Code);
        Assert.Equal("NOT_YOUR_TURN",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,new(Guid.NewGuid(),2,"red-soldier-1",0,4))))).Code);
        var saved = await db.Matches.AsNoTracking().SingleAsync(m=>m.Id==id);
        var replay = BoardSerializer.Deserialize(saved.InitialBoardJson);
        foreach (var m in await db.Moves.Where(m=>m.MatchId==id).OrderBy(m=>m.Ply).ToListAsync()) replay = replay.ApplyMove(m.PieceId,new(m.ToCol,m.ToRow),out _);
        Assert.Equal(saved.CurrentBoardJson,BoardSerializer.Serialize(replay));
        await Run(s=>s.End(id,red,false)); await Run(s=>s.End(id,black,false));
        await using var verify = Db(); var ended=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal("black",ended.Winner); Assert.Equal("resignation",ended.ResultReason);
    }
    [Fact] public async Task Save_failure_rolls_back_move_board_and_turn()
    {
        var (_,id)=await Start();
        await using(var db=Db()) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_move BEFORE INSERT ON Moves BEGIN SELECT RAISE(ABORT, 'test write failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(()=>Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5))));
        await using var verify=Db(); var match=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal(1,match.Version); Assert.Equal("red",match.SideToMove); Assert.Equal(0,match.MoveNumber); Assert.Empty(await verify.Moves.ToListAsync());
        Assert.Equal(match.InitialBoardJson,match.CurrentBoardJson);
    }
    [Fact] public async Task Chat_is_idempotent_limited_and_members_only()
    {
        var room=await Run(s=>s.Create(red,new(Guid.NewGuid(),"Chat")));
        var input=new ChatInput(Guid.NewGuid(),"<script>literal text</script>");
        var message=await Run(s=>s.Chat(room.Id,red,input)); var retry=await Run(s=>s.Chat(room.Id,red,input)); Assert.Equal(message.Id,retry.Id);
        await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,outsider,input)));
        for(int i=0;i<4;i++) await Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),$"Message {i}")));
        Assert.Equal("RATE_LIMITED",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),"Sixth"))))).Code);
        Assert.Equal(5,(await Run(s=>s.Messages(room.Id,red))).Count);
    }
    [Fact] public void Video_session_blocks_wrong_ids_wrong_answerer_and_stale_ice()
    {
        var calls=new VideoCalls(); var room=Guid.NewGuid(); var id=Guid.NewGuid();
        calls.Validate(room,red,new(id,black,"offer","{}"));
        Assert.Throws<GameFault>(()=>calls.Validate(room,red,new(id,black,"answer","{}")));
        Assert.Throws<GameFault>(()=>calls.Validate(room,black,new(Guid.NewGuid(),red,"ice","{}")));
        calls.Validate(room,black,new(id,red,"answer","{}")); calls.Validate(room,red,new(id,black,"ice","{}"));
        calls.Validate(room,black,new(id,red,"hangup","{}"));
        Assert.Throws<GameFault>(()=>calls.Validate(room,red,new(id,black,"ice","{}")));
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
}
