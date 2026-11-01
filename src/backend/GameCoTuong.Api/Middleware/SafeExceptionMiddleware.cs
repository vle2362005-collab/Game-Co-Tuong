using System.Diagnostics;
using System.Text.Json;
using GameCoTuong.Api.Endpoints;

namespace GameCoTuong.Api.Middleware;

public sealed class SafeExceptionMiddleware(RequestDelegate next, ILogger<SafeExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var requestId = RequestLoggingMiddleware.GetRequestId(context);
            var roomId = RouteId(context, "rooms");
            var matchId = RouteId(context, "matches");
            var sanitizedDetails = LogSanitizer.SanitizeMessage(exception.ToString());

            logger.LogError(
                "Unhandled request exception. {TraceId} {RequestId} {RoomId} {MatchId} {ErrorCode} {StatusCode} {ExceptionType} {ExceptionDetails}",
                traceId, requestId, roomId, matchId, "INTERNAL_SERVER_ERROR",
                StatusCodes.Status500InternalServerError, exception.GetType().FullName, sanitizedDetails);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Trace-Id"] = traceId;
            context.Items["GameCoTuong.ErrorCode"] = "INTERNAL_SERVER_ERROR";
            var response = new ApiErrorEnvelope(
                requestId,
                DateTimeOffset.UtcNow,
                new ApiErrorDetail(
                    "INTERNAL_SERVER_ERROR",
                    "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.",
                    true,
                    null,
                    null,
                    new Dictionary<string, string[]>()));
            await context.Response.WriteAsync(JsonSerializer.Serialize(response), context.RequestAborted);
        }
    }

    private static Guid? RouteId(HttpContext context, string routeSegment)
    {
        if (!(context.Request.Path.Value?.Contains($"/{routeSegment}/", StringComparison.OrdinalIgnoreCase) ?? false))
            return null;

        return Guid.TryParse(context.Request.RouteValues["id"]?.ToString(), out var id) ? id : null;
    }
}
