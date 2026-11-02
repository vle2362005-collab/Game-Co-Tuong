using System.Data;
using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
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
/// Human always plays Red; bot always plays Black.
/// Uses the same WriteGate semaphore as GameService for serialized DB writes.
/// </summary>
public sealed class BotGameService(GameDbContext db, ILogger<BotGameService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    // Minimax search depth per difficulty
    private static int Depth(string difficulty) => difficulty.ToLowerInvariant() switch
    {
        "easy" => 1,
        "hard" => 3,
        _ => 2  // medium (default)
    };

    // The bot user's well-known sentinel ID (never in Users table)
    private static readonly Guid BotUserId = new("00000000-0000-0000-0000-000000000001");

    // ── CREATE ────────────────────────────────────────────────────────────────

    /// <summary>Creates a new PvE match immediately (no lobby room needed).</summary>
    public async Task<object> CreatePveMatch(Guid userId, string difficulty, Guid requestId)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Idempotency: if same requestId was already processed, return cached response
            var receipt = await db.CommandReceipts
                .FirstOrDefaultAsync(r => r.UserId == userId && r.RequestId == requestId);
            if (receipt is not null)
                return JsonSerializer.Deserialize<object>(receipt.ResponseBody, JsonOpts)!;

            var now = DateTimeOffset.UtcNow;
            var board = BoardInitializer.CreateInitialBoard();
            var boardJson = BoardSerializer.Serialize(board);

            // Create a private placeholder room for the PvE match
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Code = "000000",
                Name = $"PvE-{difficulty}",
                HostUserId = userId,
                Status = "playing",
                CreatedAt = now,
                LastActivityAt = now
            };
            db.Rooms.Add(room);

            var match = new Match
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                Mode = "pve",
                InitialBoardJson = boardJson,
                CurrentBoardJson = boardJson,
                StartedAt = now,
                TurnStartedAt = now,
                RedRemainingMs = 0,
                BlackRemainingMs = 0
            };
            match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = userId, Side = "red" });
            match.Players.Add(new MatchPlayer { Id = Guid.NewGuid(), UserId = BotUserId, Side = "black" });
            db.Matches.Add(match);

            // Save match first so the FK from room → match resolves
            await db.SaveChangesAsync();
            room.CurrentMatchId = match.Id;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var result = BuildView(match, board);

            // Store idempotency receipt
            var body = JsonSerializer.Serialize(result, JsonOpts);
            db.CommandReceipts.Add(new CommandReceipt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestId = requestId,
                Operation = "pve-create",
                TargetId = match.Id,
                Fingerprint = difficulty,
                ResponseBody = body,
                StatusCode = 200,
                CreatedAt = now
            });
            await db.SaveChangesAsync();

            logger.LogInformation("PvE match created. {MatchId} {UserId} {Difficulty}", match.Id, userId, difficulty);
            return result;
        }
        finally { GameService.WriteGate.Release(); }
    }

    // ── GET ───────────────────────────────────────────────────────────────────

    /// <summary>Returns a snapshot of the PvE match for the human player.</summary>
    public async Task<object> GetPveMatch(Guid matchId, Guid userId)
    {
        var match = await db.Matches
            .Include(m => m.Players)
            .FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

        EnsurePve(match, userId);
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        var lastMove = await db.Moves.AsNoTracking()
            .Where(m => m.MatchId == matchId)
            .OrderByDescending(m => m.Ply)
            .FirstOrDefaultAsync();
        return BuildView(match, board, lastMove);
    }

    // ── HUMAN MOVE ────────────────────────────────────────────────────────────

    /// <summary>Applies the human (Red) player's move.</summary>
    public async Task<object> HumanMove(Guid matchId, Guid userId, MoveInput input)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches.Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId)
                ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

            EnsurePve(match, userId);
            if (match.Status != "playing")
                throw new GameFault("MATCH_NOT_PLAYING", "Trận đã kết thúc.", 409);
            if (match.SideToMove != "red")
                throw new GameFault("NOT_YOUR_TURN", "Chưa đến lượt của bạn.", 403);
            if (match.Version != input.ExpectedVersion)
                throw new GameFault("VERSION_CONFLICT", "Bàn cờ đã thay đổi, hãy tải lại.", 409);

            // Idempotency
            var prev = await db.Moves
                .FirstOrDefaultAsync(m => m.MatchId == matchId && m.RequestId == input.RequestId);
            if (prev is not null)
                return await GetPveMatch(matchId, userId);

            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            var move = new LegalMove(input.PieceId, new GameCoTuong.Domain.ValueObjects.Position(input.ToCol, input.ToRow));
            if (!XiangqiRules.IsLegal(board, GameCoTuong.Domain.Enums.Side.Red, move))
                throw new GameFault("INVALID_MOVE", "Nước đi không hợp lệ.", 400);

            var piece = board.GetPieceById(input.PieceId)!;
            var next = board.ApplyMove(input.PieceId, move.To, out var captured);
            var moveEntity = new Move
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
            };
            db.Moves.Add(moveEntity);
            match.CurrentBoardJson = BoardSerializer.Serialize(next);
            match.SideToMove = "black";

            // Check if black has no legal moves after red's move (red wins)
            var result = XiangqiRules.Result(next, GameCoTuong.Domain.Enums.Side.Black);
            if (result.Finished) await FinishMatch(match, result.Winner == GameCoTuong.Domain.Enums.Side.Red ? "red" : "black", result.Reason!);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return BuildView(match, next, moveEntity);
        }
        finally { GameService.WriteGate.Release(); }
    }

    // ── BOT MOVE ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes one bot (Black) move using Minimax.
    /// Called asynchronously from PveEndpoints after the human move response is sent.
    /// </summary>
    public async Task<object?> BotMove(Guid matchId, string difficulty)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches.Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match is null || match.Status != "playing" || match.Mode != "pve")
                return null;
            if (match.SideToMove != "black")
                return null; // Already red's turn (concurrent call guard)

            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            var bestMove = Minimax.FindBest(board, GameCoTuong.Domain.Enums.Side.Black, Depth(difficulty));

            if (bestMove is null)
            {
                // Bot has no legal moves → red wins
                await FinishMatch(match, "red", "checkmate");
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                var finalBoard = BoardSerializer.Deserialize(match.CurrentBoardJson);
                return BuildView(match, finalBoard);
            }

            var piece = board.GetPieceById(bestMove.PieceId)!;
            var next = board.ApplyMove(bestMove.PieceId, bestMove.To, out var captured);
            var moveEntity = new Move
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
            };
            db.Moves.Add(moveEntity);
            match.CurrentBoardJson = BoardSerializer.Serialize(next);
            match.SideToMove = "red";

            // Check if red has no legal moves after bot's move (black wins)
            var result = XiangqiRules.Result(next, GameCoTuong.Domain.Enums.Side.Red);
            if (result.Finished) await FinishMatch(match, result.Winner == GameCoTuong.Domain.Enums.Side.Red ? "red" : "black", result.Reason!);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            logger.LogInformation("Bot moved. {MatchId} ({PieceId} → {Col},{Row})", matchId, piece.Id, bestMove.To.Col, bestMove.To.Row);
            return BuildView(match, next, moveEntity);
        }
        finally { GameService.WriteGate.Release(); }
    }

    // ── SURRENDER ─────────────────────────────────────────────────────────────

    /// <summary>Human surrenders the PvE match.</summary>
    public async Task<object> HumanSurrender(Guid matchId, Guid userId)
    {
        await GameService.WriteGate.WaitAsync();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var match = await db.Matches.Include(m => m.Players)
                .FirstOrDefaultAsync(m => m.Id == matchId)
                ?? throw new GameFault("MATCH_NOT_FOUND", "Không tìm thấy trận.", 404);

            EnsurePve(match, userId);
            if (!IsTerminal(match.Status))
                await FinishMatch(match, "black", "resignation");

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            return BuildView(match, board);
        }
        finally { GameService.WriteGate.Release(); }
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private static void EnsurePve(Match match, Guid userId)
    {
        if (match.Mode != "pve")
            throw new GameFault("NOT_PVE_MATCH", "Không phải trận đấu với máy.", 400);
        if (!match.Players.Any(p => p.UserId == userId && p.Side == "red"))
            throw new GameFault("NOT_MATCH_PLAYER", "Bạn không tham gia trận này.", 403);
    }

    private async Task FinishMatch(Match match, string? winner, string reason)
    {
        if (IsTerminal(match.Status)) return;
        match.Status = "finished";
        match.Winner = winner;
        match.ResultReason = reason;
        match.EndedAt = DateTimeOffset.UtcNow;
        match.Version++;
        var room = await db.Rooms.FindAsync(match.RoomId);
        if (room is not null) { room.Status = "finished"; room.Version++; }
    }

    private static bool IsTerminal(string status) =>
        status is "finished" or "cancelled" or "completed";

    private static object? BuildResultView(Match match)
    {
        if (!IsTerminal(match.Status)) return null;
        return new
        {
            status = match.Status,
            match.Winner,
            outcome = match.Winner == "red" ? "red-win" : match.Winner == "black" ? "black-win" : "draw",
            reasonCode = match.ResultReason,
            reason = match.ResultReason,
            endedAt = match.EndedAt
        };
    }

    private static object BuildView(Match match, Board board, Move? lastMove = null)
    {
        var side = match.SideToMove == "red"
            ? GameCoTuong.Domain.Enums.Side.Red
            : GameCoTuong.Domain.Enums.Side.Black;
        var isCheck = XiangqiRules.IsInCheck(board, side);
        IReadOnlyList<LegalMove> legal = match.Status == "playing" && match.SideToMove == "red"
            ? XiangqiRules.Generate(board, GameCoTuong.Domain.Enums.Side.Red)
            : Array.Empty<LegalMove>();

        return new
        {
            match.Id,
            match.RoomId,
            match.Mode,
            match.Status,
            match.Version,
            match.MoveNumber,
            match.SideToMove,
            pieces = board.Select(p => new
            {
                p.Id,
                type = p.Type.ToString().ToLowerInvariant(),
                side = p.Side.ToString().ToLowerInvariant(),
                position = new { col = p.Position.Col, row = p.Position.Row }
            }),
            match.Winner,
            match.ResultReason,
            result = BuildResultView(match),
            check = isCheck,
            legalMoves = legal.Select(m => new { pieceId = m.PieceId, to = new { col = m.To.Col, row = m.To.Row } }),
            lastMove = lastMove is null ? null : (object)new
            {
                lastMove.RequestId, lastMove.Ply, lastMove.Side, lastMove.PieceId,
                lastMove.FromCol, lastMove.FromRow, lastMove.ToCol, lastMove.ToRow, lastMove.CapturedPieceId
            },
            players = match.Players.Select(p => new { p.UserId, p.Side }),
            playerSide = "red"
        };
    }
}
