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

    public static async Task<IResult> HandleLogoutAsync(
        HttpContext context,
        ITokenService tokenService,
        GameDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var reqId = ExtractRequestId(context);
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new ApiErrorEnvelope(
                reqId,
                DateTimeOffset.UtcNow,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Thiếu hoặc sai định dạng Authorization Bearer token.",
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
                DateTimeOffset.UtcNow,
                new ApiErrorDetail(
                    "UNAUTHORIZED",
                    "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
                    false,
                    null,
                    null,
                    new Dictionary<string, string[]>()
                )
            ), statusCode: StatusCodes.Status401Unauthorized);
        }

        var jti = principal.FindFirst("jti")?.Value;
        var sub = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? principal.FindFirst("sub")?.Value;

        try
        {
            if (!string.IsNullOrEmpty(jti) && Guid.TryParse(sub, out var userId))
            {
                // 1. Record token revocation
                var isAlreadyRevoked = await dbContext.RevokedTokens.AnyAsync(r => r.Jti == jti, cancellationToken);
                if (!isAlreadyRevoked)
                {
                    var expClaim = principal.FindFirst("exp")?.Value;
                    var expiresAt = long.TryParse(expClaim, out var expSeconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(expSeconds)
                        : DateTimeOffset.UtcNow.AddHours(24);

                    dbContext.RevokedTokens.Add(new RevokedToken
                    {
                        Jti = jti,
                        UserId = userId,
                        RevokedAt = DateTimeOffset.UtcNow,
                        ExpiresAt = expiresAt
                    });
                }

                // 2. Clean up active room and match status
                var activeMemberships = await dbContext.RoomMemberships
                    .Include(m => m.Room)
                    .Where(m => m.UserId == userId && m.LeftAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var m in activeMemberships)
                {
                    m.LeftAt = DateTimeOffset.UtcNow;
                    m.IsConnected = false;

                    if (m.Room is not null)
                    {
                        if (m.Room.CurrentMatchId.HasValue && string.Equals(m.Room.Status, "playing", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = await dbContext.Matches.FirstOrDefaultAsync(mat => mat.Id == m.Room.CurrentMatchId.Value, cancellationToken);
                            if (match is not null && string.Equals(match.Status, "playing", StringComparison.OrdinalIgnoreCase))
                            {
                                match.Status = "finished";
                                match.Winner = string.Equals(m.Side, "red", StringComparison.OrdinalIgnoreCase) ? "black" : "red";
                                match.ResultReason = "resignation";
                                match.EndedAt = DateTimeOffset.UtcNow;
                            }
                        }
                        else if (string.Equals(m.Room.Status, "waiting", StringComparison.OrdinalIgnoreCase))
                        {
                            if (m.Room.HostUserId == userId)
                            {
                                m.Room.Status = "closed";
                                m.Room.ClosedAt = DateTimeOffset.UtcNow;
                            }
                        }
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.ToString(), statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new ApiSuccessResponse<LogoutResponseDto>(
            reqId,
            DateTimeOffset.UtcNow,
            new LogoutResponseDto(
                Acknowledged: true,
                Revoked: true,
                PolicyMessage: "JWT đã được thu hồi trong cơ sở dữ liệu. Trường hợp không có bảng thu hồi (stateless JWT), token cũ vẫn có hiệu lực cho đến thời điểm hết hạn (exp)."
            )
        ));
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
