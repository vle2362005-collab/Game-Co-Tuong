using System.Security.Claims;
using GameCoTuong.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GameCoTuong.Infrastructure.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null) return null;

            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user.FindFirst("sub")?.Value;

            if (Guid.TryParse(sub, out var parsedGuid))
            {
                return parsedGuid;
            }

            return null;
        }
    }

    public string? Username
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Name)?.Value
                ?? user?.FindFirst("name")?.Value;
        }
    }

    public string? DisplayName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("displayName")?.Value;
        }
    }

    public bool IsAuthenticated =>
        UserId.HasValue && (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false);
}
