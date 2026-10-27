using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Common.Interfaces;

namespace GameCoTuong.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoints(app.MapGroup("/api/v1/matches"));
        RegisterEndpoints(app.MapGroup("/api/matches"));

        return app;
    }

    private static void RegisterEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{matchId:guid}/moves", HandleSubmitMoveAsync);
    }

    public static async Task<IResult> HandleSubmitMoveAsync(
        Guid matchId,
        SubmitMoveRequest? request,
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

        // Anti-spoofing: Take identity strictly from token, ignore any client-supplied spoofedUserId
        var currentUserId = currentUserService.UserId.Value;

        var authResult = await roomAuthService.AuthorizeMatchTurnAsync(matchId, currentUserId, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    authResult.ErrorCode ?? "FORBIDDEN",
                    authResult.ErrorMessage ?? "Bạn không có quyền thực hiện nước đi này.",
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
