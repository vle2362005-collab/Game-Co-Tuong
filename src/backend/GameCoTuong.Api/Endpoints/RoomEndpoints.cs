using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;

namespace GameCoTuong.Api.Endpoints;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (var prefix in new[] { "/api/v1/rooms", "/api/rooms" })
        {
            var g = app.MapGroup(prefix);
            g.MapGet("", (HttpContext c, GameService s) => GameApi.Run(c, async _ => await s.List()));
            g.MapPost("", (CreateRoomInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Create(u, input), c)));
            g.MapPost("/join/{code}", (string code, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Join(u, code), c)));
            g.MapGet("/{id:guid}", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await s.Snapshot(id, u)));
            g.MapPost("/{id:guid}/ready", (Guid id, ReadyInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Ready(id, u, input.Ready), c)));
            g.MapPost("/{id:guid}/start", (Guid id, CommandInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Start(id, u, input), c)));
            g.MapPost("/{id:guid}/leave", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Leave(id, u, false), c)));
            g.MapPost("/{id:guid}/close", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await GameApi.Publish(await s.Leave(id, u, true), c)));
            g.MapGet("/{id:guid}/messages", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await s.Messages(id, u)));
        }
        return app;
    }
}
