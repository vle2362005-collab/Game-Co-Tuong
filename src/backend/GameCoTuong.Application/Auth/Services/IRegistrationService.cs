using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Auth.Services;

public interface IRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}
