using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Infrastructure.Game;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GameCoTuong.Foundation.Tests;

public sealed class CoreApiAcceptanceTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(),$"gct-api-s4-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> root = new();
    private readonly WebApplicationFactory<Program> factory;
    private readonly List<HttpClient> clients = [];
    private readonly EventRecorder events = new();
    private readonly CaptureLogs logs = new();
    private sealed record TestUser(HttpClient Client,Guid Id,string Token,string Username,string Password);
    public CoreApiAcceptanceTests()
    {
        factory=root.WithWebHostBuilder(builder=>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Database:Provider"]="Sqlite",["ConnectionStrings:GameDatabase"]=$"Data Source={path};Default Timeout=30"
            }));
            builder.ConfigureServices(services=>
            {
                services.RemoveAll<IHubContext<GameHub>>(); services.AddSingleton<IHubContext<GameHub>>(events);
                services.AddLogging(l=>l.AddProvider(logs));
            });
        });
    }
    private HttpClient Client() { var c=factory.CreateClient(); clients.Add(c); return c; }
    private GameDbContext Db() => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite($"Data Source={path}").Options);
    private static async Task<JsonElement> Data(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    {
        Assert.Equal(expected,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("data").Clone();
    }
    private async Task<TestUser> User(string prefix)
    {
        var client=Client();var username=prefix+Guid.NewGuid().ToString("N")[..8];const string password="Password123!";
        var registered=await client.PostAsJsonAsync("/api/v1/auth/register",new RegisterRequest(Guid.NewGuid(),username,"Test "+prefix,password,username+"@example.com"));
        var body=await registered.Content.ReadAsStringAsync();Assert.Equal(HttpStatusCode.Created,registered.StatusCode);
        Assert.DoesNotContain(password,body);Assert.DoesNotContain("passwordHash",body,StringComparison.OrdinalIgnoreCase);
        var login=await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(Guid.NewGuid(),username,password));
        var session=(await login.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>())!.Data;
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.AccessToken);
        return new(client,session.User.Id,session.AccessToken,username,password);
    }
    private async Task<(TestUser Red,TestUser Black,Guid Room,Guid Match)> Start()
    {
        var red=await User("red");var black=await User("black");
        var room=await Data(await red.Client.PostAsJsonAsync("/api/v1/rooms",new {requestId=Guid.NewGuid(),name="Acceptance room"}));
        var roomId=room.GetProperty("id").GetGuid();var code=room.GetProperty("code").GetString();
        await Data(await black.Client.PostAsync($"/api/v1/rooms/join/{code}",null));
        await Data(await red.Client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/ready",new {ready=true}));
        await Data(await black.Client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/ready",new {ready=true}));
        var input=new {requestId=Guid.NewGuid()};
        var first=await Data(await red.Client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start",input));
        var retry=await Data(await red.Client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start",input));
        Assert.Equal(first.GetProperty("matchId").GetGuid(),retry.GetProperty("matchId").GetGuid());
        return(red,black,roomId,first.GetProperty("matchId").GetGuid());
    }
    private static object Move(long version,string piece="red-soldier-1",int col=0,int row=5,Guid? request=null) => new {requestId=request??Guid.NewGuid(),expectedVersion=version,pieceId=piece,toCol=col,toRow=row};
    private static async Task<string> Error(HttpResponseMessage response,HttpStatusCode expected)
    {
        Assert.Equal(expected,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();Assert.NotNull(error);return error.Error.Code;
    }
    [Fact] public async Task GCT31_account_validation_duplicate_race_tokens_and_sensitive_data()
    {
        var client=Client();var suffix=Guid.NewGuid().ToString("N")[..8];var username="race"+suffix;
        var request=new RegisterRequest(Guid.NewGuid(),username,"Race test","Password123!",username+"@example.com");
        var both=await Task.WhenAll(client.PostAsJsonAsync("/api/v1/auth/register",request),client.PostAsJsonAsync("/api/v1/auth/register",request with {RequestId=Guid.NewGuid()}));
        Assert.Single(both,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(both,r=>r.StatusCode==HttpStatusCode.Conflict);
        using(var db=Db())Assert.Equal(1,await db.Users.CountAsync(u=>u.NormalizedUsername==username));
        Assert.Equal("USERNAME_TAKEN",await Error(await client.PostAsJsonAsync("/api/v1/auth/register",request with {RequestId=Guid.NewGuid(),Username=username.ToUpperInvariant(),Email="other"+suffix+"@example.com"}),HttpStatusCode.Conflict));
        await Error(await client.PostAsJsonAsync("/api/v1/auth/register",request with {RequestId=Guid.NewGuid(),Username="x",Password="123",Email="invalid"}),HttpStatusCode.BadRequest);
        Assert.Equal("INVALID_CREDENTIALS",await Error(await client.PostAsJsonAsync("/api/v1/auth/login",new LoginRequest(Guid.NewGuid(),username,"WrongPassword123!")),HttpStatusCode.Unauthorized));
        var user=await User("valid");var me=await Data(await user.Client.GetAsync("/api/v1/users/me"));Assert.Equal(user.Id,me.GetProperty("id").GetGuid());
        var raw=me.GetRawText();Assert.DoesNotContain(user.Password,raw);Assert.DoesNotContain("hash",raw,StringComparison.OrdinalIgnoreCase);
        using var scope=factory.Services.CreateScope();var tokenService=scope.ServiceProvider.GetRequiredService<ITokenService>();
        foreach(var token in new[] {user.Token+"x",tokenService.GenerateToken(new UserPublicDto(user.Id,user.Username,"Expired"),TimeSpan.FromSeconds(-10)).AccessToken})
        {
            var invalid=Client();invalid.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
            await Error(await invalid.GetAsync("/api/v1/users/me"),HttpStatusCode.Unauthorized);
        }
        await Data(await user.Client.PostAsync("/api/v1/auth/logout",null));await Error(await user.Client.GetAsync("/api/v1/users/me"),HttpStatusCode.Unauthorized);
        var logged=string.Join('\n',logs.Messages);Assert.DoesNotContain(user.Password,logged);Assert.DoesNotContain(user.Token,logged);
    }
    [Fact] public async Task GCT47_room_membership_leave_close_and_permission_flow()
    {
        var red=await User("host");var black=await User("guest");var outsider=await User("third");
        var input=new {requestId=Guid.NewGuid(),name="Room flow"};
        var room=await Data(await red.Client.PostAsJsonAsync("/api/v1/rooms",input));var id=room.GetProperty("id").GetGuid();var code=room.GetProperty("code").GetString();
        Assert.Matches("^[0-9]{6}$",code!);Assert.Equal(1,room.GetProperty("members").GetArrayLength());
        Assert.Equal(id,(await Data(await red.Client.PostAsJsonAsync("/api/v1/rooms",input))).GetProperty("id").GetGuid());
        await Error(await outsider.Client.GetAsync($"/api/v1/rooms/{id}"),HttpStatusCode.Forbidden);
        await Error(await red.Client.PostAsJsonAsync($"/api/v1/rooms/{id}/start",new {requestId=Guid.NewGuid()}),HttpStatusCode.Conflict);
        await Data(await black.Client.PostAsync($"/api/v1/rooms/join/{code}",null));
        await Error(await outsider.Client.PostAsync($"/api/v1/rooms/join/{code}",null),HttpStatusCode.Conflict);
        await Error(await black.Client.PostAsJsonAsync($"/api/v1/rooms/{id}/start",new {requestId=Guid.NewGuid()}),HttpStatusCode.Forbidden);
        await Data(await red.Client.PostAsJsonAsync($"/api/v1/rooms/{id}/ready",new {ready=true}));await Data(await black.Client.PostAsync($"/api/v1/rooms/{id}/leave",null));
        var afterLeave=await Data(await red.Client.GetAsync($"/api/v1/rooms/{id}"));Assert.Equal(1,afterLeave.GetProperty("members").GetArrayLength());Assert.False(afterLeave.GetProperty("members")[0].GetProperty("ready").GetBoolean());
        await Data(await black.Client.PostAsync($"/api/v1/rooms/join/{code}",null));await Data(await red.Client.PostAsync($"/api/v1/rooms/{id}/close",null));
        await Error(await outsider.Client.PostAsync($"/api/v1/rooms/join/{code}",null),HttpStatusCode.NotFound);
        var list=await Data(await outsider.Client.GetAsync("/api/v1/rooms"));Assert.DoesNotContain(list.EnumerateArray(),r=>r.GetProperty("id").GetGuid()==id);
    }
    [Fact] public async Task GCT115_invalid_requests_leave_board_turn_history_and_notifications_unchanged()
    {
        var (red,black,_,id)=await Start();var outsider=await User("outside");var url=$"/api/v1/matches/{id}/moves";
        var before=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"));var eventsBefore=events.Messages.Count(m=>m=="MatchChanged");
        Assert.Equal("NOT_YOUR_TURN",await Error(await black.Client.PostAsJsonAsync(url,Move(1,"black-soldier-1",0,4)),HttpStatusCode.Forbidden));
        Assert.Equal("NOT_MATCH_PLAYER",await Error(await outsider.Client.PostAsJsonAsync(url,new {requestId=Guid.NewGuid(),expectedVersion=1,pieceId="red-soldier-1",toCol=0,toRow=5,spoofedUserId=red.Id}),HttpStatusCode.Forbidden));
        foreach(var input in new[] {Move(1,col:-1),Move(1,col:9),Move(1,row:10),Move(1,row:-1),Move(1,piece:"red-chariot-1",col:1,row:8),Move(1,piece:""),Move(1,request:Guid.Empty)})
            await Error(await red.Client.PostAsJsonAsync(url,input),HttpStatusCode.BadRequest);
        Assert.Equal("VERSION_CONFLICT",await Error(await red.Client.PostAsJsonAsync(url,Move(0)),HttpStatusCode.Conflict));
        var after=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"));Assert.Equal(before.GetRawText(),after.GetRawText());
        using var db=Db();Assert.Empty(await db.Moves.Where(m=>m.MatchId==id).ToListAsync());Assert.Equal(eventsBefore,events.Messages.Count(m=>m=="MatchChanged"));
    }
    [Fact] public async Task GCT115_duplicate_and_distinct_concurrent_commands_save_one_ply()
    {
        var (red,_,_,id)=await Start();var url=$"/api/v1/matches/{id}/moves";var input=Move(1);
        var repeated=await Task.WhenAll(red.Client.PostAsJsonAsync(url,input),red.Client.PostAsJsonAsync(url,input));Assert.All(repeated,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        using(var db=Db())Assert.Single(await db.Moves.Where(m=>m.MatchId==id).ToListAsync());
        var (red2,_,_,id2)=await Start();var url2=$"/api/v1/matches/{id2}/moves";
        var competing=await Task.WhenAll(red2.Client.PostAsJsonAsync(url2,Move(1)),red2.Client.PostAsJsonAsync(url2,Move(1,"red-soldier-2",2,5)));
        Assert.Single(competing,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(competing,r=>r.StatusCode==HttpStatusCode.Conflict);
        using var verify=Db();Assert.Single(await verify.Moves.Where(m=>m.MatchId==id2).ToListAsync());
    }
    [Fact] public async Task GCT115_save_failure_sends_no_success_and_leaves_both_snapshots_unchanged()
    {
        var (red,black,_,id)=await Start();var before=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"));
        var count=events.Messages.Count(m=>m=="MatchChanged");
        using(var db=Db())await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_move BEFORE INSERT ON Moves BEGIN SELECT RAISE(ABORT,'acceptance write failure'); END;");
        var response=await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",Move(1));Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        foreach(var player in new[] {red,black})Assert.Equal(before.GetRawText(),(await Data(await player.Client.GetAsync($"/api/v1/matches/{id}"))).GetRawText());
        Assert.Equal(count,events.Messages.Count(m=>m=="MatchChanged"));using var verify=Db();Assert.Empty(await verify.Moves.Where(m=>m.MatchId==id).ToListAsync());
    }
    [Fact] public async Task GCT82_capture_snapshot_contains_last_move_legal_moves_turn_and_check_at_one_version()
    {
        var (red,black,_,id)=await Start();var first=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"));
        Assert.True(first.GetProperty("lastMove").ValueKind==JsonValueKind.Null);Assert.NotEmpty(first.GetProperty("legalMoves").EnumerateArray());
        Assert.False(first.GetProperty("isCheck").GetBoolean());Assert.True(first.GetProperty("checkedKingPos").ValueKind==JsonValueKind.Null);
        var moved=await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",Move(1,"red-cannon-1",1,0)));
        Assert.Equal(31,moved.GetProperty("pieces").GetArrayLength());Assert.Equal(2,moved.GetProperty("version").GetInt64());Assert.Equal("black",moved.GetProperty("sideToMove").GetString());
        Assert.Equal("black-horse-1",moved.GetProperty("lastMove").GetProperty("capturedPieceId").GetString());
        var opponent=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"));Assert.Equal(moved.GetRawText(),opponent.GetRawText());
        var offered=await Data(await black.Client.PostAsync($"/api/v1/matches/{id}/draw",null));Assert.Equal(3,offered.GetProperty("version").GetInt64());
        var repeated=await Data(await black.Client.PostAsync($"/api/v1/matches/{id}/draw",null));Assert.Equal(3,repeated.GetProperty("version").GetInt64());
    }
    [Fact] public async Task GCT83_selected_hints_and_check_markers_update_in_api_and_signalr()
    {
        var (red,black,_,id)=await Start();
        var board=new Board(new[]
        {
            new Piece("red-general-1",PieceType.General,Side.Red,new Position(3,9)),
            new Piece("black-general-1",PieceType.General,Side.Black,new Position(4,0)),
            new Piece("red-chariot-1",PieceType.Chariot,Side.Red,new Position(4,3))
        });
        using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            match.SideToMove="black";
            match.CurrentBoardJson=BoardSerializer.Serialize(board);
            await db.SaveChangesAsync();
        }

        var checkedState=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"));
        Assert.True(checkedState.GetProperty("check").GetBoolean());
        Assert.True(checkedState.GetProperty("isCheck").GetBoolean());
        Assert.Equal(4,checkedState.GetProperty("checkedKingPos").GetProperty("col").GetInt32());
        Assert.Equal(0,checkedState.GetProperty("checkedKingPos").GetProperty("row").GetInt32());
        Assert.True(checkedState.GetProperty("lastMove").ValueKind==JsonValueKind.Null);

        var hints=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/legal-moves?pieceId=black-general-1"));
        Assert.Contains(hints.EnumerateArray(),move=>move.GetProperty("to").GetProperty("col").GetInt32()==5&&move.GetProperty("to").GetProperty("row").GetInt32()==0);
        Assert.All(hints.EnumerateArray(),move=>Assert.False(XiangqiRules.IsInCheck(
            board.ApplyMove(move.GetProperty("pieceId").GetString()!,new Position(
                move.GetProperty("to").GetProperty("col").GetInt32(),move.GetProperty("to").GetProperty("row").GetInt32()),out _),Side.Black)));
        Assert.Empty((await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/legal-moves?pieceId=red-chariot-1"))).EnumerateArray());

        var escaped=await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",Move(1,"black-general-1",5,0)));
        Assert.False(escaped.GetProperty("isCheck").GetBoolean());
        Assert.True(escaped.GetProperty("checkedKingPos").ValueKind==JsonValueKind.Null);
        Assert.Equal("black-general-1",escaped.GetProperty("lastMove").GetProperty("pieceId").GetString());
        Assert.Equal(5,escaped.GetProperty("lastMove").GetProperty("toCol").GetInt32());
        var pushed=JsonSerializer.SerializeToElement(events.Events.Last(e=>e.Method=="MatchChanged").Args[0],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(pushed.GetProperty("isCheck").GetBoolean());
        Assert.True(pushed.GetProperty("checkedKingPos").ValueKind==JsonValueKind.Null);
        Assert.Equal("black-general-1",pushed.GetProperty("lastMove").GetProperty("pieceId").GetString());

        var eventCount=events.Messages.Count(m=>m=="MatchChanged");
        Assert.Equal("INVALID_MOVE",await Error(await red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{id}/moves",Move(2,"red-chariot-1",5,0)),HttpStatusCode.BadRequest));
        Assert.Equal(escaped.GetRawText(),(await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"))).GetRawText());
        Assert.Equal(eventCount,events.Messages.Count(m=>m=="MatchChanged"));
        using var verify=Db();Assert.Single(await verify.Moves.Where(m=>m.MatchId==id).ToListAsync());
    }
    [Fact] public async Task GCT108_match_history_is_persistent_and_visible_only_to_players()
    {
        var (red,black,roomId,id)=await Start();
        await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",Move(1)));
        await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/resign",new {confirmed=true}));
        await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/resign",new {confirmed=true}));
        await Data(await red.Client.PostAsync($"/api/v1/rooms/{roomId}/close",null));

        var redHistory=await Data(await red.Client.GetAsync("/api/v1/matches/history"));
        var redMatch=Assert.Single(redHistory.GetProperty("items").EnumerateArray());
        Assert.Equal(id,redMatch.GetProperty("id").GetGuid());
        Assert.Equal("win",redMatch.GetProperty("outcome").GetString());
        Assert.Equal("red",redMatch.GetProperty("playerSide").GetString());
        Assert.Equal(1,redMatch.GetProperty("moveNumber").GetInt32());
        Assert.Equal("surrender",redMatch.GetProperty("reasonCode").GetString());
        var blackHistory=await Data(await black.Client.GetAsync("/api/v1/matches"));
        Assert.Equal("loss",blackHistory.GetProperty("items")[0].GetProperty("outcome").GetString());

        var detail=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}/history"));
        Assert.Equal(id,detail.GetProperty("id").GetGuid());
        Assert.Equal("resignation",detail.GetProperty("resultReason").GetString());
        Assert.Equal("red",detail.GetProperty("winner").GetString());
        Assert.Equal(black.Id,detail.GetProperty("opponent").GetProperty("userId").GetGuid());
        Assert.NotEqual(default,detail.GetProperty("startedAt").GetDateTimeOffset());
        Assert.NotEqual(default,detail.GetProperty("endedAt").GetDateTimeOffset());
        Assert.Single(detail.GetProperty("moves").EnumerateArray());

        var (drawRed,drawBlack,_,drawId)=await Start();
        var drawOffer=await Data(await drawRed.Client.PostAsync($"/api/v1/matches/{drawId}/offer-draw",null));
        await Data(await drawBlack.Client.PostAsJsonAsync($"/api/v1/matches/{drawId}/accept-draw",
            new {offerId=drawOffer.GetProperty("drawOfferId").GetGuid()}));
        var drawDetail=await Data(await drawRed.Client.GetAsync($"/api/v1/matches/{drawId}/history"));
        Assert.Equal("draw",drawDetail.GetProperty("outcome").GetString());
        Assert.Equal("draw-agreement",drawDetail.GetProperty("resultReason").GetString());

        var outsider=await User("history_outsider");
        Assert.Empty((await Data(await outsider.Client.GetAsync("/api/v1/matches"))).GetProperty("items").EnumerateArray());
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}/history"),HttpStatusCode.Forbidden);
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}"),HttpStatusCode.Forbidden);
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}/moves"),HttpStatusCode.Forbidden);
        var anonymous=Client();
        await Error(await anonymous.GetAsync("/api/v1/matches"),HttpStatusCode.Unauthorized);
        using var db=Db();
        Assert.Equal(1,await db.Matches.CountAsync(m=>m.Id==id));
        Assert.Equal(1,await db.Moves.CountAsync(m=>m.MatchId==id));
    }
    [Fact] public async Task GCT91_match_history_is_paginated_filtered_and_cancelled_matches_have_no_outcome()
    {
        var (red,_,roomId,id)=await Start();
        Guid cancelledId;
        using (var db=Db())
        {
            cancelledId=Guid.NewGuid();
            db.Matches.Add(new Match
            {
                Id=cancelledId,RoomId=roomId,Mode="pve",Status="cancelled",Winner="red",
                ResultReason="cancellation",StartedAt=DateTimeOffset.UtcNow.AddMinutes(1),
                EndedAt=DateTimeOffset.UtcNow,InitialBoardJson="{}",CurrentBoardJson="{}",
                Players=[new MatchPlayer {UserId=red.Id,Side="red"}]
            });
            await db.SaveChangesAsync();
        }

        var firstPage=await Data(await red.Client.GetAsync("/api/v1/matches/history?page=1&pageSize=1"));
        Assert.Equal(1,firstPage.GetProperty("page").GetInt32());
        Assert.Equal(1,firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2,firstPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2,firstPage.GetProperty("totalPages").GetInt32());
        var firstMatch=Assert.Single(firstPage.GetProperty("items").EnumerateArray());
        Assert.Equal(cancelledId,firstMatch.GetProperty("id").GetGuid());
        Assert.Equal("cancelled",firstMatch.GetProperty("outcome").GetString());
        Assert.Equal("pve",firstMatch.GetProperty("mode").GetString());
        Assert.Equal("Máy",firstMatch.GetProperty("opponent").GetProperty("displayName").GetString());
        Assert.Null(firstMatch.GetProperty("opponent").GetProperty("userId").GetString());

        var secondPage=await Data(await red.Client.GetAsync("/api/v1/matches/history?page=2&pageSize=1"));
        Assert.Equal(id,Assert.Single(secondPage.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1,(await Data(await red.Client.GetAsync(
            "/api/v1/matches/history?outcome=cancelled&mode=pve"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0,(await Data(await red.Client.GetAsync(
            "/api/v1/matches/history?outcome=win&mode=pve"))).GetProperty("totalCount").GetInt32());
        await Error(await red.Client.GetAsync("/api/v1/matches/history?page=0"),HttpStatusCode.BadRequest);
        await Error(await red.Client.GetAsync("/api/v1/matches/history?pageSize=101"),HttpStatusCode.BadRequest);
        await Error(await red.Client.GetAsync("/api/v1/matches/history?outcome=unknown"),HttpStatusCode.BadRequest);
    }
    [Fact] public async Task GCT116_closed_room_preserves_match_result_moves_and_player_access()
    {
        var (red,black,roomId,id)=await Start();
        var redRequest=Guid.NewGuid();
        var redMove=Move(1,request:redRequest);
        await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",redMove));
        await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",redMove));
        var blackMove=new {requestId=Guid.NewGuid(),expectedVersion=2,pieceId="black-soldier-1",toCol=0,toRow=4};
        await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",blackMove));

        var firstEnd=await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/resign",new {confirmed=true}));
        var retriedEnd=await Data(await black.Client.PostAsJsonAsync($"/api/v1/matches/{id}/resign",new {confirmed=true}));
        Assert.Equal(firstEnd.GetRawText(),retriedEnd.GetRawText());
        Assert.Equal("finished",firstEnd.GetProperty("status").GetString());
        Assert.Equal("red",firstEnd.GetProperty("winner").GetString());
        Assert.Equal("resignation",firstEnd.GetProperty("resultReason").GetString());

        var redDetails=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}/history"));
        var redMoves=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}/moves"));
        var redSnapshot=await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"));
        var blackDetails=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/history"));
        var blackMoves=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/moves"));
        var blackSnapshot=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"));

        var moves=redDetails.GetProperty("moves").EnumerateArray().ToArray();
        Assert.Equal(2,moves.Length);
        Assert.Equal(1,moves[0].GetProperty("ply").GetInt32());
        Assert.Equal(redRequest,moves[0].GetProperty("requestId").GetGuid());
        Assert.Equal("red",moves[0].GetProperty("side").GetString());
        Assert.Equal(2,moves[1].GetProperty("ply").GetInt32());
        Assert.Equal(blackMove.requestId,moves[1].GetProperty("requestId").GetGuid());
        Assert.Equal("black",moves[1].GetProperty("side").GetString());

        var replay=BoardSerializer.Deserialize(redDetails.GetProperty("initialBoardJson").GetString()!);
        for(var index=0;index<moves.Length;index++)
        {
            var move=moves[index];
            replay=replay.ApplyMove(move.GetProperty("pieceId").GetString()!,
                new(move.GetProperty("toCol").GetInt32(),move.GetProperty("toRow").GetInt32()),out _);
            Assert.Equal(BoardSerializer.Serialize(replay),move.GetProperty("boardSnapshotJson").GetString());
        }
        var replayedBoard=BoardSerializer.Serialize(replay);
        Assert.Equal(replayedBoard,redDetails.GetProperty("currentBoardJson").GetString());
        Assert.Equal(redDetails.GetProperty("currentBoardJson").GetString(),redMoves.GetProperty("currentBoardJson").GetString());
        Assert.Equal("finished",redDetails.GetProperty("status").GetString());
        Assert.Equal("red",redDetails.GetProperty("winner").GetString());
        Assert.Equal("resignation",redDetails.GetProperty("resultReason").GetString());
        Assert.Equal(2,redSnapshot.GetProperty("moveNumber").GetInt32());

        using(var db=Db())
        {
            Assert.Equal(1,await db.Matches.CountAsync(m=>m.Id==id));
            Assert.Equal(2,await db.Moves.CountAsync(m=>m.MatchId==id));
        }

        var closed=await Data(await red.Client.PostAsync($"/api/v1/rooms/{roomId}/close",null));
        Assert.Equal("closed",closed.GetProperty("status").GetString());
        Assert.Empty(closed.GetProperty("members").EnumerateArray());

        Assert.Equal(redDetails.GetRawText(),(await Data(await red.Client.GetAsync($"/api/v1/matches/{id}/history"))).GetRawText());
        Assert.Equal(redMoves.GetRawText(),(await Data(await red.Client.GetAsync($"/api/v1/matches/{id}/moves"))).GetRawText());
        Assert.Equal(redSnapshot.GetRawText(),(await Data(await red.Client.GetAsync($"/api/v1/matches/{id}"))).GetRawText());
        Assert.Equal(blackDetails.GetRawText(),(await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/history"))).GetRawText());
        Assert.Equal(blackMoves.GetRawText(),(await Data(await black.Client.GetAsync($"/api/v1/matches/{id}/moves"))).GetRawText());
        Assert.Equal(blackSnapshot.GetRawText(),(await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"))).GetRawText());
        Assert.Equal(id,Assert.Single((await Data(await red.Client.GetAsync("/api/v1/matches"))).GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(id,Assert.Single((await Data(await black.Client.GetAsync("/api/v1/matches"))).GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        var outsider=await User("history_outsider");
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}/history"),HttpStatusCode.Forbidden);
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}/moves"),HttpStatusCode.Forbidden);
        await Error(await outsider.Client.GetAsync($"/api/v1/matches/{id}"),HttpStatusCode.Forbidden);
        Assert.Empty((await Data(await outsider.Client.GetAsync("/api/v1/matches"))).GetProperty("items").EnumerateArray());

        using var verify=Db();
        Assert.Equal("closed",(await verify.Rooms.AsNoTracking().SingleAsync(r=>r.Id==roomId)).Status);
        Assert.Equal(0,await verify.RoomMemberships.CountAsync(m=>m.RoomId==roomId&&m.LeftAt==null));
        Assert.Equal(1,await verify.Matches.CountAsync(m=>m.Id==id&&m.Status=="finished"&&m.Winner=="red"&&m.ResultReason=="resignation"));
        Assert.Equal(2,await verify.Moves.CountAsync(m=>m.MatchId==id));
    }

    [Fact] public async Task GCT16_surrender_and_explicit_draw_endpoints_enforce_confirmation_and_offer_ownership()
    {
        var (red,black,_,surrenderId)=await Start();
        Assert.Equal("CONFIRMATION_REQUIRED",await Error(
            await black.Client.PostAsJsonAsync($"/api/v1/matches/{surrenderId}/surrender",new {confirmed=false}),
            HttpStatusCode.BadRequest));
        var eventsBeforeCancel=events.Messages.Count(m=>m=="MatchChanged");
        using(var verifyCancel=Db())
        {
            var state=await verifyCancel.Matches.AsNoTracking().SingleAsync(m=>m.Id==surrenderId);
            Assert.Equal("playing",state.Status);
            Assert.Equal(1,state.Version);
            Assert.Null(state.Winner);
            Assert.Null(state.ResultReason);
        }
        Assert.Equal(eventsBeforeCancel,events.Messages.Count(m=>m=="MatchChanged"));
        var surrendered=await Data(await black.Client.PostAsJsonAsync(
            $"/api/v1/matches/{surrenderId}/surrender",new {confirmed=true}));
        Assert.Equal("red",surrendered.GetProperty("winner").GetString());
        Assert.Equal("surrender",surrendered.GetProperty("reasonCode").GetString());
        Assert.Equal("surrender",surrendered.GetProperty("result").GetProperty("reasonCode").GetString());
        Assert.Equal("red",surrendered.GetProperty("result").GetProperty("winner").GetString());
        Assert.Equal("red-win",surrendered.GetProperty("result").GetProperty("outcome").GetString());
        var endedVersion=surrendered.GetProperty("version").GetInt64();
        var redSnapshot=await Data(await red.Client.GetAsync($"/api/v1/matches/{surrenderId}"));
        var blackSnapshot=await Data(await black.Client.GetAsync($"/api/v1/matches/{surrenderId}"));
        Assert.Equal(redSnapshot.GetProperty("result").GetRawText(),blackSnapshot.GetProperty("result").GetRawText());
        var pushed=JsonSerializer.SerializeToElement(
            events.Events.Last(e=>e.Method=="MatchChanged").Args[0],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(surrendered.GetProperty("result").GetRawText(),pushed.GetProperty("result").GetRawText());
        Assert.Equal("surrender",pushed.GetProperty("reasonCode").GetString());
        Assert.Equal("MATCH_NOT_PLAYING", await Error(
            await black.Client.PostAsJsonAsync($"/api/v1/matches/{surrenderId}/moves",Move(endedVersion)),
            HttpStatusCode.Conflict));
        using(var verifyLocked=Db())
        {
            var state=await verifyLocked.Matches.AsNoTracking().SingleAsync(m=>m.Id==surrenderId);
            Assert.Equal(0,state.MoveNumber);
            Assert.Equal("red",state.Winner);
            Assert.Equal("resignation",state.ResultReason);
        }
        var retry=await Data(await black.Client.PostAsJsonAsync(
            $"/api/v1/matches/{surrenderId}/surrender",new {confirmed=true}));
        Assert.Equal(endedVersion,retry.GetProperty("version").GetInt64());

        var (drawRed,drawBlack,_,drawId)=await Start();
        var offered=await Data(await drawRed.Client.PostAsync($"/api/v1/matches/{drawId}/offer-draw",null));
        Assert.Equal("red",offered.GetProperty("drawOfferedBy").GetString());
        var offerId=offered.GetProperty("drawOfferId").GetGuid();
        Assert.Equal(30,(offered.GetProperty("drawExpiresAt").GetDateTimeOffset() -
            offered.GetProperty("drawCreatedAt").GetDateTimeOffset()).TotalSeconds);
        Assert.Equal("DRAW_OFFER_NOT_YOURS",await Error(
            await drawRed.Client.PostAsJsonAsync($"/api/v1/matches/{drawId}/accept-draw",new {offerId}),
            HttpStatusCode.Conflict));
        var rejected=await Data(await drawBlack.Client.PostAsJsonAsync(
            $"/api/v1/matches/{drawId}/reject-draw",new {offerId}));
        Assert.Equal("playing",rejected.GetProperty("status").GetString());
        Assert.True(rejected.GetProperty("drawOfferId").ValueKind==JsonValueKind.Null);

        var nextOffer=await Data(await drawRed.Client.PostAsync($"/api/v1/matches/{drawId}/offer-draw",null));
        var accepted=await Data(await drawBlack.Client.PostAsJsonAsync(
            $"/api/v1/matches/{drawId}/accept-draw",
            new {offerId=nextOffer.GetProperty("drawOfferId").GetGuid()}));
        Assert.Equal("finished",accepted.GetProperty("status").GetString());
        Assert.Null(accepted.GetProperty("winner").GetString());
        Assert.Equal("draw-agreement",accepted.GetProperty("resultReason").GetString());
        Assert.Equal("draw-agreement",accepted.GetProperty("result").GetProperty("reasonCode").GetString());
    }

    [Fact] public async Task GCT1_player_can_start_ai_match_move_legally_surrender_and_view_history()
    {
        var red = await User("ai_player");
        var invalid = await red.Client.PostAsJsonAsync("/api/v1/matches/ai",
            new { requestId = Guid.NewGuid(), difficulty = "expert" });
        Assert.Equal("VALIDATION_ERROR", await Error(invalid, HttpStatusCode.BadRequest));

        var requestId = Guid.NewGuid();
        var createInput = new { requestId, difficulty = "hard" };
        var initial = await Data(await red.Client.PostAsJsonAsync("/api/v1/matches/ai", createInput));
        var duplicate = await Data(await red.Client.PostAsJsonAsync("/api/v1/matches/ai", createInput));
        var matchId = initial.GetProperty("id").GetGuid();
        var roomId = initial.GetProperty("roomId").GetGuid();
        Assert.Equal(matchId, duplicate.GetProperty("id").GetGuid());
        Assert.Equal("pve", initial.GetProperty("mode").GetString());
        Assert.Equal("hard", initial.GetProperty("aiDifficulty").GetString());
        Assert.Equal("red", initial.GetProperty("sideToMove").GetString());
        Assert.Equal("red", Assert.Single(initial.GetProperty("players").EnumerateArray()).GetProperty("side").GetString());

        var room = await Data(await red.Client.GetAsync($"/api/v1/rooms/{roomId}"));
        Assert.Single(room.GetProperty("members").EnumerateArray());
        var lobby = await Data(await red.Client.GetAsync("/api/v1/rooms"));
        Assert.DoesNotContain(lobby.EnumerateArray(), listedRoom => listedRoom.GetProperty("id").GetGuid() == roomId);

        var moveInput = Move(initial.GetProperty("version").GetInt64());
        var aiTimer = System.Diagnostics.Stopwatch.StartNew();
        var moved = await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", moveInput));
        Assert.True(aiTimer.Elapsed <= TimeSpan.FromSeconds(2),
            $"A hard AI move should finish within the 1-2 second demo target; elapsed={aiTimer.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.Equal(2, moved.GetProperty("moveNumber").GetInt32());
        Assert.Equal("red", moved.GetProperty("sideToMove").GetString());
        Assert.Equal("black", moved.GetProperty("lastMove").GetProperty("side").GetString());

        await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", moveInput));
        using (var verify = Db())
        {
            var match = await verify.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
            var moves = await verify.Moves.AsNoTracking().Where(m => m.MatchId == matchId).OrderBy(m => m.Ply).ToListAsync();
            Assert.Equal(2, moves.Count);
            Assert.Equal(new[] { "red", "black" }, moves.Select(m => m.Side));
            var board = BoardSerializer.Deserialize(match.InitialBoardJson);
            foreach (var savedMove in moves)
            {
                var side = savedMove.Side == "red" ? Side.Red : Side.Black;
                var legalMove = new LegalMove(savedMove.PieceId, new Position(savedMove.ToCol, savedMove.ToRow));
                Assert.True(XiangqiRules.IsLegal(board, side, legalMove));
                board = board.ApplyMove(savedMove.PieceId, legalMove.To, out _);
            }
            Assert.Equal(match.CurrentBoardJson, BoardSerializer.Serialize(board));
        }

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<GameService>().RecoverAfterRestart();
        var paused = await Data(await red.Client.GetAsync($"/api/v1/matches/{matchId}"));
        Assert.Equal("paused", paused.GetProperty("status").GetString());
        var resumed = await Data(await red.Client.PostAsync($"/api/v1/matches/{matchId}/reconnect", null));
        Assert.Equal("playing", resumed.GetProperty("status").GetString());
        var resumedRoom = await Data(await red.Client.GetAsync($"/api/v1/rooms/{roomId}"));
        Assert.True(resumedRoom.GetProperty("members").EnumerateArray().Single().GetProperty("connected").GetBoolean());

        var surrender = await Data(await red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matchId}/surrender", new { confirmed = true }));
        Assert.Equal("finished", surrender.GetProperty("status").GetString());
        Assert.Equal("black", surrender.GetProperty("winner").GetString());
        Assert.Equal("surrender", surrender.GetProperty("reasonCode").GetString());
        Assert.Equal("MATCH_NOT_PLAYING", await Error(
            await red.Client.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves",
                Move(surrender.GetProperty("version").GetInt64(), request: Guid.NewGuid())),
            HttpStatusCode.Conflict));
        using (var afterGame = Db())
            Assert.Equal(2, await afterGame.Moves.CountAsync(m => m.MatchId == matchId));

        var history = await Data(await red.Client.GetAsync("/api/v1/matches/history?mode=pve&outcome=loss"));
        var item = Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(matchId, item.GetProperty("id").GetGuid());
        Assert.Equal("hard", item.GetProperty("aiDifficulty").GetString());
        Assert.Equal("Máy", item.GetProperty("opponent").GetProperty("displayName").GetString());
        Assert.Equal("loss", item.GetProperty("outcome").GetString());
        Assert.Equal("surrender", item.GetProperty("reasonCode").GetString());
    }

    [Fact] public async Task GCT72_surrender_while_ai_is_thinking_never_applies_a_stale_move()
    {
        var red = await User("ai_cancel");
        var initial = await Data(await red.Client.PostAsJsonAsync("/api/v1/matches/ai",
            new { requestId = Guid.NewGuid(), difficulty = "hard" }));
        var matchId = initial.GetProperty("id").GetGuid();
        var moveTask = red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matchId}/moves", Move(initial.GetProperty("version").GetInt64()));

        var moveDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < moveDeadline)
        {
            using var progress = Db();
            if (await progress.Moves.AnyAsync(m => m.MatchId == matchId)) break;
            await Task.Delay(5);
        }
        using (var verifyPlayerMove = Db())
            Assert.True(await verifyPlayerMove.Moves.AnyAsync(m => m.MatchId == matchId),
                "The player's move should be persisted before surrender is requested.");

        var surrender = await Data(await red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matchId}/surrender", new { confirmed = true }));
        var moveResponse = await moveTask;
        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);
        var moveResult = await Data(moveResponse);
        Assert.Equal("finished", surrender.GetProperty("status").GetString());
        Assert.Equal("finished", moveResult.GetProperty("status").GetString());

        using var verify = Db();
        var finalMatch = await verify.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        var moves = await verify.Moves.AsNoTracking().Where(m => m.MatchId == matchId).OrderBy(m => m.Ply).ToListAsync();
        Assert.Equal("finished", finalMatch.Status);
        Assert.Equal("black", finalMatch.Winner);
        Assert.InRange(moves.Count, 1, 2);
        if (moves.Count == 2)
        {
            Assert.Equal("black", moves[1].Side);
            Assert.NotNull(finalMatch.EndedAt);
            Assert.True(moves[1].CreatedAt <= finalMatch.EndedAt.Value);
        }
    }

    [Fact] public async Task GCT90_player_and_machine_share_match_flow_and_history()
    {
        var red = await User("ai_flow");
        var initial = await Data(await red.Client.PostAsJsonAsync("/api/v1/matches/ai",
            new { requestId = Guid.NewGuid(), difficulty = "medium" }));
        var matchId = initial.GetProperty("id").GetGuid();
        Assert.Equal("pve", initial.GetProperty("mode").GetString());
        Assert.Equal("red", initial.GetProperty("playerSide").GetString());
        Assert.Equal("red", initial.GetProperty("sideToMove").GetString());
        Assert.Equal("medium", initial.GetProperty("aiDifficulty").GetString());

        var afterTurn = await Data(await red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matchId}/moves", Move(initial.GetProperty("version").GetInt64())));
        Assert.Equal(2, afterTurn.GetProperty("moveNumber").GetInt32());
        Assert.Equal("red", afterTurn.GetProperty("sideToMove").GetString());
        Assert.Equal("black", afterTurn.GetProperty("lastMove").GetProperty("side").GetString());

        var finished = await Data(await red.Client.PostAsJsonAsync(
            $"/api/v1/matches/{matchId}/surrender", new { confirmed = true }));
        Assert.Equal("finished", finished.GetProperty("status").GetString());

        var history = await Data(await red.Client.GetAsync($"/api/v1/matches/{matchId}/history"));
        Assert.Equal("pve", history.GetProperty("mode").GetString());
        Assert.Equal("finished", history.GetProperty("status").GetString());
        Assert.Equal("surrender", history.GetProperty("reasonCode").GetString());
        Assert.Equal("Máy", history.GetProperty("opponent").GetProperty("displayName").GetString());
        var moves = history.GetProperty("moves").EnumerateArray().ToArray();
        Assert.Equal(new[] { "red", "black" }, moves.Select(move => move.GetProperty("side").GetString()));
        Assert.Equal(history.GetProperty("currentBoardJson").GetString(),
            moves[^1].GetProperty("boardSnapshotJson").GetString());
        Assert.Equal("MATCH_NOT_PLAYING", await Error(
            await red.Client.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves",
                Move(finished.GetProperty("version").GetInt64(), request: Guid.NewGuid())),
            HttpStatusCode.Conflict));
    }

    public void Dispose() {foreach(var c in clients)c.Dispose();factory.Dispose();root.Dispose();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}

    private sealed class CaptureLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages {get;}=new();
        public ILogger CreateLogger(string categoryName)=>new Logger(Messages);
        public void Dispose() { }
        private sealed class Logger(ConcurrentQueue<string> messages):ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>level!=LogLevel.None;
            public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>messages.Enqueue(formatter(state,exception));
        }
    }
    private sealed class EventRecorder:IHubContext<GameHub>,IHubClients,IGroupManager,IClientProxy
    {
        public ConcurrentQueue<string> Messages {get;}=new();
        public ConcurrentQueue<(string Method,object?[] Args)> Events {get;}=new();
        IHubClients IHubContext<GameHub>.Clients=>this;IGroupManager IHubContext<GameHub>.Groups=>this;public IClientProxy All=>this;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)=>this;
        public IClientProxy Client(string connectionId)=>this;public IClientProxy Clients(IReadOnlyList<string> connectionIds)=>this;
        public IClientProxy Group(string groupName)=>this;public IClientProxy GroupExcept(string groupName,IReadOnlyList<string> excludedConnectionIds)=>this;
        public IClientProxy Groups(IReadOnlyList<string> groupNames)=>this;public IClientProxy User(string userId)=>this;public IClientProxy Users(IReadOnlyList<string> userIds)=>this;
        public Task AddToGroupAsync(string connectionId,string groupName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId,string groupName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task SendCoreAsync(string method,object?[] args,CancellationToken cancellationToken=default){Messages.Enqueue(method);Events.Enqueue((method,args));return Task.CompletedTask;}
    }
}
