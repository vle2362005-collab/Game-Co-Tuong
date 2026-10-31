using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Auth.Services;

public interface IRoomAuthorizationService
{
    Task<AuthorizationResult> AuthorizeRoomHostAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthorizationResult> AuthorizeRoomMemberAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthorizationResult> AuthorizeMatchPlayerAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthorizationResult> AuthorizeMatchTurnAsync(Guid matchId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthorizationResult> AuthorizeVideoCallAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);
}
