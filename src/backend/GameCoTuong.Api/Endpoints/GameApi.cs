using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Application.Game;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Api.Middleware;
using Microsoft.AspNetCore.SignalR;

namespace GameCoTuong.Api.Endpoints;

public static class GameApi
{
    public static async Task<IResult> Run(HttpContext context, Func<Guid, Task<object>> action)
    {
        var current = context.RequestServices.GetRequiredService<ICurrentUserService>();
        var requestId = AuthEndpoints.ExtractRequestId(context);
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GameCoTuong.Api.GameApi");
        if (!current.IsAuthenticated || !current.UserId.HasValue)
        {
            LogFailure(context, logger, requestId, "UNAUTHORIZED", StatusCodes.Status401Unauthorized);
            return Error(requestId, "UNAUTHORIZED", "Bạn cần đăng nhập.", 401);
        }
        try { return Results.Ok(new ApiSuccessResponse<object>(requestId, DateTimeOffset.UtcNow, await action(current.UserId.Value))); }
        catch (GameFault ex)
        {
            LogFailure(context, logger, requestId, ex.Code, ex.Status);
            var isInternalError = ex.Status >= StatusCodes.Status500InternalServerError;
            var message = isInternalError
                ? "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau."
                : ex.Message;
            return Error(requestId, ex.Code, message, ex.Status, isInternalError);
        }
    }
    private static void LogFailure(HttpContext context, ILogger logger, Guid requestId, string errorCode, int statusCode)
    {
        context.Items["GameCoTuong.ErrorCode"] = errorCode;
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        Guid? roomId = context.Request.Path.Value?.Contains("/rooms/", StringComparison.OrdinalIgnoreCase) == true &&
                       Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var parsedRoomId)
            ? parsedRoomId
            : null;
        Guid? matchId = context.Request.Path.Value?.Contains("/matches/", StringComparison.OrdinalIgnoreCase) == true &&
                        Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var parsedMatchId)
            ? parsedMatchId
            : null;

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError("Game request failed. {TraceId} {RequestId} {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                traceId, requestId, roomId, matchId, errorCode, statusCode);
        else
            logger.LogWarning("Game request rejected. {TraceId} {RequestId} {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                traceId, requestId, roomId, matchId, errorCode, statusCode);
    }
    private static IResult Error(Guid id, string code, string message, int status, bool retryable = false) => Results.Json(
        new ApiErrorEnvelope(id, DateTimeOffset.UtcNow, new ApiErrorDetail(code, message, retryable, null, null, new Dictionary<string, string[]>())), statusCode: status);

    public static async Task<object> Publish(RoomView room, HttpContext context)
    {
        var hub = context.RequestServices.GetRequiredService<IHubContext<GameHub>>();
        await hub.Clients.Group(GameHub.GetRoomGroupName(room.Id)).SendAsync("RoomStateChanged", room);
        await hub.Clients.Group(GameHub.LobbyGroupName).SendAsync("LobbyChanged");
        await context.RequestServices.GetRequiredService<LiveConnections>().EvictInactive(room, hub);
        return room;
    }
}
