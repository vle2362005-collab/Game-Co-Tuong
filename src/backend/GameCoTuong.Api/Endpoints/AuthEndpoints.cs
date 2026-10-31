using System.Text.Json.Serialization;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Endpoints;

public sealed record ApiSuccessResponse<T>(
    [property: JsonPropertyName("requestId")] Guid RequestId,
    [property: JsonPropertyName("serverTime")] DateTimeOffset ServerTime,
    [property: JsonPropertyName("data")] T Data
);

public sealed record ApiErrorDetail(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("retryable")] bool Retryable,
    [property: JsonPropertyName("currentVersion")] long? CurrentVersion,
    [property: JsonPropertyName("resyncUrl")] string? ResyncUrl,
    [property: JsonPropertyName("fieldErrors")] Dictionary<string, string[]> FieldErrors
);

public sealed record ApiErrorEnvelope(
    [property: JsonPropertyName("requestId")] Guid RequestId,
    [property: JsonPropertyName("serverTime")] DateTimeOffset ServerTime,
    [property: JsonPropertyName("error")] ApiErrorDetail Error
);

public sealed record AckDto(
    [property: JsonPropertyName("acknowledged")] bool Acknowledged
);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoints(app.MapGroup("/api/v1/auth"));
        RegisterEndpoints(app.MapGroup("/api/auth"));

        return app;
    }

    private static void RegisterEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/register", HandleRegisterAsync);
        group.MapPost("/login", HandleLoginAsync);
        group.MapPost("/logout", HandleLogoutAsync);
    }

    public static async Task<IResult> HandleRegisterAsync(
        RegisterRequest? request,
        IRegistrationService registrationService,
        CancellationToken cancellationToken)
    {
        var reqId = request?.RequestId ?? Guid.Empty;
        var serverTime = DateTimeOffset.UtcNow;

        if (request is null)
        {
            return Results.BadRequest(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "VALIDATION_ERROR",
                    "Dữ liệu yêu cầu không được để trống.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>
                    {
                        ["request"] = ["Dữ liệu yêu cầu không được để trống."]
                    }
                )
            ));
        }

        var result = await registrationService.RegisterAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Created($"/api/v1/users/{result.User!.Id}", new ApiSuccessResponse<UserPublicDto>(
                request.RequestId,
                DateTimeOffset.UtcNow,
                result.User
            ));
        }

        var statusCode = result.ErrorCode switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "USERNAME_TAKEN" or "EMAIL_TAKEN" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(new ApiErrorEnvelope(
            request.RequestId,
            DateTimeOffset.UtcNow,
            new ApiErrorDetail(
                result.ErrorCode ?? "UNKNOWN_ERROR",
                result.ErrorMessage ?? "Đã xảy ra lỗi.",
                false,
                null,
                null,
                result.FieldErrors ?? new Dictionary<string, string[]>()
            )
        ), statusCode: statusCode);
    }

    public static async Task<IResult> HandleLoginAsync(
        LoginRequest? request,
        ILoginService loginService,
        CancellationToken cancellationToken)
    {
        var reqId = request?.RequestId ?? Guid.Empty;
        var serverTime = DateTimeOffset.UtcNow;

        if (request is null)
        {
            return Results.BadRequest(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "VALIDATION_ERROR",
                    "Dữ liệu yêu cầu đăng nhập không được để trống.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>
                    {
                        ["request"] = ["Dữ liệu yêu cầu đăng nhập không được để trống."]
                    }
                )
            ));
        }

        var result = await loginService.LoginAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(new ApiSuccessResponse<AuthSessionDto>(
                request.RequestId,
                DateTimeOffset.UtcNow,
                result.Session!
            ));
        }

        var statusCode = result.ErrorCode switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "INVALID_CREDENTIALS" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(new ApiErrorEnvelope(
            request.RequestId,
            DateTimeOffset.UtcNow,
            new ApiErrorDetail(
                result.ErrorCode ?? "INVALID_CREDENTIALS",
                result.ErrorMessage ?? "Tên đăng nhập hoặc mật khẩu không chính xác.",
                false,
                null,
                null,
                result.FieldErrors ?? new Dictionary<string, string[]>()
            )
        ), statusCode: statusCode);
    }

    public static Task<IResult> HandleLogoutAsync(HttpContext context, GameCoTuong.Infrastructure.Game.GameService game)
    {
        return GameApi.Run(context, async userId =>
        {
            var jti = context.User.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti)) throw new GameCoTuong.Application.Game.GameFault("UNAUTHORIZED", "Phiên không hợp lệ.", 401);
            var exp = context.User.FindFirst("exp")?.Value;
            var expires = long.TryParse(exp, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : DateTimeOffset.UtcNow.AddDays(1);
            var rooms = await game.Logout(userId, jti, expires);
            var hub = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<GameCoTuong.Api.Hubs.GameHub>>();
            foreach (var room in rooms)
            {
                context.RequestServices.GetRequiredService<GameCoTuong.Api.Hubs.VideoCalls>().End(room.Id, userId);
                await Microsoft.AspNetCore.SignalR.ClientProxyExtensions.SendAsync(hub.Clients.Group(GameCoTuong.Api.Hubs.GameHub.GetRoomGroupName(room.Id)), "CallEnded");
                if (room.MatchId is Guid matchId)
                    await Microsoft.AspNetCore.SignalR.ClientProxyExtensions.SendAsync(hub.Clients.Group(GameCoTuong.Api.Hubs.GameHub.GetRoomGroupName(room.Id)), "MatchChanged", await game.MatchSnapshot(matchId, userId));
                await GameApi.Publish(room, context);
            }
            return new LogoutResponseDto(true, true, "JWT đã được thu hồi; phiên cũ không tiếp tục sử dụng được.");
        });
    }

    public static Guid ExtractRequestId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Request-ID", out var headerVal) &&
            Guid.TryParse(headerVal.ToString(), out var guid))
        {
            return guid;
        }
        return Guid.NewGuid();
    }
}
