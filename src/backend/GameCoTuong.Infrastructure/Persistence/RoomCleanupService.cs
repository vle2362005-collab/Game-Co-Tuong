using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Persistence;

public sealed class RoomCleanupService(GameDbContext dbContext)
{
    private static readonly TimeSpan WaitingRoomInactivity = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FinishedRoomRetention = TimeSpan.FromMinutes(5);

    public async Task<int> CleanupExpiredRoomsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var waitingCutoff = now - WaitingRoomInactivity;
        var finishedCutoff = now - FinishedRoomRetention;

        var inactiveWaitingRooms = await dbContext.Rooms
            .Where(room => room.Status == "waiting"
                && room.LastActivityAt <= waitingCutoff
                && !room.Memberships.Any(membership =>
                    membership.LeftAt == null && membership.ReconnectDeadlineAt > now))
            .Include(room => room.Memberships)
            .ToListAsync(cancellationToken);

        var expiredFinishedRooms = await dbContext.Rooms
            .Where(room => room.Status == "finished"
                && (((room.CurrentMatch == null
                        ? room.LastActivityAt
                        : room.CurrentMatch.EndedAt ?? room.LastActivityAt) <= finishedCutoff)
                    || (room.Memberships.Count >= 2
                        && !room.Memberships.Any(membership => membership.LeftAt == null))))
            .Include(room => room.Memberships)
            .ToListAsync(cancellationToken);

        var roomsToClose = inactiveWaitingRooms.Concat(expiredFinishedRooms).ToList();
        if (roomsToClose.Count == 0)
        {
            return 0;
        }

        foreach (var room in roomsToClose)
        {
            room.Status = "closed";
            room.ClosedAt ??= now;

            foreach (var membership in room.Memberships)
            {
                membership.IsConnected = false;
                membership.IsReady = false;
                membership.ReconnectDeadlineAt = null;
                membership.LeftAt ??= now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return roomsToClose.Count;
    }
}
