using GameCoTuong.Api.Hubs;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Application.Game;
using GameCoTuong.Infrastructure.Game;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Endpoints;

public static class PveEndpoints
{
    public static IEndpointRouteBuilder MapPveEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (var prefix in new[] { "/api/v1/pve", "/api/pve" })
        {
            var g = app.MapGroup(prefix);

            // POST /api/v1/pve/matches — create a new PvE match
            g.MapPost("/matches", async (PveCreateInput input, HttpContext context, BotGameService svc) =>
            {
                var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
                if (!current.IsAuthenticated || !current.UserId.HasValue)
                    return Results.Json(new { error = new { code = "UNAUTHORIZED", message = "Bạn cần đăng nhập." } }, statusCode: 401);
                var difficulty = input.Difficulty?.ToLowerInvariant() ?? "medium";
                if (difficulty is not ("easy" or "medium" or "hard"))
                    return Results.Json(new { error = new { code = "VALIDATION_ERROR", message = "Độ khó không hợp lệ. Chọn: easy, medium, hard." } }, statusCode: 400);
                var requestId = input.RequestId == Guid.Empty ? Guid.NewGuid() : input.RequestId;
                try
                {
                    var result = await svc.CreatePveMatch(current.UserId.Value, difficulty, requestId);
                    return Results.Ok(new { data = result });
                }
                catch (GameFault ex) { return Results.Json(new { error = new { ex.Code, message = ex.Message } }, statusCode: ex.Status); }
            });

            // GET /api/v1/pve/matches/{id} — fetch match snapshot
            g.MapGet("/matches/{id:guid}", async (Guid id, HttpContext context, BotGameService svc) =>
            {
                var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
                if (!current.IsAuthenticated || !current.UserId.HasValue)
                    return Results.Json(new { error = new { code = "UNAUTHORIZED", message = "Bạn cần đăng nhập." } }, statusCode: 401);
                try
                {
                    var result = await svc.GetPveMatch(id, current.UserId.Value);
                    return Results.Ok(new { data = result });
                }
                catch (GameFault ex) { return Results.Json(new { error = new { ex.Code, message = ex.Message } }, statusCode: ex.Status); }
            });

            // POST /api/v1/pve/matches/{id}/moves — human move (response immediate; bot moves via SignalR)
            g.MapPost("/matches/{id:guid}/moves", async (Guid id, PveMoveInput input, HttpContext context, BotGameService svc,
                IHubContext<GameHub> hub) =>
            {
                var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
                if (!current.IsAuthenticated || !current.UserId.HasValue)
                    return Results.Json(new { error = new { code = "UNAUTHORIZED", message = "Bạn cần đăng nhập." } }, statusCode: 401);
                var userId = current.UserId.Value;
                var mi = new MoveInput(input.RequestId, input.ExpectedVersion, input.PieceId, input.ToCol, input.ToRow);
                try
                {
                    var snapshot = await svc.HumanMove(id, userId, mi);
                    var difficulty = input.Difficulty ?? "medium";
                    // Bot move runs in background; client receives result via PvEMatchChanged SignalR event
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(150);
                        try
                        {
                            var botSnapshot = await svc.BotMove(id, difficulty);
                            if (botSnapshot is not null)
                                await hub.Clients.User(userId.ToString()).SendAsync("PvEMatchChanged", botSnapshot);
                        }
                        catch { /* bot move failures are non-fatal; client can poll via GET */ }
                    });
                    return Results.Ok(new { data = snapshot });
                }
                catch (GameFault ex) { return Results.Json(new { error = new { ex.Code, message = ex.Message } }, statusCode: ex.Status); }
            });

            // POST /api/v1/pve/matches/{id}/surrender — human surrenders
            g.MapPost("/matches/{id:guid}/surrender", async (Guid id, HttpContext context, BotGameService svc) =>
            {
                var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
                if (!current.IsAuthenticated || !current.UserId.HasValue)
                    return Results.Json(new { error = new { code = "UNAUTHORIZED", message = "Bạn cần đăng nhập." } }, statusCode: 401);
                try
                {
                    var result = await svc.HumanSurrender(id, current.UserId.Value);
                    return Results.Ok(new { data = result });
                }
                catch (GameFault ex) { return Results.Json(new { error = new { ex.Code, message = ex.Message } }, statusCode: ex.Status); }
            });
        }
        return app;
    }
}

public sealed record PveCreateInput(Guid RequestId, string? Difficulty = "medium");
public sealed record PveMoveInput(Guid RequestId, long ExpectedVersion, string PieceId, int ToCol, int ToRow, string? Difficulty = "medium");
