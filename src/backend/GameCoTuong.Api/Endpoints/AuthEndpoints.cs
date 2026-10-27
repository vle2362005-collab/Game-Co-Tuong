using System.Text.Json.Serialization;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;

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
}
