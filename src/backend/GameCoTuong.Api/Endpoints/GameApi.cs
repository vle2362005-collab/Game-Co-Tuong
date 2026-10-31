using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Application.Game;
using GameCoTuong.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Endpoints;

public static class GameApi
{
    public static async Task<IResult> Run(HttpContext context, Func<Guid, Task<object>> action)
    {
        var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
        var requestId = AuthEndpoints.ExtractRequestId(context);
        if (!current.IsAuthenticated || !current.UserId.HasValue) return Error(requestId, "UNAUTHORIZED", "Bạn cần đăng nhập.", 401);
        try { return Results.Ok(new ApiSuccessResponse<object>(requestId, DateTimeOffset.UtcNow, await action(current.UserId.Value))); }
        catch (GameFault ex) { return Error(requestId, ex.Code, ex.Message, ex.Status); }
    }
    private static IResult Error(Guid id, string code, string message, int status) => Results.Json(
        new ApiErrorEnvelope(id, DateTimeOffset.UtcNow, new ApiErrorDetail(code, message, false, null, null, new Dictionary<string, string[]>())), statusCode: status);

    public static async Task<object> Publish(RoomView room, HttpContext context)
    {
        var hub = context.RequestServices.GetRequiredService<IHubContext<GameHub>>();
        await hub.Clients.Group(GameHub.GetRoomGroupName(room.Id)).SendAsync("RoomStateChanged", room);
        await hub.Clients.Group(GameHub.LobbyGroupName).SendAsync("LobbyChanged");
        await context.RequestServices.GetRequiredService<LiveConnections>().EvictInactive(room, hub);
        return room;
    }
}
