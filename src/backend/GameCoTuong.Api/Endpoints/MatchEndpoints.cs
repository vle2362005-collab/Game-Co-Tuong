using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;
using GameCoTuong.Infrastructure.Persistence;
using GameCoTuong.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (var prefix in new[] { "/api/v1/matches", "/api/matches" })
        {
            var g = app.MapGroup(prefix);
            g.MapGet("/{id:guid}", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, u => s.MatchSnapshot(id, u)));
            g.MapGet("/{id:guid}/moves", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, u => s.History(id, u)));
            g.MapPost("/{id:guid}/moves", (Guid id, MoveInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.Move(id, u, input), c)));
            g.MapPost("/{id:guid}/resign", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.End(id, u, false), c)));
            g.MapPost("/{id:guid}/draw", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.End(id, u, true), c)));
        }
        return app;
    }
    private static async Task<object> Publish(Guid id, object snapshot, HttpContext context)
    {
        var db = context.RequestServices.GetRequiredService<GameDbContext>();
        var roomId = await db.Matches.Where(m => m.Id == id).Select(m => m.RoomId).SingleAsync();
        await context.RequestServices.GetRequiredService<IHubContext<GameHub>>().Clients.Group(GameHub.GetRoomGroupName(roomId)).SendAsync("MatchChanged", snapshot);
        return snapshot;
    }
}
