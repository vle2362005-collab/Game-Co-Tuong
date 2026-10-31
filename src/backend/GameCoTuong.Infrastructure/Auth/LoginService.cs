using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Auth.Validation;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Auth;

public sealed class LoginService : ILoginService
{
    private readonly GameDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginService(
        GameDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = LoginRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return LoginResult.ValidationFailed(validationErrors);
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

        if (user is null)
        {
            // Mitigate timing attacks by executing a constant dummy verification
            _passwordHasher.VerifyPassword(request.Password, "100000.dHVtbXlzYWx0MTIzNDU2.dHVtbXloYXNoMTIzNDU2");
            return LoginResult.Failed("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var isValidPassword = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isValidPassword)
        {
            return LoginResult.Failed("INVALID_CREDENTIALS", "Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var userPublic = new UserPublicDto(user.Id, user.Username, user.DisplayName);
        var session = _tokenService.GenerateToken(userPublic);

        return LoginResult.Success(session);
    }
}
