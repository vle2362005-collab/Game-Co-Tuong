using System.Diagnostics;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;

namespace GameCoTuong.Engine.AI;

public static class Minimax
{
    private const int Mate = 1_000_000;
    private sealed class SearchTimeoutException : Exception { }

    public static int Evaluate(Board board, Side perspective)
    {
        int score = 0;
        foreach (var p in board)
        {
            int value = p.Type switch
            {
                PieceType.General => 10000, PieceType.Chariot => 900, PieceType.Cannon => 450,
                PieceType.Horse => 400, PieceType.Advisor or PieceType.Elephant => 200, _ => 100
            };
            if (p.Type == PieceType.Soldier && BoardGeometry.HasCrossedRiver(p.Side, p.Position)) value += 100;
            if (p.Type != PieceType.General) value += 4 - Math.Abs(4 - p.Position.Col);
            score += p.Side == perspective ? value : -value;
        }
        return score;
    }

    public static LegalMove? FindBest(Board board, Side side, int depth = 2, TimeSpan? timeLimit = null)
    {
        depth = Math.Clamp(depth, 1, 3);
        var budget = timeLimit ?? TimeSpan.FromSeconds(2);
        if (budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeLimit));

        var timer = Stopwatch.StartNew();
        var moves = XiangqiRules.Generate(board, side);
        if (moves.Count == 0) return null;

        var best = moves[0];
        for (var currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            var iterationBest = best;
            var bestScore = int.MinValue;
            try
            {
                foreach (var move in moves)
                {
                    CheckTime(timer, budget);
                    var next = board.ApplyMove(move.PieceId, move.To, out _);
                    var score = Search(next, XiangqiRules.Opponent(side), side, currentDepth - 1,
                        int.MinValue + 1, int.MaxValue, timer, budget);
                    if (score > bestScore) { bestScore = score; iterationBest = move; }
                }
                best = iterationBest;
            }
            catch (SearchTimeoutException)
            {
                break;
            }
        }
        return best;
    }

    private static int Search(Board board, Side turn, Side root, int depth, int alpha, int beta,
        Stopwatch timer, TimeSpan budget)
    {
        CheckTime(timer, budget);
        var moves = XiangqiRules.Generate(board, turn);
        if (moves.Count == 0) return turn == root ? -Mate - depth : Mate + depth;
        if (depth == 0) return Evaluate(board, root);
        int best = turn == root ? int.MinValue + 1 : int.MaxValue;
        foreach (var move in moves)
        {
            int score = Search(board.ApplyMove(move.PieceId, move.To, out _), XiangqiRules.Opponent(turn),
                root, depth - 1, alpha, beta, timer, budget);
            if (turn == root) { best = Math.Max(best, score); alpha = Math.Max(alpha, best); }
            else { best = Math.Min(best, score); beta = Math.Min(beta, best); }
            if (beta <= alpha) break;
        }
        return best;
    }

    private static void CheckTime(Stopwatch timer, TimeSpan budget)
    {
        if (timer.Elapsed >= budget) throw new SearchTimeoutException();
    }
}
