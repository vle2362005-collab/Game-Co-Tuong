using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
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
        var moved=await Data(await red.Client.PostAsJsonAsync($"/api/v1/matches/{id}/moves",Move(1,"red-cannon-1",1,0)));
        Assert.Equal(31,moved.GetProperty("pieces").GetArrayLength());Assert.Equal(2,moved.GetProperty("version").GetInt64());Assert.Equal("black",moved.GetProperty("sideToMove").GetString());
        Assert.Equal("black-horse-1",moved.GetProperty("lastMove").GetProperty("capturedPieceId").GetString());
        var opponent=await Data(await black.Client.GetAsync($"/api/v1/matches/{id}"));Assert.Equal(moved.GetRawText(),opponent.GetRawText());
        var offered=await Data(await black.Client.PostAsync($"/api/v1/matches/{id}/draw",null));Assert.Equal(3,offered.GetProperty("version").GetInt64());
        var repeated=await Data(await black.Client.PostAsync($"/api/v1/matches/{id}/draw",null));Assert.Equal(3,repeated.GetProperty("version").GetInt64());
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
        IHubClients IHubContext<GameHub>.Clients=>this;IGroupManager IHubContext<GameHub>.Groups=>this;public IClientProxy All=>this;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)=>this;
        public IClientProxy Client(string connectionId)=>this;public IClientProxy Clients(IReadOnlyList<string> connectionIds)=>this;
        public IClientProxy Group(string groupName)=>this;public IClientProxy GroupExcept(string groupName,IReadOnlyList<string> excludedConnectionIds)=>this;
        public IClientProxy Groups(IReadOnlyList<string> groupNames)=>this;public IClientProxy User(string userId)=>this;public IClientProxy Users(IReadOnlyList<string> userIds)=>this;
        public Task AddToGroupAsync(string connectionId,string groupName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId,string groupName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task SendCoreAsync(string method,object?[] args,CancellationToken cancellationToken=default){Messages.Enqueue(method);return Task.CompletedTask;}
    }
}
