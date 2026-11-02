using System.Diagnostics;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;

namespace GameCoTuong.Engine.AI;

public sealed record SearchResult(LegalMove? BestMove, int Score, long Nodes, TimeSpan Elapsed, int CompletedDepth);

public static class Minimax
{
    private const int Mate = 1_000_000;

    private sealed class SearchTimeoutException : Exception { }

    private sealed class SearchContext(bool alphaBeta, bool orderMoves, TimeSpan? timeLimit, CancellationToken cancellationToken)
    {
        public bool AlphaBeta { get; } = alphaBeta;
        public bool OrderMoves { get; } = orderMoves;
        public TimeSpan? TimeLimit { get; } = timeLimit;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public Stopwatch Timer { get; } = Stopwatch.StartNew();
        public long Nodes { get; set; }
        public int CompletedDepth { get; set; }
    }

    public static int Evaluate(Board board, Side perspective)
    {
        int score = 0;
        foreach (var piece in board)
        {
            var value = PieceValue(piece.Type);
            if (piece.Type == PieceType.Soldier && BoardGeometry.HasCrossedRiver(piece.Side, piece.Position)) value += 100;
            if (piece.Type != PieceType.General) value += 4 - Math.Abs(4 - piece.Position.Col);
            score += piece.Side == perspective ? value : -value;
        }
        return score;
    }

    public static LegalMove? FindBest(Board board, Side side, int depth = 2, TimeSpan? timeLimit = null,
        CancellationToken cancellationToken = default) =>
        FindBestWithStats(board, side, depth, alphaBeta: true, orderMoves: true,
            timeLimit: timeLimit ?? TimeSpan.FromSeconds(2), cancellationToken: cancellationToken).BestMove;

    public static SearchResult FindBestWithStats(Board board, Side side, int depth = 2,
        bool alphaBeta = true, bool orderMoves = true, TimeSpan? timeLimit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        depth = Math.Clamp(depth, 1, 3);
        if (timeLimit is { } budget && budget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeLimit));

        cancellationToken.ThrowIfCancellationRequested();
        var context = new SearchContext(alphaBeta, orderMoves, timeLimit, cancellationToken);
        var rootMoves = XiangqiRules.Generate(board, side);
        cancellationToken.ThrowIfCancellationRequested();
        if (rootMoves.Count == 0)
            return Result(null, -Mate, context);

        var bestMove = rootMoves[0];
        var bestScore = Evaluate(board.ApplyMove(bestMove.PieceId, bestMove.To, out _), side);

        for (var currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            try
            {
                var (move, score) = SearchRoot(board, side, currentDepth, rootMoves, context);
                bestMove = move;
                bestScore = score;
                context.CompletedDepth = currentDepth;
            }
            catch (SearchTimeoutException)
            {
                if (!timeLimit.HasValue) throw;
                break;
            }
        }

        return Result(bestMove, bestScore, context);
    }

    private static (LegalMove Move, int Score) SearchRoot(Board board, Side side, int depth,
        IReadOnlyList<LegalMove> rootMoves, SearchContext context)
    {
        var moves = context.OrderMoves ? OrderMoves(board, side, rootMoves, context) : rootMoves;
        var alpha = int.MinValue + 1;
        var bestScore = int.MinValue;
        var bestMove = moves[0];

        foreach (var move in moves)
        {
            CheckTime(context);
            var next = board.ApplyMove(move.PieceId, move.To, out _);
            var score = Search(next, XiangqiRules.Opponent(side), side, depth - 1,
                alpha, int.MaxValue, context);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
            if (context.AlphaBeta)
                alpha = Math.Max(alpha, bestScore);
        }

        return (bestMove, bestScore);
    }

    private static int Search(Board board, Side turn, Side root, int depth, int alpha, int beta,
        SearchContext context)
    {
        CheckTime(context);
        context.Nodes++;
        var moves = XiangqiRules.Generate(board, turn);
        if (moves.Count == 0) return turn == root ? -Mate - depth : Mate + depth;
        if (depth == 0) return Evaluate(board, root);
        if (context.OrderMoves) moves = OrderMoves(board, turn, moves, context);

        var maximizing = turn == root;
        var best = maximizing ? int.MinValue + 1 : int.MaxValue;
        foreach (var move in moves)
        {
            var score = Search(board.ApplyMove(move.PieceId, move.To, out _),
                XiangqiRules.Opponent(turn), root, depth - 1, alpha, beta, context);
            if (maximizing)
            {
                best = Math.Max(best, score);
                if (context.AlphaBeta) alpha = Math.Max(alpha, best);
            }
            else
            {
                best = Math.Min(best, score);
                if (context.AlphaBeta) beta = Math.Min(beta, best);
            }
            if (context.AlphaBeta && beta <= alpha) break;
        }
        return best;
    }

    private static IReadOnlyList<LegalMove> OrderMoves(Board board, Side side,
        IReadOnlyList<LegalMove> moves, SearchContext context)
    {
        var scoredMoves = new List<(LegalMove Move, int Priority)>(moves.Count);
        foreach (var move in moves)
        {
            CheckTime(context);
            var captured = board.GetPieceAt(move.To);
            var next = board.ApplyMove(move.PieceId, move.To, out _);
            var givesCheck = XiangqiRules.IsInCheck(next, XiangqiRules.Opponent(side));
            var priority = (givesCheck ? 1_000_000 : 0) +
                (captured is null ? 0 : 10_000 + PieceValue(captured.Type));
            scoredMoves.Add((move, priority));
        }

        return scoredMoves
            .OrderByDescending(item => item.Priority)
            .Select(item => item.Move)
            .ToArray();
    }

    private static int PieceValue(PieceType type) => type switch
    {
        PieceType.General => 10_000,
        PieceType.Chariot => 900,
        PieceType.Cannon => 450,
        PieceType.Horse => 400,
        PieceType.Advisor or PieceType.Elephant => 200,
        _ => 100
    };

    private static void CheckTime(SearchContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.TimeLimit is { } budget && context.Timer.Elapsed >= budget)
            throw new SearchTimeoutException();
    }

    private static SearchResult Result(LegalMove? move, int score, SearchContext context) =>
        new(move, score, context.Nodes, context.Timer.Elapsed, context.CompletedDepth);
}
