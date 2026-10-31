using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Auth.Validation;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Auth;

public sealed class RegistrationService : IRegistrationService
{
    private readonly GameDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public RegistrationService(GameDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ của dữ liệu đầu vào
        var validationErrors = RegisterRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return RegistrationResult.ValidationFailed(validationErrors);
        }

        // 2. Chuẩn hóa username và email (trim + lowercase) để chặn trùng kể cả khác hoa thường
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 3. Kiểm tra trước sự tồn tại trong CSDL
        var isUsernameTaken = await _dbContext.Users
            .AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);
        if (isUsernameTaken)
        {
            return RegistrationResult.UsernameTaken();
        }

        var isEmailTaken = await _dbContext.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (isEmailTaken)
        {
            return RegistrationResult.EmailTaken();
        }

        // 4. Băm mật khẩu bằng thuật toán PBKDF2-SHA256 với salt ngẫu nhiên
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 5. Khởi tạo thực thể người dùng mới
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true
        };

        // 6. Lưu vào cơ sở dữ liệu và xử lý trường hợp gửi đồng thời (race condition)
        try
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            if (message.Contains("NormalizedUsername", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("UQ_Users_NormalizedUsername", StringComparison.OrdinalIgnoreCase))
            {
                return RegistrationResult.UsernameTaken();
            }

            if (message.Contains("NormalizedEmail", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("UQ_Users_NormalizedEmail", StringComparison.OrdinalIgnoreCase))
            {
                return RegistrationResult.EmailTaken();
            }

            throw;
        }

        // 7. Trả về thông tin công khai UserPublic (tuyệt đối không chứa password, passwordHash hay email)
        var userPublic = new UserPublicDto(user.Id, user.NormalizedUsername, user.DisplayName);
        return RegistrationResult.Success(userPublic);
    }
}
