using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Game;

// Single API process for this student demo. Serializable transactions + database indexes
// protect seats and move sequence. Scale-out needs a distributed lock/presence store.
public sealed class GameService(GameDbContext db)
{
    public static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.Status != "closed")
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

    public Task<RoomView> Presence(Guid roomId, Guid userId, bool connected) => Write(async () =>
    {
        var room = await LoadRoom(roomId); var member = Member(room, userId);
        member.IsConnected = connected;
        if (!connected) foreach (var m in room.Memberships) m.IsReady = false;
        room.Version++; return View(room);
    });

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
        var match = await db.Matches.Include(m => m.Players).SingleOrDefaultAsync(m => m.Id == id)
            ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);
        if (!match.Players.Any(p => p.UserId == userId)) throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
        return match;
    }

    public async Task<object> MatchSnapshot(Guid id, Guid userId) => MatchView(await LoadMatch(id, userId));
    private static object MatchView(Match match) => new
    {
        match.Id, match.RoomId, match.Status, match.Version, match.MoveNumber, match.SideToMove,
        pieces = BoardSerializer.Deserialize(match.CurrentBoardJson).ToArray(), match.Winner, match.ResultReason,
        check = XiangqiRules.IsInCheck(BoardSerializer.Deserialize(match.CurrentBoardJson), ParseSide(match.SideToMove)),
        players = match.Players.Select(p => new { p.UserId, p.Side }), match.DrawOfferedBy
    };
    private static Side ParseSide(string side) => side == "red" ? Side.Red : Side.Black;

    public Task<object> Move(Guid id, Guid userId, MoveInput input) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        if (input.RequestId == Guid.Empty) throw new GameFault("VALIDATION_ERROR", "Thiếu mã nước.", 400);
        var previous = await db.Moves.SingleOrDefaultAsync(m => m.MatchId == id && m.RequestId == input.RequestId);
        if (previous is not null)
        {
            var playerSide = match.Players.Single(p => p.UserId == userId).Side;
            if (previous.Side != playerSide || previous.PieceId != input.PieceId || previous.ToCol != input.ToCol || previous.ToRow != input.ToRow)
                throw new GameFault("REQUEST_CONFLICT", "Mã nước đã được sử dụng.");
            return MatchView(match);
        }
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận không cho phép đi quân.");
        if (match.Version != input.ExpectedVersion) throw new GameFault("VERSION_CONFLICT", "Bàn cờ đã thay đổi, hãy tải lại.");
        if (match.Players.Single(p => p.UserId == userId).Side != match.SideToMove)
            throw new GameFault("NOT_YOUR_TURN", "Chưa đến lượt của bạn.", 403);
        if (!Position.IsValid(input.ToCol, input.ToRow)) throw new GameFault("INVALID_MOVE", "Tọa độ ngoài bàn cờ.", 400);
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        var move = new LegalMove(input.PieceId, new Position(input.ToCol, input.ToRow));
        if (!XiangqiRules.IsLegal(board, ParseSide(match.SideToMove), move)) throw new GameFault("INVALID_MOVE", "Nước đi không hợp lệ.", 400);
        var piece = board.GetPieceById(input.PieceId)!;
        var next = board.ApplyMove(input.PieceId, move.To, out var captured);
        db.Moves.Add(new Move { Id = Guid.NewGuid(), MatchId = id, RequestId = input.RequestId, Ply = ++match.MoveNumber,
            MatchVersion = ++match.Version, Side = match.SideToMove, PieceId = piece.Id, FromCol = piece.Position.Col,
            FromRow = piece.Position.Row, ToCol = input.ToCol, ToRow = input.ToRow, CapturedPieceId = captured?.Id,
            BoardSnapshotJson = BoardSerializer.Serialize(next), CreatedAt = DateTimeOffset.UtcNow });
        match.CurrentBoardJson = BoardSerializer.Serialize(next);
        match.SideToMove = match.SideToMove == "red" ? "black" : "red";
        match.DrawOfferedBy = null;
        var result = XiangqiRules.Result(next, ParseSide(match.SideToMove));
        if (result.Finished) await Finish(match, result.Winner == Side.Red ? "red" : "black", result.Reason!);
        return MatchView(match);
    });

    private async Task Finish(Match match, string? winner, string reason)
    {
        if (match.Status == "finished") return;
        match.Status = "finished"; match.Winner = winner; match.ResultReason = reason;
        match.EndedAt = DateTimeOffset.UtcNow; match.Version++; match.DrawOfferedBy = null;
        var room = await LoadRoom(match.RoomId); room.Status = "finished"; room.Version++;
    }

    public Task<object> End(Guid id, Guid userId, bool draw) => Write(async () =>
    {
        var match = await LoadMatch(id, userId);
        if (match.Status == "finished") return MatchView(match);
        if (match.Status != "playing") throw new GameFault("MATCH_NOT_PLAYING", "Trận đang tạm dừng.");
        var side = match.Players.Single(p => p.UserId == userId).Side;
        if (draw)
        {
            if (match.DrawOfferedBy is not null && match.DrawOfferedBy != side) await Finish(match, null, "draw-agreement");
            else match.DrawOfferedBy = side;
        }
        else await Finish(match, side == "red" ? "black" : "red", "resignation");
        return MatchView(match);
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
