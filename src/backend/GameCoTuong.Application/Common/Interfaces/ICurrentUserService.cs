namespace GameCoTuong.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    bool IsAuthenticated { get; }
}
