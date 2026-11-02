using System.Data;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Engine.AI;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameCoTuong.Infrastructure.Game;

/// <summary>
/// Handles PvE (player vs bot) match creation and bot moves for GCT-113.
/// The bot always plays as Black; the human player plays as Red.
/// </summary>
public sealed class BotGameService(GameDbContext db, ILogger<BotGameService> logger)
{
    // Difficulty → Minimax search depth
    private static int DepthForDifficulty(string difficulty) => difficulty.ToLowerInvariant() switch
    {
        "easy" => 1,
        "hard" => 3,
        _ => 2  // medium (default)
    };

    /// <summary>Creates a new PvE match immediately (no room needed).</summary>
    public Task<object> CreatePveMatch(Guid userId, string difficulty, Guid requestId) =>
        GameService.WriteGate.WaitAsync().ContinueWith(async _ =>
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                // Idempotency: return existing match if same requestId was already used
                var existing = await db.Matches
                    .Include(m => m.Players)
                    .FirstOrDefaultAsync(m => m.Players.Any(p => p.UserId == userId)
                                              && m.Mode == "pve"
                                              && m.Status == "playing"
                                              && m.Players.Any(p => p.UserId == userId));
                // Check command receipt for idempotency
                var receipt = await db.CommandReceipts.FirstOrDefaultAsync(r => r.UserId == userId && r.RequestId == requestId);
                if (receipt is not null)
                    return (object)System.Text.Json.JsonSerializer.Deserialize<object>(receipt.ResponseBody,
                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

                // Create a placeholder room for the PvE match (needed by Match FK)
                var now = DateTimeOffset.UtcNow;
                var botId = GetBotId();
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    Code = "000000",
                    Name = $"Bot-{difficulty}",
                    HostUserId = userId,
                    Status = "playing",
                    CreatedAt = now,
                    LastActivityAt = now
                };
                // Add human membership
                var humanMember = new RoomMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Side = "red",
                    JoinedAt = now,
                    IsConnected = true,
                    IsReady = true,
                    User = await db.Users.FindAsync(userId)
                };
                room.Memberships.Add(humanMember);
                db.Rooms.Add(room);

                string board = BoardSerializer.Serialize(BoardInitializer.CreateInitialBoard());
                var match = new Match
                {
                    Id = Guid.NewGuid(),
                    RoomId = room.Id,
                    Mode = "pve",
                    InitialBoardJson = board,
                    CurrentBoardJson = board,
                    StartedAt = now,
                    TurnStartedAt = now,
                    RedRemainingMs = 0,
                    BlackRemainingMs = 0
                };
                match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = userId, Side = "red" });
                match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = botId, Side = "black" });
                db.Matches.Add(match);

                // Resolve circular FK in two saves
                await db.SaveChangesAsync();
                room.CurrentMatchId = match.Id;
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                var result = await PveMatchView(match, userId);
                // Store receipt
                db.CommandReceipts.Add(new CommandReceipt
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RequestId = requestId,
                    Operation = "create-pve",
                    Fingerprint = difficulty,
                    ResponseBody = System.Text.Json.JsonSerializer.Serialize(result,
                        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                    StatusCode = 200,
                    CreatedAt = now
                });
                await db.SaveChangesAsync();
                logger.LogInformation("PvE match created. {MatchId} {UserId} {Difficulty}", match.Id, userId, difficulty);
                return result;
            }
            finally { GameService.WriteGate.Release(); }
        }, TaskScheduler.Default).Unwrap();

    /// <summary>
    /// Executes the bot's move for a PvE match. Called by BotMoveHostedService.
    /// Returns the updated match snapshot, or null if the match is already over.
    /// </summary>
    public async Task<object?> BotMove(Guid matchId, string difficulty)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches
                .Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match is null || match.Status != "playing" || match.Mode != "pve")
                return null;

            if (match.SideToMove != "black")
                return null; // Not bot's turn

            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            var depth = DepthForDifficulty(difficulty);
            var bestMove = Minimax.FindBest(board, Side.Black, depth);

            if (bestMove is null)
            {
                // Bot has no legal moves → stalemate/checkmate, player wins
                await Finish(match, "red", "checkmate");
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return await PveMatchView(match, GetHumanId(match));
            }

            var piece = board.GetPieceById(bestMove.PieceId)!;
            var next = board.ApplyMove(bestMove.PieceId, bestMove.To, out var captured);
            db.Moves.Add(new Move
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                RequestId = Guid.NewGuid(),
                Ply = ++match.MoveNumber,
                MatchVersion = ++match.Version,
                Side = "black",
                PieceId = piece.Id,
                FromCol = piece.Position.Col,
                FromRow = piece.Position.Row,
                ToCol = bestMove.To.Col,
                ToRow = bestMove.To.Row,
                CapturedPieceId = captured?.Id,
                BoardSnapshotJson = BoardSerializer.Serialize(next),
                CreatedAt = DateTimeOffset.UtcNow
            });
            match.CurrentBoardJson = BoardSerializer.Serialize(next);
            match.SideToMove = "red";

            var result = XiangqiRules.Result(next, Side.Red);
            if (result.Finished)
                await Finish(match, result.Winner == Side.Red ? "red" : "black", result.Reason!);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            logger.LogInformation("Bot moved. {MatchId} {Move}", matchId, $"{piece.Id}→({bestMove.To.Col},{bestMove.To.Row})");
            return await PveMatchView(match, GetHumanId(match));
        }
        finally { GameService.WriteGate.Release(); }
    }

    /// <summary>Returns a PvE match snapshot for a player. Used by GET /api/v1/pve/matches/{id}.</summary>
    public async Task<object> GetPveMatch(Guid matchId, Guid userId)
    {
        var match = await db.Matches.Include(m => m.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);
        if (match.Mode != "pve")
            throw new GameFault("NOT_PVE_MATCH", "Không phải trận đấu với máy.", 400);
        if (!match.Players.Any(p => p.UserId == userId && p.Side == "red"))
            throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
        return await PveMatchView(match, userId);
    }

    /// <summary>Human player moves in a PvE match.</summary>
    public async Task<object> HumanMove(Guid matchId, Guid userId, MoveInput input)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches.Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId)
                ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

            if (match.Mode != "pve")
                throw new GameFault("NOT_PVE_MATCH", "Không phải trận đấu với máy.", 400);
            if (!match.Players.Any(p => p.UserId == userId && p.Side == "red"))
                throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
            if (match.Status != "playing")
                throw new GameFault("MATCH_NOT_PLAYING", "Trận đã kết thúc.", 409);
            if (match.SideToMove != "red")
                throw new GameFault("NOT_YOUR_TURN", "Chưa đến lượt của bạn.", 403);
            if (match.Version != input.ExpectedVersion)
                throw new GameFault("VERSION_CONFLICT", "Bàn cờ đã thay đổi, hãy tải lại.", 409);
            if (string.IsNullOrWhiteSpace(input.PieceId))
                throw new GameFault("VALIDATION_ERROR", "Thiếu mã quân cờ.", 400);

            // Idempotency
            var prev = await db.Moves.FirstOrDefaultAsync(m => m.MatchId == matchId && m.RequestId == input.RequestId);
            if (prev is not null) return await PveMatchView(match, userId);

            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            var legalMove = new LegalMove(input.PieceId, new Position(input.ToCol, input.ToRow));
            if (!XiangqiRules.IsLegal(board, Side.Red, legalMove))
                throw new GameFault("INVALID_MOVE", "Nước đi không hợp lệ.", 400);

            var piece = board.GetPieceById(input.PieceId)!;
            var next = board.ApplyMove(input.PieceId, legalMove.To, out var captured);
            db.Moves.Add(new Move
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                RequestId = input.RequestId,
                Ply = ++match.MoveNumber,
                MatchVersion = ++match.Version,
                Side = "red",
                PieceId = piece.Id,
                FromCol = piece.Position.Col,
                FromRow = piece.Position.Row,
                ToCol = input.ToCol,
                ToRow = input.ToRow,
                CapturedPieceId = captured?.Id,
                BoardSnapshotJson = BoardSerializer.Serialize(next),
                CreatedAt = DateTimeOffset.UtcNow
            });
            match.CurrentBoardJson = BoardSerializer.Serialize(next);
            match.SideToMove = "black";

            var result = XiangqiRules.Result(next, Side.Black);
            if (result.Finished)
                await Finish(match, result.Winner == Side.Red ? "red" : "black", result.Reason!);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return await PveMatchView(match, userId);
        }
        finally { GameService.WriteGate.Release(); }
    }

    /// <summary>Human surrenders in a PvE match.</summary>
    public async Task<object> HumanSurrender(Guid matchId, Guid userId)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches.Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId)
                ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

            if (match.Mode != "pve")
                throw new GameFault("NOT_PVE_MATCH", "Không phải trận đấu với máy.", 400);
            if (!match.Players.Any(p => p.UserId == userId && p.Side == "red"))
                throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
            if (!IsTerminalStatus(match.Status))
                await Finish(match, "black", "resignation");

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return await PveMatchView(match, userId);
        }
        finally { GameService.WriteGate.Release(); }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<object> PveMatchView(Match match, Guid userId)
    {
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        var turn = match.SideToMove == "red" ? Side.Red : Side.Black;
        var isCheck = XiangqiRules.IsInCheck(board, turn);
        var last = db.Moves.Local.FirstOrDefault(m => m.MatchId == match.Id && m.Ply == match.MoveNumber)
            ?? await db.Moves.AsNoTracking().Where(m => m.MatchId == match.Id)
                              .OrderByDescending(m => m.Ply).FirstOrDefaultAsync();

        return new
        {
            match.Id,
            match.RoomId,
            match.Mode,
            match.Status,
            match.Version,
            match.MoveNumber,
            match.SideToMove,
            pieces = board.ToArray(),
            match.Winner,
            match.ResultReason,
            reasonCode = GetReasonCode(match.ResultReason),
            result = GetResult(match),
            check = isCheck,
            legalMoves = match.Status == "playing" && match.SideToMove == "red"
                ? XiangqiRules.Generate(board, Side.Red)
                : Array.Empty<LegalMove>(),
            lastMove = last is null ? null : new
            {
                last.RequestId, last.Ply, last.Side, last.PieceId,
                last.FromCol, last.FromRow, last.ToCol, last.ToRow, last.CapturedPieceId
            },
            players = match.Players.Select(p => new { p.UserId, p.Side }),
            playerSide = "red"
        };
    }

    private async Task Finish(Match match, string? winner, string reason)
    {
        if (IsTerminalStatus(match.Status)) return;
        match.Status = "finished";
        match.Winner = winner;
        match.ResultReason = reason;
        match.EndedAt = DateTimeOffset.UtcNow;
        match.Version++;
        var room = await db.Rooms.FindAsync(match.RoomId);
        if (room is not null) { room.Status = "finished"; room.Version++; }
    }

    private static bool IsTerminalStatus(string status) =>
        status is "finished" or "cancelled" or "completed" or "ended";

    private static string? GetReasonCode(string? reason) => reason?.ToLowerInvariant() switch
    {
        "resignation" or "surrender" => "surrender",
        "checkmate" => "checkmate",
        "stalemate" => "stalemate",
        _ => reason
    };

    private static object? GetResult(Match match)
    {
        if (!IsTerminalStatus(match.Status)) return null;
        return new
        {
            match.Status,
            match.Winner,
            outcome = match.Winner == "red" ? "red-win" : match.Winner == "black" ? "black-win" : "draw",
            reasonCode = GetReasonCode(match.ResultReason) ?? "unknown",
            reason = match.ResultReason,
            endedAt = match.EndedAt
        };
    }

    private static Guid GetBotId() => new("00000000-0000-0000-0000-000000000001");

    private static Guid GetHumanId(Match match) =>
        match.Players.First(p => p.UserId != GetBotId()).UserId;
}
