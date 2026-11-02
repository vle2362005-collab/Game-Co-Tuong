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
            g.MapPost("/ai", (CreateAiMatchInput input, HttpContext c, GameService s) =>
                GameApi.Run(c, u => s.CreateAiMatch(u, input)));
            g.MapGet("", (HttpContext c, GameService s, int page = 1, int pageSize = 20, string? outcome = null, string? mode = null) =>
                GameApi.Run(c, u => s.MatchHistory(u, page, pageSize, outcome, mode)));
            g.MapGet("/history", (HttpContext c, GameService s, int page = 1, int pageSize = 20, string? outcome = null, string? mode = null) =>
                GameApi.Run(c, u => s.MatchHistory(u, page, pageSize, outcome, mode)));
            g.MapGet("/{id:guid}", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, u => s.MatchSnapshot(id, u)));
            g.MapGet("/{id:guid}/legal-moves", (Guid id, string? pieceId, HttpContext c, GameService s) =>
                GameApi.Run(c, async u => (object)await s.LegalMoves(id, u, pieceId ?? string.Empty)));
            g.MapGet("/{id:guid}/history", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, u => s.MatchDetails(id, u)));
            g.MapGet("/{id:guid}/moves", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, u => s.History(id, u)));
            g.MapPost("/{id:guid}/reconnect", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.Reconnect(id, u), c, u)));
            g.MapPost("/{id:guid}/moves", (Guid id, MoveInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.Move(id, u, input), c)));
            g.MapPost("/{id:guid}/surrender", (Guid id, SurrenderInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.Surrender(id, u, input.Confirmed), c)));
            g.MapPost("/{id:guid}/resign", (Guid id, SurrenderInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.Surrender(id, u, input.Confirmed), c)));
            g.MapPost("/{id:guid}/offer-draw", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.OfferDraw(id, u), c)));
            g.MapPost("/{id:guid}/accept-draw", (Guid id, DrawOfferResponseInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.AcceptDraw(id, u, input.OfferId), c)));
            g.MapPost("/{id:guid}/reject-draw", (Guid id, DrawOfferResponseInput input, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.RejectDraw(id, u, input.OfferId), c)));
            g.MapPost("/{id:guid}/draw", (Guid id, HttpContext c, GameService s) => GameApi.Run(c, async u => await Publish(id, await s.OfferDraw(id, u), c)));
        }
        return app;
    }
    private static async Task<object> Publish(Guid id, object snapshot, HttpContext context, Guid? reconnectedUser = null)
    {
        var db = context.RequestServices.GetRequiredService<GameDbContext>();
        var roomId = await db.Matches.Where(m => m.Id == id).Select(m => m.RoomId).SingleAsync();
        var clients = context.RequestServices.GetRequiredService<IHubContext<GameHub>>().Clients;
        var state = reconnectedUser is Guid stateUserId
            ? await context.RequestServices.GetRequiredService<GameService>().MatchStateSnapshot(id, stateUserId)
            : snapshot;
        await clients.Group(GameHub.GetRoomGroupName(roomId)).SendAsync("MatchChanged", state);
        if (reconnectedUser is Guid userId)
            await clients.User(userId.ToString()).SendAsync("Reconnected", snapshot);
        return snapshot;
    }
}
