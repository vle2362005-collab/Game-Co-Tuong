using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Common.Interfaces;

namespace GameCoTuong.Api.Endpoints;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoints(app.MapGroup("/api/v1/rooms"));
        RegisterEndpoints(app.MapGroup("/api/rooms"));

        return app;
    }

    private static void RegisterEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{roomId:guid}/start", HandleStartRoomAsync);
        group.MapPost("/{roomId:guid}/close", HandleCloseRoomAsync);
        group.MapPost("/{roomId:guid}/video/signal", HandleVideoSignalAsync);
    }

    public static async Task<IResult> HandleStartRoomAsync(
        Guid roomId,
        StartRoomRequest? request,
        HttpContext context,
        ICurrentUserService currentUserService,
        IRoomAuthorizationService roomAuthService,
        CancellationToken cancellationToken)
    {
        var reqId = AuthEndpoints.ExtractRequestId(context);
        var serverTime = DateTimeOffset.UtcNow;

        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Yêu cầu xác thực không hợp lệ hoặc thiếu Bearer token.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        // Anti-spoofing: Always bind to authenticated user identity; ignore any client-supplied spoofedUserId
        var currentUserId = currentUserService.UserId.Value;

        var authResult = await roomAuthService.AuthorizeRoomHostAsync(roomId, currentUserId, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    authResult.ErrorCode ?? "FORBIDDEN",
                    authResult.ErrorMessage ?? "Bạn không có quyền bắt đầu trận đấu trong phòng này.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(new ApiSuccessResponse<AckDto>(
            reqId,
            serverTime,
            new AckDto(true)
        ));
    }

    public static async Task<IResult> HandleCloseRoomAsync(
        Guid roomId,
        CloseRoomRequest? request,
        HttpContext context,
        ICurrentUserService currentUserService,
        IRoomAuthorizationService roomAuthService,
        CancellationToken cancellationToken)
    {
        var reqId = AuthEndpoints.ExtractRequestId(context);
        var serverTime = DateTimeOffset.UtcNow;

        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Yêu cầu xác thực không hợp lệ hoặc thiếu Bearer token.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var currentUserId = currentUserService.UserId.Value;

        var authResult = await roomAuthService.AuthorizeRoomHostAsync(roomId, currentUserId, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    authResult.ErrorCode ?? "FORBIDDEN",
                    authResult.ErrorMessage ?? "Bạn không có quyền đóng phòng này.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(new ApiSuccessResponse<AckDto>(
            reqId,
            serverTime,
            new AckDto(true)
        ));
    }

    public static async Task<IResult> HandleVideoSignalAsync(
        Guid roomId,
        VideoSignalRequest? request,
        HttpContext context,
        ICurrentUserService currentUserService,
        IRoomAuthorizationService roomAuthService,
        CancellationToken cancellationToken)
    {
        var reqId = AuthEndpoints.ExtractRequestId(context);
        var serverTime = DateTimeOffset.UtcNow;

        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Yêu cầu xác thực không hợp lệ hoặc thiếu Bearer token.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var currentUserId = currentUserService.UserId.Value;

        var authResult = await roomAuthService.AuthorizeVideoCallAsync(roomId, currentUserId, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    authResult.ErrorCode ?? "FORBIDDEN",
                    authResult.ErrorMessage ?? "Chỉ thành viên trong phòng mới được phép tham gia hoặc gửi tín hiệu video.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(new ApiSuccessResponse<AckDto>(
            reqId,
            serverTime,
            new AckDto(true)
        ));
    }
}
