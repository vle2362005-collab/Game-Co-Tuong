using System.Data;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.AI;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameCoTuong.Infrastructure.Game;

// Serialize writes in-process; concurrency tokens and database indexes protect persisted state across processes.
public sealed class GameService(GameDbContext db, ILogger<GameService> logger)
{
    public GameService(GameDbContext db) : this(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<GameService>.Instance) { }

    public static readonly SemaphoreSlim WriteGate = new(1, 1);
    public static readonly TimeSpan DrawOfferDuration = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<Guid, AiSearchCancellation> AiSearches = new();

    private sealed class AiSearchCancellation : IDisposable
    {
        private readonly object sync = new();
        private readonly CancellationTokenSource source = new();
        private bool disposed;

        public CancellationToken Token => source.Token;

        public void Cancel()
        {
            lock (sync)
                if (!disposed)
                    source.Cancel();
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                source.Dispose();
            }
        }
    }

    private sealed record AiTurn(Guid MatchId, long Version, string BoardJson, string Difficulty, Guid UserId);
    private sealed record MoveOutcome(object Snapshot, AiTurn? AiTurn);

    public async Task<T> Write<T>(Func<Task<T>> action)
    {
        await WriteGate.WaitAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await action();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GameFault("STATE_CONFLICT", "Trạng thái đã được thay đổi bởi thao tác khác. Hãy tải lại và thử lại.", 409);
        }
        finally { WriteGate.Release(); }
    }

    private async Task<T> Once<T>(Guid userId, Guid requestId, string operation, object input, Func<Task<T>> action)
    {
        if (requestId == Guid.Empty) throw new GameFault("VALIDATION_ERROR", "Thiếu mã yêu cầu.", 400);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input, Json)));
        var receipt = await db.CommandReceipts.SingleOrDefaultAsync(r => r.UserId == userId && r.RequestId == requestId);
        if (receipt is not null)
        {
            if (receipt.Operation != operation || receipt.Fingerprint != fingerprint)
                throw new GameFault("REQUEST_CONFLICT", "Mã yêu cầu đã dùng cho thao tác khác.");
            return JsonSerializer.Deserialize<T>(receipt.ResponseBody, Json)!;
        }
        var result = await action();
        db.CommandReceipts.Add(new CommandReceipt
        {
            Id = Guid.NewGuid(), UserId = userId, RequestId = requestId, Operation = operation,
            Fingerprint = fingerprint, ResponseBody = JsonSerializer.Serialize(result, Json), StatusCode = 200,
            CreatedAt = DateTimeOffset.UtcNow
        });
        return result;
    }

    public async Task<IReadOnlyList<RoomView>> List()
    {
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.Status != "closed" &&
                (!r.CurrentMatchId.HasValue || !db.Matches.Any(m =>
                    m.Id == r.CurrentMatchId && (m.Mode == "pve" || m.Mode == "ai"))))
            .Include(r => r.Memberships).ThenInclude(m => m.User).ToListAsync();
        return rooms.OrderByDescending(r => r.CreatedAt).Select(View).ToArray();
    }

    private static RoomView View(Room r) => new(r.Id, r.Code, r.Name, r.HostUserId, r.Status, r.Version, r.CurrentMatchId,
        r.Memberships.Where(m => m.LeftAt is null).Select(m => new MemberView(m.UserId, m.User?.DisplayName ?? "Kỳ thủ", m.Side, m.IsReady, m.IsConnected)).ToArray());

    private async Task<Room> LoadRoom(Guid id) => await db.Rooms.Include(r => r.Memberships).ThenInclude(m => m.User)
        .SingleOrDefaultAsync(r => r.Id == id) ?? throw new GameFault("ROOM_NOT_FOUND", "Không tìm thấy phòng.", 404);

    private static RoomMembership Member(Room room, Guid userId)
    {
        if (room.Status == "closed") throw new GameFault("ROOM_CLOSED", "Phòng đã đóng.");
        return room.Memberships.SingleOrDefault(m => m.UserId == userId && m.LeftAt is null)
            ?? throw new GameFault("NOT_ROOM_MEMBER", "Bạn không còn ở phòng này.", 403);
    }

    public async Task<RoomView> Snapshot(Guid roomId, Guid userId)
    {
        var room = await LoadRoom(roomId); Member(room, userId); return View(room);
    }

    public async Task<RoomView> Presence(Guid roomId, Guid userId, bool connected)
    {
        var outcome = await Write(async () =>
        {
            var room = await LoadRoom(roomId);
            var member = Member(room, userId);
            AiTurn? aiTurn = null;
            if (connected && room.CurrentMatchId is Guid matchId)
            {
                var match = await LoadRecoveryMatch(matchId);
                if (match.Status == "paused" && match.RecoveryReason is "serverRestart" or "disconnect")
                {
                    await RestoreRecovery(match, userId);
                    aiTurn = CreateAiTurn(match, userId);
                }
                else
                {
                    member.IsConnected = true;
                }
            }
            else if (connected) member.IsConnected = true;
            if (!connected)
            {
                foreach (var m in room.Memberships) m.IsReady = false;
                if (room.CurrentMatchId is Guid disconnectedMatchId)
                {
                    var match = await LoadRecoveryMatch(disconnectedMatchId);
                    if (match.Status == "playing")
                        PauseForDisconnect(match, member, DateTimeOffset.UtcNow);
                    else if (match.Status == "paused" && match.RecoveryReason is "serverRestart" or "disconnect")
                        SetRecoveryDeadline(match, member, DateTimeOffset.UtcNow);
                }
                member.IsConnected = false;
            }
            room.Version++;
            return (Room: View(room), AiTurn: aiTurn);
        });

        if (outcome.AiTurn is null) return outcome.Room;
        db.ChangeTracker.Clear();
        await PlayAiTurn(outcome.AiTurn);
        db.ChangeTracker.Clear();
        return await Write(async () => View(await LoadRoom(roomId)));
    }

    public Task<int> RecoverAfterRestart() => Write(async () =>
    {
        var matches = await db.Matches
            .Include(m => m.Players)
            .Include(m => m.Room).ThenInclude(r => r!.Memberships)
            .Where(m => m.Status == "playing" || (m.Status == "paused" && (m.RecoveryReason == "serverRestart" || m.RecoveryReason == "disconnect")))
            .ToListAsync();
        var now = DateTimeOffset.UtcNow;

        foreach (var match in matches)
        {
            CancelAiSearch(match.Id);
            var wasWaitingForDisconnect = match.Status == "paused" && match.RecoveryReason == "disconnect";
            match.Status = "paused";
            if (!wasWaitingForDisconnect) match.RecoveryReason = "serverRestart";
            match.TurnStartedAt = null;
            match.DrawOfferId = null;
            match.DrawOfferedBy = null;
            match.DrawCreatedAt = null;
            match.DrawExpiresAt = null;
            match.Version++;
            match.Room!.Version++;

            foreach (var player in match.Players)
            {
                var member = match.Room!.Memberships.Single(m => m.UserId == player.UserId && m.LeftAt is null);
                if (member.IsConnected)
                    SetRecoveryDeadline(match, member, now, resetDeadline: true);
                else
                    SetRecoveryDeadline(match, member, now);
                member.IsConnected = false;
                member.IsReady = false;
            }
        }

        return matches.Count;
    });

    public async Task<object> Reconnect(Guid id, Guid userId)
    {
        var outcome = await Write(async () =>
        {
            var match = await LoadRecoveryMatch(id);
            if (!match.Players.Any(p => p.UserId == userId))
                throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
            if (match.Status == "playing" || match.Status == "cancelled" || match.Status == "finished")
                return (Snapshot: await MatchView(match, userId), AiTurn: (AiTurn?)null);
            if (match.Status != "paused" || match.RecoveryReason is not ("serverRestart" or "disconnect"))
                throw new GameFault("MATCH_NOT_RECOVERABLE", "Trận không chờ khôi phục kết nối.");

            await RestoreRecovery(match, userId);
            return (Snapshot: await MatchView(match, userId), AiTurn: CreateAiTurn(match, userId));
        });

        if (outcome.AiTurn is null) return outcome.Snapshot;
        db.ChangeTracker.Clear();
        return await PlayAiTurn(outcome.AiTurn);
    }

    public Task<int> ExpireRecoveries() => Write(async () =>
    {
        var matches = await db.Matches
            .Include(m => m.Players)
            .Include(m => m.Room)
            .Where(m => m.Status == "paused" && (m.RecoveryReason == "serverRestart" || m.RecoveryReason == "disconnect"))
            .ToListAsync();
        var now = DateTimeOffset.UtcNow;
        var expired = 0;
        foreach (var match in matches)
        {
            if (match.Room is null) throw new InvalidOperationException($"Match {match.Id} has no room.");
            var memberships = await db.RoomMemberships.Where(m => m.RoomId == match.RoomId && m.LeftAt == null).ToListAsync();
            if (match.RecoveryReason == "disconnect")
            {
                var previousStatus = match.Status;
                await ProcessExpiredDisconnects(match, memberships, now);
                if (previousStatus != match.Status && match.Status is "finished" or "cancelled")
                {
                    logger.LogWarning("Disconnected match recovery timed out. {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                        match.RoomId, match.Id, "DISCONNECT_RECOVERY_TIMEOUT", StatusCodes.Status408RequestTimeout);
                    expired++;
                }
            }
            else if (HasExpiredRecovery(match, memberships, now))
            {
                await CancelAfterRestart(match);
                logger.LogWarning("Match recovery timed out. {RoomId} {MatchId} {ErrorCode} {StatusCode}",
                    match.RoomId, match.Id, "MATCH_RECOVERY_TIMEOUT", StatusCodes.Status408RequestTimeout);
                expired++;
            }
        }
        return expired;
    });

    public Task<IReadOnlyList<(Guid MatchId, Guid RoomId)>> ExpireDrawOffers() =>
        Write<IReadOnlyList<(Guid MatchId, Guid RoomId)>>(async () =>
    {
        var matches = await db.Matches
            .Where(m => m.Status == "playing" &&
                (m.DrawOfferId != null || m.DrawOfferedBy != null || m.DrawCreatedAt != null || m.DrawExpiresAt != null))
            .ToListAsync();
        var now = DateTimeOffset.UtcNow;
        var expired = new List<(Guid MatchId, Guid RoomId)>();
        foreach (var match in matches)
        {
            if (ExpireDrawOfferIfNeeded(match, now))
                expired.Add((match.Id, match.RoomId));
        }
        return expired;
    });

    private static bool ExpireDrawOfferIfNeeded(Match match, DateTimeOffset now)
    {
        if (match.DrawOfferId is not null &&
            match.DrawOfferedBy is not null &&
            match.DrawCreatedAt is not null &&
            match.DrawExpiresAt is DateTimeOffset expiresAt &&
            now < expiresAt)
            return false;

        if (match.DrawOfferId is null &&
            match.DrawOfferedBy is null &&
            match.DrawCreatedAt is null &&
            match.DrawExpiresAt is null)
            return false;

        ClearDrawOffer(match);
        match.Version++;
        return true;
    }

    private static void ClearDrawOffer(Match match)
    {
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;
    }

    private async Task<Match> LoadRecoveryMatch(Guid id) =>
        await db.Matches.Include(m => m.Players).Include(m => m.Room).ThenInclude(r => r!.Memberships)
            .SingleOrDefaultAsync(m => m.Id == id)
        ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

    private static void SetRecoveryDeadline(Match match, RoomMembership member, DateTimeOffset now, bool resetDeadline = false)
    {
        var red = member.Side == "red";
        var remainingMs = red ? match.RedRecoveryRemainingMs : match.BlackRecoveryRemainingMs;
        var existingDeadline = red ? match.RedDeadlineAt : match.BlackDeadlineAt;
        var deadline = resetDeadline
            ? now.AddMilliseconds(Math.Max(0, remainingMs))
            : existingDeadline ?? member.ReconnectDeadlineAt ?? now.AddMilliseconds(Math.Max(0, remainingMs));

        member.ReconnectDeadlineAt = deadline;
        if (red)
        {
            if (resetDeadline) match.RedDisconnectedAt = now;
            else match.RedDisconnectedAt ??= now;
            match.RedDeadlineAt = deadline;
        }
        else
        {
            if (resetDeadline) match.BlackDisconnectedAt = now;
            else match.BlackDisconnectedAt ??= now;
            match.BlackDeadlineAt = deadline;
        }
    }

    private static void PauseForDisconnect(Match match, RoomMembership member, DateTimeOffset now)
    {
        CancelAiSearch(match.Id);
        match.Status = "paused";
        match.RecoveryReason = "disconnect";
        match.TurnStartedAt = null;
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;
        SetRecoveryDeadline(match, member, now);
        match.Version++;
        if (match.Room is not null) match.Room.Version++;
    }

    private async Task RestoreRecovery(Match match, Guid userId)
    {
        var room = match.Room ?? throw new InvalidOperationException($"Match {match.Id} has no room.");
        var memberships = room.Memberships.Where(m => m.LeftAt is null).ToList();
        var now = DateTimeOffset.UtcNow;
        var player = match.Players.Single(p => p.UserId == userId);
        var member = memberships.Single(m => m.UserId == userId);
        if (member.IsConnected) return;

        if (match.RecoveryReason == "serverRestart" && HasExpiredRecovery(match, memberships, now))
        {
            await CancelAfterRestart(match);
            return;
        }

        var deadline = player.Side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt;
        if (deadline is null || now >= deadline.Value)
        {
            if (match.RecoveryReason == "serverRestart")
                await CancelAfterRestart(match);
            else
                await ProcessExpiredDisconnects(match, memberships, now);
            return;
        }

        var disconnectedAt = player.Side == "red" ? match.RedDisconnectedAt : match.BlackDisconnectedAt;
        var remainingMs = player.Side == "red" ? match.RedRecoveryRemainingMs : match.BlackRecoveryRemainingMs;
        if (disconnectedAt is not null)
        {
            var elapsedMs = Math.Max(0, Math.Ceiling((now - disconnectedAt.Value).TotalMilliseconds));
            remainingMs = Math.Max(0, remainingMs - (int)Math.Min(Math.Max(0, remainingMs), elapsedMs));
        }

        member.IsConnected = true;
        member.ReconnectDeadlineAt = null;
        if (player.Side == "red")
        {
            match.RedRecoveryRemainingMs = remainingMs;
            match.RedDisconnectedAt = null;
            match.RedDeadlineAt = null;
        }
        else
        {
            match.BlackRecoveryRemainingMs = remainingMs;
            match.BlackDisconnectedAt = null;
            match.BlackDeadlineAt = null;
        }
        match.Version++;
        room.Version++;

        if (match.Players.All(p => memberships.Single(m => m.UserId == p.UserId).IsConnected))
        {
            match.Status = "playing";
            match.RecoveryReason = null;
            match.TurnStartedAt = now;
        }
        else if (match.RecoveryReason == "disconnect")
        {
            await ProcessExpiredDisconnects(match, memberships, now);
        }
    }

    private async Task ProcessExpiredDisconnects(Match match, IReadOnlyCollection<RoomMembership> memberships, DateTimeOffset now)
    {
        var expiredPlayers = match.Players
            .Where(player =>
            {
                var member = memberships.Single(m => m.UserId == player.UserId);
                var deadline = player.Side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt;
                return !member.IsConnected && deadline is not null && now >= deadline.Value;
            })
            .OrderBy(player => player.Side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt)
            .ToArray();

        foreach (var expiredPlayer in expiredPlayers)
        {
            if (match.Mode is "pve" or "ai")
            {
                await Finish(match, expiredPlayer.Side == "red" ? "black" : "red", "disconnect-timeout");
                return;
            }

            var opponent = match.Players.Single(player => player.UserId != expiredPlayer.UserId);
            if (memberships.Single(m => m.UserId == opponent.UserId).IsConnected)
            {
                await Finish(match, opponent.Side, "disconnect-timeout");
                return;
            }
        }

        if (expiredPlayers.Length == match.Players.Count && match.Players.All(p => !memberships.Single(m => m.UserId == p.UserId).IsConnected))
            await CancelAfterDisconnect(match);
    }

    private async Task CancelAfterDisconnect(Match match)
    {
        match.Status = "cancelled";
        match.Winner = null;
        match.ResultReason = "disconnect-timeout";
        match.EndedAt = DateTimeOffset.UtcNow;
        match.TurnStartedAt = null;
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;
        match.Version++;
        var room = match.Room ?? await db.Rooms.SingleAsync(r => r.Id == match.RoomId);
        room.Status = "finished";
        room.Version++;
    }

    private static bool HasExpiredRecovery(Match match, IReadOnlyCollection<RoomMembership> memberships, DateTimeOffset now)
    {
        foreach (var player in match.Players)
        {
            var member = memberships.Single(m => m.UserId == player.UserId);
            var deadline = player.Side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt;
            if (!member.IsConnected && (deadline is null || now >= deadline.Value)) return true;
        }
        return false;
    }

    private async Task CancelAfterRestart(Match match)
    {
        match.Status = "cancelled";
        match.Winner = null;
        match.ResultReason = "serverRestart";
        match.EndedAt = DateTimeOffset.UtcNow;
        match.TurnStartedAt = null;
        match.DrawOfferId = null;
        match.DrawOfferedBy = null;
        match.DrawCreatedAt = null;
        match.DrawExpiresAt = null;
        match.Version++;
        var room = match.Room ?? await db.Rooms.SingleAsync(r => r.Id == match.RoomId);
        room.Status = "finished";
        room.Version++;
    }

    public Task<RoomView> Create(Guid userId, CreateRoomInput input) => Write(() => Once(userId, input.RequestId, "create", input, async () =>
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 80)
            throw new GameFault("VALIDATION_ERROR", "Tên phòng từ 1 đến 80 ký tự.", 400);
        if (await db.RoomMemberships.AnyAsync(m => m.UserId == userId && m.LeftAt == null))
            throw new GameFault("ALREADY_IN_ROOM", "Bạn đang ở một phòng khác.");
        string code;
        do { code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); }
        while (await db.Rooms.AnyAsync(r => r.Code == code && r.Status != "closed"));
        var now = DateTimeOffset.UtcNow;
        var room = new Room { Id = Guid.NewGuid(), Code = code, Name = input.Name.Trim(), HostUserId = userId, CreatedAt = now, LastActivityAt = now };
        room.Memberships.Add(new RoomMembership { Id = Guid.NewGuid(), UserId = userId, Side = "red", JoinedAt = now, User = await db.Users.FindAsync(userId) });
        db.Rooms.Add(room);
        return View(room);
    }));

    public Task<object> CreateAiMatch(Guid userId, CreateAiMatchInput input) =>
        Write(() => Once(userId, input.RequestId, "create-ai-match", input, async () =>
        {
            if (string.IsNullOrWhiteSpace(input.Difficulty))
                throw new GameFault("VALIDATION_ERROR", "Hãy chọn mức khó easy, medium hoặc hard.", 400);
            var difficulty = input.Difficulty.Trim().ToLowerInvariant();
            if (difficulty is not ("easy" or "medium" or "hard"))
                throw new GameFault("VALIDATION_ERROR", "Mức khó phải là easy, medium hoặc hard.", 400);
            if (await db.RoomMemberships.AnyAsync(m => m.UserId == userId && m.LeftAt == null))
                throw new GameFault("ALREADY_IN_ROOM", "Bạn đang ở một phòng khác.");

            string code;
            do { code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); }
            while (await db.Rooms.AnyAsync(r => r.Code == code && r.Status != "closed"));

            var now = DateTimeOffset.UtcNow;
            var board = BoardSerializer.Serialize(BoardInitializer.CreateInitialBoard());
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = "Đấu với máy",
                HostUserId = userId,
                Status = "playing",
                CreatedAt = now,
                LastActivityAt = now
            };
            room.Memberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Side = "red",
                JoinedAt = now,
                User = await db.Users.FindAsync(userId)
            });
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            var match = new Match
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Mode = "pve",
                AiDifficulty = difficulty,
                InitialBoardJson = board,
                CurrentBoardJson = board,
                StartedAt = now,
                TurnStartedAt = now,
                RedRemainingMs = room.InitialMinutes * 60000,
                BlackRemainingMs = room.InitialMinutes * 60000
            };
            match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = userId, Side = "red" });
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            room.CurrentMatchId = match.Id;
            room.Version++;
            return await MatchView(match, userId);
        }));

    public Task<RoomView> Join(Guid userId, string code) => Write(async () =>
    {
        var room = await db.Rooms.Include(r => r.Memberships).ThenInclude(m => m.User)
            .SingleOrDefaultAsync(r => r.Code == code && r.Status != "closed")
            ?? throw new GameFault("ROOM_NOT_FOUND", "Mã phòng không tồn tại.", 404);
        if (room.Memberships.Any(m => m.UserId == userId && m.LeftAt is null)) return View(room);
        if (room.Status != "waiting" || room.Memberships.Count(m => m.LeftAt is null) >= 2)
            throw new GameFault("ROOM_FULL", "Phòng đầy hoặc đã bắt đầu.");
        if (await db.RoomMemberships.AnyAsync(m => m.UserId == userId && m.LeftAt == null))
            throw new GameFault("ALREADY_IN_ROOM", "Bạn đang ở phòng khác.");
        foreach (var m in room.Memberships) m.IsReady = false;
        var joining = new RoomMembership { Id = Guid.NewGuid(), RoomId = room.Id, UserId = userId, Side = "black", JoinedAt = DateTimeOffset.UtcNow, User = await db.Users.FindAsync(userId) };
        db.RoomMemberships.Add(joining);
        room.Memberships.Add(joining);
        room.Version++; room.LastActivityAt = DateTimeOffset.UtcNow;
        return View(room);
    });

    public Task<RoomView> Ready(Guid roomId, Guid userId, bool ready) => Write(async () =>
    {
        var room = await LoadRoom(roomId); var member = Member(room, userId);
        if (room.Status != "waiting") throw new GameFault("ROOM_PLAYING", "Trận đã bắt đầu.");
        member.IsReady = ready; room.Version++; return View(room);
    });

    public Task<RoomView> Start(Guid roomId, Guid userId, CommandInput input) => Write(() => Once(userId, input.RequestId, "start", new { roomId }, async () =>
    {
        var room = await LoadRoom(roomId); Member(room, userId);
        if (room.HostUserId != userId) throw new GameFault("NOT_ROOM_HOST", "Chỉ chủ phòng được bắt đầu.", 403);
        if (room.CurrentMatchId.HasValue && room.Status == "playing") return View(room);
        var members = room.Memberships.Where(m => m.LeftAt is null).ToArray();
        if (room.Status != "waiting" || members.Length != 2 || members.Any(m => !m.IsReady || !m.IsConnected))
            throw new GameFault("NOT_READY", "Cần đủ hai người trực tuyến và sẵn sàng.");
        string board = BoardSerializer.Serialize(BoardInitializer.CreateInitialBoard());
        var match = new Match { Id = Guid.NewGuid(), RoomId = room.Id, InitialBoardJson = board, CurrentBoardJson = board,
            StartedAt = DateTimeOffset.UtcNow, TurnStartedAt = DateTimeOffset.UtcNow,
            RedRemainingMs = room.InitialMinutes * 60000, BlackRemainingMs = room.InitialMinutes * 60000 };
        foreach (var m in members) match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = m.UserId, Side = m.Side });
        db.Matches.Add(match);
        // Resolve the circular Rooms.CurrentMatch -> Matches.Room relation in two saves in one transaction.
        await db.SaveChangesAsync();
        room.CurrentMatchId = match.Id; room.Status = "playing"; room.Version++;
        return View(room);
    }));

    public Task<RoomView> Leave(Guid roomId, Guid userId, bool close) => Write(async () =>
    {
        var room = await LoadRoom(roomId); var member = Member(room, userId);
        if (room.Status == "playing") throw new GameFault("RESIGN_REQUIRED", "Hãy xác nhận đầu hàng trước khi rời trận.");
        if (close && room.HostUserId != userId) throw new GameFault("NOT_ROOM_HOST", "Chỉ chủ phòng được đóng phòng.", 403);
        if (room.HostUserId == userId)
        {
            room.Status = "closed"; room.ClosedAt = DateTimeOffset.UtcNow;
            foreach (var m in room.Memberships.Where(m => m.LeftAt is null)) { m.LeftAt = DateTimeOffset.UtcNow; m.IsReady = false; m.IsConnected = false; }
        }
        else { member.LeftAt = DateTimeOffset.UtcNow; member.IsReady = false; member.IsConnected = false; }
        foreach (var m in room.Memberships) m.IsReady = false;
        room.Version++; return View(room);
    });

    private async Task<Match> LoadMatch(Guid id, Guid userId)
    {
        var match = await db.Matches.Include(m => m.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(m => m.Id == id)
            ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);
        if (!match.Players.Any(p => p.UserId == userId)) throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
        return match;
    }

    public async Task<object> MatchHistory(Guid userId, int page = 1, int pageSize = 20, string? outcome = null, string? mode = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new GameFault("VALIDATION_ERROR", "Thông tin phân trang không hợp lệ.", 400);

        var normalizedOutcome = outcome?.Trim().ToLowerInvariant();
        if (normalizedOutcome is "" or "all") normalizedOutcome = null;
        if (normalizedOutcome is not null &&
            normalizedOutcome is not ("win" or "loss" or "draw" or "cancelled" or "in-progress"))
            throw new GameFault("VALIDATION_ERROR", "Bộ lọc kết quả không hợp lệ.", 400);

        var normalizedMode = mode?.Trim().ToLowerInvariant();
        if (normalizedMode is "" or "all") normalizedMode = null;
        if (normalizedMode is not null && normalizedMode is not ("pvp" or "pve" or "ai"))
            throw new GameFault("VALIDATION_ERROR", "Bộ lọc loại trận không hợp lệ.", 400);

        IQueryable<Match> matches = db.Matches.AsNoTracking()
            .Where(m => m.Players.Any(p => p.UserId == userId));

        if (normalizedMode == "pvp")
            matches = matches.Where(m => m.Mode.ToLower() == "pvp");
        else if (normalizedMode is "pve" or "ai")
            matches = matches.Where(m => m.Mode.ToLower() == "pve" || m.Mode.ToLower() == "ai");

        if (normalizedOutcome is not null)
        {
            var cancelled = matches.Where(m =>
                m.Status.ToLower() == "cancelled" || m.Status.ToLower() == "canceled" ||
                m.Status.ToLower() == "cancellation" ||
                m.ResultReason != null &&
                (m.ResultReason.ToLower() == "cancelled" || m.ResultReason.ToLower() == "canceled" ||
                 m.ResultReason.ToLower() == "cancellation"));
            var terminal = matches.Where(m =>
                m.Status.ToLower() == "finished" || m.Status.ToLower() == "completed" ||
                m.Status.ToLower() == "ended" || m.Status.ToLower() == "cancelled" ||
                m.Status.ToLower() == "canceled" || m.Status.ToLower() == "cancellation");

            matches = normalizedOutcome switch
            {
                "cancelled" => cancelled,
                "in-progress" => matches.Where(m => !terminal.Any(t => t.Id == m.Id)),
                "draw" => matches.Where(m => m.Winner == null && terminal.Any(t => t.Id == m.Id) &&
                                              !cancelled.Any(c => c.Id == m.Id)),
                "win" => matches.Where(m => m.Winner != null &&
                                              m.Players.Any(p => p.UserId == userId && p.Side == m.Winner) &&
                                              terminal.Any(t => t.Id == m.Id) && !cancelled.Any(c => c.Id == m.Id)),
                "loss" => matches.Where(m => m.Winner != null &&
                                              m.Players.Any(p => p.UserId == userId && p.Side != m.Winner) &&
                                              terminal.Any(t => t.Id == m.Id) && !cancelled.Any(c => c.Id == m.Id)),
                _ => matches
            };
        }

        var totalCount = await matches.CountAsync();
        var orderedMatches = db.Database.IsSqlite()
            ? matches.OrderByDescending(match => match.StartedAt.ToString())
            : matches.OrderByDescending(match => match.StartedAt);
        var items = await orderedMatches
            .ThenByDescending(match => match.Id)
            .Skip((int)((long)(page - 1) * pageSize))
            .Take(pageSize)
            .Include(match => match.Players).ThenInclude(player => player.User)
            .ToListAsync();

        return new
        {
            items = items.Select(match => MatchHistorySummary(match, userId)).ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<object> MatchDetails(Guid id, Guid userId)
    {
        var match = await LoadMatch(id, userId);
        var moves = await db.Moves.AsNoTracking().Where(m => m.MatchId == id).OrderBy(m => m.Ply)
            .Select(m => new
            {
                m.Ply, m.RequestId, m.Side, m.PieceId, m.FromCol, m.FromRow, m.ToCol, m.ToRow,
                m.CapturedPieceId, m.CreatedAt, m.BoardSnapshotJson
            }).ToListAsync();

        return new
        {
            match.Id, match.RoomId, match.Mode, match.AiDifficulty, match.Status,
            outcome = GetOutcome(match, userId),
            winner = match.Winner,
            match.ResultReason,
            reasonCode = GetResultReasonCode(match.ResultReason),
            result = GetMatchResult(match),
            playerSide = match.Players.Single(p => p.UserId == userId).Side,
            opponent = GetOpponent(match, userId),
            match.StartedAt, match.EndedAt,
            durationSeconds = Math.Max(0, ((match.EndedAt ?? DateTimeOffset.UtcNow) - match.StartedAt).TotalSeconds),
            match.RuleSetVersion, match.InitialBoardJson, match.CurrentBoardJson,
            moves
        };
    }

    private static object MatchHistorySummary(Match match, Guid userId)
    {
        return new
        {
            match.Id, match.RoomId, match.Mode, match.AiDifficulty, match.Status,
            outcome = GetOutcome(match, userId),
            winner = match.Winner,
            match.ResultReason,
            reasonCode = GetResultReasonCode(match.ResultReason),
            result = GetMatchResult(match),
            playerSide = match.Players.Single(p => p.UserId == userId).Side,
            opponent = GetOpponent(match, userId),
            match.StartedAt, match.EndedAt,
            durationSeconds = Math.Max(0, ((match.EndedAt ?? DateTimeOffset.UtcNow) - match.StartedAt).TotalSeconds),
            match.MoveNumber
        };
    }

    private static string GetOutcome(Match match, Guid userId)
    {
        if (IsCancellation(match.Status) || IsCancellation(match.ResultReason)) return "cancelled";
        if (!IsTerminalStatus(match.Status)) return "in-progress";
        if (match.Winner is null) return "draw";
        return match.Players.Single(p => p.UserId == userId).Side == match.Winner ? "win" : "loss";
    }

    private static bool IsTerminalStatus(string status) =>
        status.Equals("finished", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("ended", StringComparison.OrdinalIgnoreCase) ||
        IsCancellation(status);

    private static string? GetResultReasonCode(string? reason) => reason?.ToLowerInvariant() switch
    {
        "resignation" or "surrender" => "surrender",
        "draw-agreement" => "draw-agreement",
        "checkmate" => "checkmate",
        "stalemate" => "stalemate",
        "disconnect-timeout" or "timeout" => "timeout",
        "serverrestart" or "server-restart" => "server-restart",
        "cancelled" or "canceled" or "cancellation" => "cancellation",
        null => null,
        "" => "unknown",
        _ => reason
    };

    private static MatchResultView? GetMatchResult(Match match) =>
        IsTerminalStatus(match.Status)
            ? new MatchResultView(
                match.Status,
                match.Winner,
                IsCancellation(match.Status) || IsCancellation(match.ResultReason)
                    ? "cancelled"
                    : match.Winner is null ? "draw" : $"{match.Winner}-win",
                GetResultReasonCode(match.ResultReason) ?? "unknown",
                match.ResultReason,
                match.EndedAt)
            : null;

    private static bool IsCancellation(string? value) =>
        string.Equals(value, "cancelled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "canceled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "cancellation", StringComparison.OrdinalIgnoreCase);

    private static MatchOpponentView? GetOpponent(Match match, Guid userId)
    {
        var player = match.Players.Single(p => p.UserId == userId);
        var opponent = match.Players.FirstOrDefault(p => p.UserId != userId);
        if (match.Mode is "ai" or "pve")
            return new(null, "Máy", opponent?.Side ?? (player.Side == "red" ? "black" : "red"));
        if (opponent is not null)
            return new(opponent.UserId, opponent.User?.DisplayName ?? "Kỳ thủ", opponent.Side);
        return null;
    }

    private sealed record MatchOpponentView(Guid? UserId, string DisplayName, string Side);

    public async Task<object> MatchSnapshot(Guid id, Guid userId) => await MatchView(await LoadMatch(id, userId));
    public async Task<object> MatchStateSnapshot(Guid id, Guid userId) => await MatchView(await LoadMatch(id, userId));
    public async Task<object> MatchStateSnapshot(Guid id)
    {
        var match = await db.Matches.Include(m => m.Players).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(m => m.Id == id)
            ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);
        return await MatchView(match);
    }

    public async Task<IReadOnlyList<LegalMove>> LegalMoves(Guid id, Guid userId, string pieceId)
    {
        if (string.IsNullOrWhiteSpace(pieceId))
            throw new GameFault("VALIDATION_ERROR", "Thiếu mã quân cờ.", 400);

        var match = await LoadMatch(id, userId);
        if (match.Status != "playing") return Array.Empty<LegalMove>();

        var sideToMove = ParseSide(match.SideToMove);
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        return XiangqiRules.Generate(board, sideToMove, pieceId);
    }

    public async Task<object> DisconnectionSnapshot(Guid id, Guid userId)
    {
        var match = await LoadMatch(id, userId);
        var player = match.Players.Single(p => p.UserId == userId);
        var deadline = player.Side == "red" ? match.RedDeadlineAt : match.BlackDeadlineAt;
        var countdownMs = deadline is null ? 0 : Math.Max(0, (int)Math.Ceiling((deadline.Value - DateTimeOffset.UtcNow).TotalMilliseconds));
        return new
        {
            playerId = userId,
            player.Side,
            matchId = match.Id,
            status = match.Status == "paused" && match.RecoveryReason == "disconnect" ? "waiting_reconnect" : match.Status,
            gracePeriodMs = countdownMs,
            deadlineAt = deadline,
            countdownMs,
            match = await MatchView(match, userId)
        };
    }

    private async Task<object> MatchView(Match match, Guid? userId = null)
    {
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        var turn = ParseSide(match.SideToMove);
        var now = DateTimeOffset.UtcNow;
        var redCountdown = match.RedDeadlineAt is null ? 0 : Math.Max(0, (int)Math.Ceiling((match.RedDeadlineAt.Value - now).TotalMilliseconds));
        var blackCountdown = match.BlackDeadlineAt is null ? 0 : Math.Max(0, (int)Math.Ceiling((match.BlackDeadlineAt.Value - now).TotalMilliseconds));
        // The accepted move is still tracked before SaveChanges; GETs read the latest persisted move.
        var last = db.Moves.Local.FirstOrDefault(m => m.MatchId == match.Id && m.Ply == match.MoveNumber)
            ?? await db.Moves.AsNoTracking().Where(m => m.MatchId == match.Id).OrderByDescending(m => m.Ply).FirstOrDefaultAsync();
        var isCheck = XiangqiRules.IsInCheck(board, turn);
        Position? checkedKingPos = isCheck ? board.GetGeneral(turn).Position : null;
        return new
        {
            match.Id, match.RoomId, match.Status,
            recoveryState = match.Status == "paused" && match.RecoveryReason == "disconnect" ? "waiting_reconnect" : match.Status,
            playerSide = userId is null ? null : match.Players.Single(p => p.UserId == userId).Side,
            match.Mode, match.AiDifficulty, match.Version, match.MoveNumber, match.SideToMove,
            pieces = board.ToArray(), match.Winner, match.ResultReason,
            reasonCode = GetResultReasonCode(match.ResultReason), result = GetMatchResult(match), match.RecoveryReason,
            match.RedRemainingMs, match.BlackRemainingMs,
            match.RedRecoveryRemainingMs, match.RedDeadlineAt, redReconnectCountdownMs = redCountdown,
            match.BlackRecoveryRemainingMs, match.BlackDeadlineAt, blackReconnectCountdownMs = blackCountdown,
            check = isCheck,
            isCheck,
            checkedKingPos,
            legalMoves = match.Status == "playing" ? XiangqiRules.Generate(board, turn) : Array.Empty<LegalMove>(),
            lastMove = last is null ? null : new { last.RequestId, last.Ply, last.Side, last.PieceId,
                last.FromCol, last.FromRow, last.ToCol, last.ToRow, last.CapturedPieceId },
            players = match.Players.Select(p => new { p.UserId, p.Side }),
            match.DrawOfferId, match.DrawOfferedBy, match.DrawCreatedAt, match.DrawExpiresAt
        };
    }
    private static Side ParseSide(string side) => side == "red" ? Side.Red : Side.Black;

    public async Task<Guid> MatchRoomId(Guid id, Guid userId) => (await LoadMatch(id, userId)).RoomId;

    public async Task<object> Move(Guid id, Guid userId, MoveInput input)
    {
        var outcome = await Write(async () =>
        {
            var match = await LoadMatch(id, userId);
            if (input.RequestId == Guid.Empty) throw new GameFault("VALIDATION_ERROR", "Thiếu mã nước.", 400);
            if (string.IsNullOrWhiteSpace(input.PieceId)) throw new GameFault("VALIDATION_ERROR", "Thiếu mã quân cờ.", 400);
            var previous = await db.Moves.SingleOrDefaultAsync(m => m.MatchId == id && m.RequestId == input.RequestId);
            if (previous is not null)
            {
                var playerSide = match.Players.Single(p => p.UserId == userId).Side;
                if (previous.Side != playerSide || previous.PieceId != input.PieceId || previous.ToCol != input.ToCol || previous.ToRow != input.ToRow)
                    throw new GameFault("REQUEST_CONFLICT", "Mã nước đã được sử dụng.");
                return new MoveOutcome(await MatchView(match), null);
            }
            if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận không cho phép đi quân.");
            if (match.Version != input.ExpectedVersion) throw new GameFault("VERSION_CONFLICT", "Bàn cờ đã thay đổi, hãy tải lại.");
            if (match.Players.Single(p => p.UserId == userId).Side != match.SideToMove)
                throw new GameFault("NOT_YOUR_TURN", "Chưa đến lượt của bạn.", 403);
            if (!Position.IsValid(input.ToCol, input.ToRow)) throw new GameFault("INVALID_MOVE", "Tọa độ ngoài bàn cờ.", 400);
            var move = new LegalMove(input.PieceId, new Position(input.ToCol, input.ToRow));
            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            if (!XiangqiRules.IsLegal(board, ParseSide(match.SideToMove), move))
                throw new GameFault("INVALID_MOVE", "Nước đi không hợp lệ.", 400);
            await ApplyMove(match, input.RequestId, move);

            return new MoveOutcome(await MatchView(match), CreateAiTurn(match, userId));
        });

        if (outcome.AiTurn is null) return outcome.Snapshot;
        db.ChangeTracker.Clear();
        return await PlayAiTurn(outcome.AiTurn);
    }

    private async Task<object> PlayAiTurn(AiTurn turn)
    {
        var cancellation = new AiSearchCancellation();
        if (!AiSearches.TryAdd(turn.MatchId, cancellation))
        {
            cancellation.Dispose();
            return await ReadCurrentMatch(turn.MatchId, turn.UserId);
        }

        try
        {
            var current = await db.Matches.AsNoTracking()
                .Where(match => match.Id == turn.MatchId)
                .Select(match => new { match.Status, match.Version, match.SideToMove, match.CurrentBoardJson })
                .SingleOrDefaultAsync();
            if (current is null || current.Status != "playing" || current.Version != turn.Version ||
                current.SideToMove != "black" || current.CurrentBoardJson != turn.BoardJson)
                return await ReadCurrentMatch(turn.MatchId, turn.UserId);

            var (depth, budget) = AiSearchParameters(turn.Difficulty);
            var aiBoard = BoardSerializer.Deserialize(turn.BoardJson);
            LegalMove? aiMove;
            try
            {
                aiMove = Minimax.FindBest(aiBoard, Side.Black, depth, budget, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
            {
                return await ReadCurrentMatch(turn.MatchId, turn.UserId);
            }

            if (cancellation.Token.IsCancellationRequested)
                return await ReadCurrentMatch(turn.MatchId, turn.UserId);

            return await Write(async () =>
            {
                var match = await LoadMatch(turn.MatchId, turn.UserId);
                if (match.Status != "playing" || match.Version != turn.Version ||
                    match.SideToMove != "black" || match.CurrentBoardJson != turn.BoardJson)
                    return await MatchView(match);

                if (aiMove is null)
                {
                    var result = XiangqiRules.Result(aiBoard, Side.Black);
                    if (result.Finished)
                        await Finish(match, result.Winner == Side.Red ? "red" : "black", result.Reason!);
                }
                else
                {
                    await ApplyMove(match, Guid.NewGuid(), aiMove);
                }
                return await MatchView(match);
            });
        }
        finally
        {
            AiSearches.TryRemove(turn.MatchId, out _);
            cancellation.Dispose();
        }
    }

    private Task<object> ReadCurrentMatch(Guid matchId, Guid userId) => Write(async () =>
    {
        var match = await LoadMatch(matchId, userId);
        return await MatchView(match);
    });

    private static void CancelAiSearch(Guid matchId)
    {
        if (AiSearches.TryGetValue(matchId, out var cancellation))
            cancellation.Cancel();
    }

    private static AiTurn? CreateAiTurn(Match match, Guid userId) =>
        match.Status == "playing" && (match.Mode is "pve" or "ai") && match.SideToMove == "black"
            ? new AiTurn(match.Id, match.Version, match.CurrentBoardJson, match.AiDifficulty, userId)
            : null;

    private async Task ApplyMove(Match match, Guid requestId, LegalMove move)
    {
        CancelAiSearch(match.Id);
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        var side = ParseSide(match.SideToMove);
        if (!XiangqiRules.IsLegal(board, side, move))
            throw new GameFault("AI_MOVE_INVALID", "Bộ máy chọn một nước không hợp lệ.", 500);
        var piece = board.GetPieceById(move.PieceId)!;
        var next = board.ApplyMove(piece.Id, move.To, out var captured);
        db.Moves.Add(new Move { Id = Guid.NewGuid(), MatchId = match.Id, RequestId = requestId, Ply = ++match.MoveNumber,
            MatchVersion = ++match.Version, Side = match.SideToMove, PieceId = piece.Id, FromCol = piece.Position.Col,
            FromRow = piece.Position.Row, ToCol = move.To.Col, ToRow = move.To.Row, CapturedPieceId = captured?.Id,
            BoardSnapshotJson = BoardSerializer.Serialize(next), CreatedAt = DateTimeOffset.UtcNow });
        match.CurrentBoardJson = BoardSerializer.Serialize(next);
        match.SideToMove = match.SideToMove == "red" ? "black" : "red";
        ClearDrawOffer(match);
        var result = XiangqiRules.Result(next, ParseSide(match.SideToMove));
        if (result.Finished) await Finish(match, result.Winner == Side.Red ? "red" : "black", result.Reason!);
    }

    private static (int Depth, TimeSpan Budget) AiSearchParameters(string difficulty) => difficulty switch
    {
        "easy" => (1, TimeSpan.FromMilliseconds(300)),
        "medium" => (2, TimeSpan.FromMilliseconds(900)),
        "hard" => (3, TimeSpan.FromMilliseconds(1500)),
        _ => throw new GameFault("INVALID_AI_DIFFICULTY", "Mức khó của trận không hợp lệ.", 500)
    };

    private async Task Finish(Match match, string? winner, string reason)
    {
        if (IsTerminalStatus(match.Status)) return;
        CancelAiSearch(match.Id);
        match.Status = "finished"; match.Winner = winner; match.ResultReason = reason;
        match.EndedAt = DateTimeOffset.UtcNow; match.Version++; ClearDrawOffer(match);
        var room = await LoadRoom(match.RoomId); room.Status = "finished"; room.Version++;
    }

    public Task<object> Surrender(Guid id, Guid userId, bool confirmed) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        if (!confirmed) throw new GameFault("CONFIRMATION_REQUIRED", "Cần xác nhận trước khi đầu hàng.", 400);
        if (IsTerminalStatus(match.Status)) return await MatchView(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        var side = match.Players.Single(p => p.UserId == userId).Side;
        await Finish(match, side == "red" ? "black" : "red", "resignation");
        return await MatchView(match);
    });

    public Task<object> OfferDraw(Guid id, Guid userId) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        EnsureDrawNegotiationAllowed(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        var now = DateTimeOffset.UtcNow;
        ExpireDrawOfferIfNeeded(match, now);
        var side = match.Players.Single(p => p.UserId == userId).Side;
        if (match.DrawOfferId is not null)
        {
            if (match.DrawOfferedBy == side) return await MatchView(match);
            throw new GameFault("DRAW_OFFER_PENDING", "Đối thủ đã gửi đề nghị hòa; hãy phản hồi đề nghị đó.");
        }

        match.DrawOfferId = Guid.NewGuid();
        match.DrawOfferedBy = side;
        match.DrawCreatedAt = now;
        match.DrawExpiresAt = now + DrawOfferDuration;
        match.Version++;
        return await MatchView(match);
    });

    public Task<object> AcceptDraw(Guid id, Guid userId, Guid offerId) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        EnsureDrawNegotiationAllowed(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        if (ExpireDrawOfferIfNeeded(match, DateTimeOffset.UtcNow)) return await MatchView(match);
        if (match.DrawOfferId is null) throw new GameFault("DRAW_OFFER_NOT_FOUND", "Không có đề nghị hòa còn hiệu lực.");
        if (offerId == Guid.Empty) throw new GameFault("VALIDATION_ERROR", "Thiếu mã đề nghị hòa.", 400);
        if (match.DrawOfferId != offerId)
            throw new GameFault("DRAW_OFFER_STALE", "Đề nghị hòa đã thay đổi hoặc hết hiệu lực.");

        var side = match.Players.Single(p => p.UserId == userId).Side;
        if (match.DrawOfferedBy == side)
            throw new GameFault("DRAW_OFFER_NOT_YOURS", "Bạn không thể chấp nhận đề nghị hòa của chính mình.");

        await Finish(match, null, "draw-agreement");
        return await MatchView(match);
    });

    public Task<object> RejectDraw(Guid id, Guid userId, Guid offerId) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        EnsureDrawNegotiationAllowed(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        if (ExpireDrawOfferIfNeeded(match, DateTimeOffset.UtcNow)) return await MatchView(match);
        if (match.DrawOfferId is null) throw new GameFault("DRAW_OFFER_NOT_FOUND", "Không có đề nghị hòa còn hiệu lực.");
        if (offerId == Guid.Empty) throw new GameFault("VALIDATION_ERROR", "Thiếu mã đề nghị hòa.", 400);
        if (match.DrawOfferId != offerId)
            throw new GameFault("DRAW_OFFER_STALE", "Đề nghị hòa đã thay đổi hoặc hết hiệu lực.");

        var side = match.Players.Single(p => p.UserId == userId).Side;
        if (match.DrawOfferedBy == side)
            throw new GameFault("DRAW_OFFER_NOT_YOURS", "Bạn không thể từ chối đề nghị hòa của chính mình.");

        ClearDrawOffer(match);
        match.Version++;
        return await MatchView(match);
    });

    private static void EnsureDrawNegotiationAllowed(Match match)
    {
        if (match.Mode is "ai" or "pve")
            throw new GameFault("DRAW_NOT_AVAILABLE", "Không thể thương lượng hòa trong trận đấu với máy.");
    }

    public Task<object> End(Guid id, Guid userId, bool draw) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        if (match.Status == "finished") return await MatchView(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        var side = match.Players.Single(p => p.UserId == userId).Side;
        if (draw)
        {
            EnsureDrawNegotiationAllowed(match);
            ExpireDrawOfferIfNeeded(match, DateTimeOffset.UtcNow);
            if (match.DrawOfferId is not null && match.DrawOfferedBy != side)
                throw new GameFault("DRAW_OFFER_PENDING", "Đối thủ đã gửi đề nghị hòa; hãy phản hồi đề nghị đó.");
            else if (match.DrawOfferId is null)
            {
                var now = DateTimeOffset.UtcNow;
                match.DrawOfferId = Guid.NewGuid();
                match.DrawOfferedBy = side;
                match.DrawCreatedAt = now;
                match.DrawExpiresAt = now + DrawOfferDuration;
                match.Version++;
            }
        }
        else await Finish(match, side == "red" ? "black" : "red", "resignation");
        return await MatchView(match);
    });

    public async Task<object> History(Guid id, Guid userId)
    {
        var match = await LoadMatch(id, userId);
        var moves = await db.Moves.AsNoTracking().Where(m => m.MatchId == id).OrderBy(m => m.Ply)
            .Select(m => new { m.Ply, m.RequestId, m.Side, m.PieceId, m.FromCol, m.FromRow, m.ToCol, m.ToRow, m.CapturedPieceId, m.CreatedAt, m.BoardSnapshotJson }).ToListAsync();
        return new { match.InitialBoardJson, match.RuleSetVersion, match.CurrentBoardJson, moves };
    }

    public Task<ChatView> Chat(Guid roomId, Guid userId, ChatInput input) => Write(async () =>
    {
        var room = await LoadRoom(roomId); Member(room, userId);
        if (input.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(input.Content) || input.Content.Trim().Length > 500)
            throw new GameFault("VALIDATION_ERROR", "Tin nhắn từ 1 đến 500 ký tự.", 400);
        var old = await db.ChatMessages.Include(m => m.SenderUser).SingleOrDefaultAsync(m => m.RoomId == roomId && m.RequestId == input.RequestId);
        if (old is not null)
        {
            if (old.SenderUserId != userId || old.Text != input.Content.Trim()) throw new GameFault("REQUEST_CONFLICT", "Mã tin đã dùng.");
            return ChatViewOf(old);
        }
        var recent = await db.ChatMessages.Where(m => m.SenderUserId == userId).Select(m => m.CreatedAt).ToListAsync();
        if (recent.Count(t => t > DateTimeOffset.UtcNow.AddSeconds(-10)) >= 5)
            throw new GameFault("RATE_LIMITED", "Tối đa 5 tin trong 10 giây.", 429);
        var message = new ChatMessage { Id = Guid.NewGuid(), RoomId = roomId, SenderUserId = userId, RequestId = input.RequestId,
            Text = input.Content.Trim(), CreatedAt = DateTimeOffset.UtcNow, SenderUser = await db.Users.FindAsync(userId),
            Sequence = (await db.ChatMessages.Where(m => m.RoomId == roomId).MaxAsync(m => (long?)m.Sequence) ?? 0) + 1 };
        db.ChatMessages.Add(message); return ChatViewOf(message);
    });
    private static ChatView ChatViewOf(ChatMessage m) => new(m.Id, m.RoomId, m.SenderUserId, m.SenderUser?.DisplayName ?? "Kỳ thủ", m.Text, m.CreatedAt, m.Sequence);
    public Task<IReadOnlyList<RoomView>> Logout(Guid userId, string jti, DateTimeOffset expires) => Write<IReadOnlyList<RoomView>>(async () =>
    {
        if (!await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
            db.RevokedTokens.Add(new RevokedToken { Jti = jti, UserId = userId, RevokedAt = DateTimeOffset.UtcNow, ExpiresAt = expires });
        var ids = await db.RoomMemberships.Where(m => m.UserId == userId && m.LeftAt == null).Select(m => m.RoomId).ToListAsync();
        var result = new List<RoomView>();
        foreach (var id in ids)
        {
            var room = await LoadRoom(id); var member = room.Memberships.Single(m => m.UserId == userId && m.LeftAt is null);
            if (room.CurrentMatchId is Guid matchId)
            {
                var match = await LoadMatch(matchId, userId);
                if (match.Status == "playing") await Finish(match, member.Side == "red" ? "black" : "red", "resignation");
            }
            member.LeftAt = DateTimeOffset.UtcNow; member.IsConnected = false;
            foreach (var m in room.Memberships) m.IsReady = false;
            if (room.HostUserId == userId)
            {
                room.Status = "closed"; room.ClosedAt = DateTimeOffset.UtcNow;
                foreach (var m in room.Memberships.Where(m => m.LeftAt is null)) { m.LeftAt = DateTimeOffset.UtcNow; m.IsConnected = false; }
            }
            room.Version++; result.Add(View(room));
        }
        return result;
    });
    public async Task<IReadOnlyList<ChatView>> Messages(Guid roomId, Guid userId)
    {
        Member(await LoadRoom(roomId), userId);
        var rows = await db.ChatMessages.AsNoTracking().Include(m => m.SenderUser).Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.Sequence).Take(100).ToListAsync();
        return rows.OrderBy(m => m.Sequence).Select(ChatViewOf).ToArray();
    }
}
