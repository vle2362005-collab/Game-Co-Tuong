using System.Security.Claims;
using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Common.Interfaces;

public interface ITokenService
{
    AuthSessionDto GenerateToken(UserPublicDto user, TimeSpan? lifetime = null);
    ClaimsPrincipal? ValidateToken(string token);
}
