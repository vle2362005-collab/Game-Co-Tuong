using System.Diagnostics;

namespace GameCoTuong.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string RequestIdItemKey = "GameCoTuong.RequestId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = GetRequestId(context);
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Response.Headers["X-Trace-Id"] = traceId;

        try
        {
            await next(context);
        }
        finally
        {
            var roomId = RouteId(context, "rooms");
            var matchId = RouteId(context, "matches");
            var statusCode = context.Response.StatusCode;
            var errorCode = context.Items.TryGetValue(GameApiErrorItemKey, out var value) ? value?.ToString() : null;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    "HTTP request completed with server error. {TraceId} {RequestId} {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                    traceId, requestId, roomId, matchId, errorCode ?? "INTERNAL_SERVER_ERROR", statusCode);
            }
            else if (statusCode >= StatusCodes.Status400BadRequest)
            {
                logger.LogWarning(
                    "HTTP request completed with client error. {TraceId} {RequestId} {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                    traceId, requestId, roomId, matchId, errorCode, statusCode);
            }
            else
            {
                logger.LogInformation(
                    "HTTP request completed. {TraceId} {RequestId} {RoomId} {MatchId} {StatusCode}",
                    traceId, requestId, roomId, matchId, statusCode);
            }
        }
    }

    public static Guid GetRequestId(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestIdItemKey, out var item) && item is Guid existing)
            return existing;

        var requestId = context.Request.Headers.TryGetValue("X-Request-ID", out var header) &&
                        Guid.TryParse(header.ToString(), out var parsed)
            ? parsed
            : Guid.NewGuid();
        context.Items[RequestIdItemKey] = requestId;
        return requestId;
    }

    private static Guid? RouteId(HttpContext context, string routeSegment)
    {
        if (!(context.Request.Path.Value?.Contains($"/{routeSegment}/", StringComparison.OrdinalIgnoreCase) ?? false))
            return null;

        return Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var id) ? id : null;
    }

    private const string GameApiErrorItemKey = "GameCoTuong.ErrorCode";
}
