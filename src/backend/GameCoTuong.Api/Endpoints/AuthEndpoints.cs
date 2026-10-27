using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;

namespace GameCoTuong.Api.Endpoints;

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
        group.MapPost("/register", async (
            RegisterRequest? request,
            IRegistrationService registrationService,
            CancellationToken cancellationToken) =>
        {
            var reqId = request?.RequestId ?? Guid.Empty;
            var serverTime = DateTimeOffset.UtcNow;

            if (request is null)
            {
                return Results.BadRequest(new
                {
                    requestId = reqId,
                    serverTime,
                    error = new
                    {
                        code = "VALIDATION_ERROR",
                        message = "Dữ liệu yêu cầu không được để trống.",
                        retryable = false,
                        currentVersion = (long?)null,
                        resyncUrl = (string?)null,
                        fieldErrors = new Dictionary<string, string[]>
                        {
                            ["request"] = ["Dữ liệu yêu cầu không được để trống."]
                        }
                    }
                });
            }

            var result = await registrationService.RegisterAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Created($"/api/v1/users/{result.User!.Id}", new
                {
                    requestId = request.RequestId,
                    serverTime = DateTimeOffset.UtcNow,
                    data = result.User
                });
            }

            var statusCode = result.ErrorCode switch
            {
                "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
                "USERNAME_TAKEN" or "EMAIL_TAKEN" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Json(new
            {
                requestId = request.RequestId,
                serverTime = DateTimeOffset.UtcNow,
                error = new
                {
                    code = result.ErrorCode,
                    message = result.ErrorMessage,
                    retryable = false,
                    currentVersion = (long?)null,
                    resyncUrl = (string?)null,
                    fieldErrors = result.FieldErrors ?? new Dictionary<string, string[]>()
                }
            }, statusCode: statusCode);
        });
    }
}
