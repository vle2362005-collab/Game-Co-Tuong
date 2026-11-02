using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Engine.AI;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Infrastructure.Game;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GameCoTuong.Foundation.Tests;

public sealed class PerformanceAcceptanceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GCT97_ten_rooms_and_AI_emit_measured_median_and_p95()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gct-performance-{Guid.NewGuid():N}.db");
        GameDbContext Db() => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite($"Data Source={path}").Options);
        async Task<T> Run<T>(Func<GameService, Task<T>> action) { await using var db = Db(); return await action(new GameService(db)); }
        var pairs = Enumerable.Range(0, 10).Select(_ => (Red: Guid.NewGuid(), Black: Guid.NewGuid())).ToArray();
        try
        {
            await using (var db = Db())
            {
                await db.Database.EnsureCreatedAsync();
                foreach (var id in pairs.SelectMany(p => new[] { p.Red, p.Black }))
                    db.Users.Add(new User { Id = id, Username = id.ToString("N"), NormalizedUsername = id.ToString("N"),
                        Email = id + "@test.local", NormalizedEmail = id + "@test.local", DisplayName = "Benchmark", PasswordHash = "test-only" });
                await db.SaveChangesAsync();
            }
            var rooms = new List<(Guid Match, Guid Red, Guid Black)>();
            foreach (var pair in pairs)
            {
                var room = await Run(s => s.Create(pair.Red, new(Guid.NewGuid(), "Performance room")));
                await Run(s => s.Join(pair.Black, room.Code));
                await Run(s => s.Ready(room.Id, pair.Red, true)); await Run(s => s.Ready(room.Id, pair.Black, true));
                room = await Run(s => s.Start(room.Id, pair.Red, new(Guid.NewGuid())));
                rooms.Add((room.MatchId!.Value, pair.Red, pair.Black));
            }
            var samples = new ConcurrentBag<double>();
            await Task.WhenAll(rooms.Select(async room =>
            {
                for (var ply = 0; ply < 10; ply++)
                {
                    var red = ply % 2 == 0; var outward = ply / 2 % 2 == 0;
                    var timer = Stopwatch.StartNew();
                    await Run(s => s.Move(room.Match, red ? room.Red : room.Black,
                        new MoveInput(Guid.NewGuid(), ply + 1, red ? "red-horse-1" : "black-horse-1",
                            outward ? 2 : 1, red ? (outward ? 7 : 9) : (outward ? 2 : 0))));
                    samples.Add(timer.Elapsed.TotalMilliseconds);
                }
            }));
            Assert.Equal(100, samples.Count);
            await using (var verify = Db()) Assert.Equal(100, await verify.Moves.CountAsync());
            var ai = new List<object>();
            foreach (var (name, depth, budget) in new[] { ("easy", 1, 300), ("medium", 2, 900), ("hard", 3, 1500) })
            {
                var times = new List<double>();
                for (var index = 0; index < 5; index++)
                {
                    var board = BoardInitializer.CreateInitialBoard(); var timer = Stopwatch.StartNew();
                    var result = Minimax.FindBestWithStats(board, Side.Red, depth, timeLimit: TimeSpan.FromMilliseconds(budget));
                    times.Add(timer.Elapsed.TotalMilliseconds);
                    Assert.NotNull(result.BestMove); Assert.True(XiangqiRules.IsLegal(board, Side.Red, result.BestMove));
                }
                ai.Add(new { difficulty = name, samples = times.Count, p50Ms = Percentile(times, .5), p95Ms = Percentile(times, .95) });
            }
            output.WriteLine("GCT97_JSON=" + JsonSerializer.Serialize(new {
                scope = "local GameService + SQLite; includes queue/context/JIT; excludes HTTP/LAN/browser",
                os = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(), logicalCpus = Environment.ProcessorCount,
                rooms = rooms.Count, samples = samples.Count, p50Ms = Percentile(samples, .5), p95Ms = Percentile(samples, .95), ai
            }));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
    }
    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var sorted = values.Order().ToArray();
        return Math.Round(sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1], 2);
    }
}
