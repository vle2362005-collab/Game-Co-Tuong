using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Auth.Services;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
