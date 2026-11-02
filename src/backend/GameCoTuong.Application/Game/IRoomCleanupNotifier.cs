namespace GameCoTuong.Application.Game;

public interface IRoomCleanupNotifier
{
    Task NotifyClosed(RoomView room, CancellationToken cancellationToken);
}
