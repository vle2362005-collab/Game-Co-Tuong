using System.Security.Claims;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterEndpoints(app.MapGroup("/api/v1/users"));
        RegisterEndpoints(app.MapGroup("/api/users"));

        return app;
    }

    private static void RegisterEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/me", HandleGetMeAsync);
    }

    public static async Task<IResult> HandleGetMeAsync(
        HttpContext context,
        ITokenService tokenService,
        GameDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var reqId = AuthEndpoints.ExtractRequestId(context);
        var serverTime = DateTimeOffset.UtcNow;

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var principal = tokenService.ValidateToken(token);
        if (principal is null)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Phiên đăng nhập không hợp lệ, đã bị sửa đổi hoặc đã hết hạn.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var jtiClaim = principal.FindFirst("jti")?.Value;
        if (!string.IsNullOrEmpty(jtiClaim))
        {
            var isRevoked = await dbContext.RevokedTokens
                .AsNoTracking()
                .AnyAsync(r => r.Jti == jtiClaim, cancellationToken);

            if (isRevoked)
            {
                return Results.Json(new ApiErrorEnvelope(
                    reqId,
                    serverTime,
                    new ApiErrorDetail(
                        "UNAUTHORIZED",
                        "Phiên đăng nhập đã bị thu hồi do đăng xuất.",
                        false,
                        null,
                        null,
                        new Dictionary<string, string[]>()
                    )
                ), statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subClaim, out var userId))
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Danh tính người dùng trong token không hợp lệ.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                serverTime,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Tài khoản người dùng không còn tồn tại trong hệ thống.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var userDto = new UserPublicDto(user.Id, user.Username, user.DisplayName);
        return Results.Ok(new ApiSuccessResponse<UserPublicDto>(
            reqId,
            serverTime,
            userDto
        ));
    }
}
