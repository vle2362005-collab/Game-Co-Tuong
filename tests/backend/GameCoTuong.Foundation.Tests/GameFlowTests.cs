using System.Text.Json;
using GameCoTuong.Application.Game;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Domain.Enums;
using GameCoTuong.Domain.ValueObjects;
using GameCoTuong.Infrastructure.Game;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GameCoTuong.Engine.Serialization;
using GameCoTuong.Engine.Model;
using GameCoTuong.Engine.Rules;
using GameCoTuong.Api.Hubs;

namespace GameCoTuong.Foundation.Tests;

public sealed class GameFlowTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"gct-flow-{Guid.NewGuid():N}.db");
    private readonly Guid red = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid black = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid outsider = Guid.NewGuid();
    private GameDbContext Db() => new(new DbContextOptionsBuilder<GameDbContext>().UseSqlite($"Data Source={path}").Options);
    public GameFlowTests()
    {
        using var db = Db(); db.Database.EnsureCreated();
        db.Users.Add(new User { Id = outsider, Username = "outsider", NormalizedUsername = "outsider", Email = "out@test.local", NormalizedEmail = "out@test.local", DisplayName = "Outsider", PasswordHash = "test" }); db.SaveChanges();
    }
    private async Task<T> Run<T>(Func<GameService, Task<T>> action) { await using var db = Db(); return await action(new GameService(db)); }
    private async Task<(RoomView Room, Guid Match)> Start()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(),"Test room")));
        await Run(s => s.Join(black, room.Code));
        await Run(s => s.Presence(room.Id,red,true)); await Run(s => s.Presence(room.Id,black,true));
        await Run(s => s.Ready(room.Id,red,true)); await Run(s => s.Ready(room.Id,black,true));
        room = await Run(s => s.Start(room.Id,red,new(Guid.NewGuid())));
        return (room,room.MatchId!.Value);
    }
    private static Piece P(string id, PieceType type, Side side, int col, int row) => new(id, type, side, new(col, row));
    private static string PositionKey(Board board, Side sideToMove) =>
        $"{sideToMove}|" + string.Join("|", board.OrderBy(p => p.Type).ThenBy(p => p.Side)
            .ThenBy(p => p.Position.Col).ThenBy(p => p.Position.Row)
            .Select(p => $"{p.Type}:{p.Side}:{p.Position.Col}:{p.Position.Row}"));

    private async Task<IReadOnlyList<Board>> PlaySequence(
        Guid matchId,
        Board initial,
        params (string PieceId, Side Side, int Col, int Row)[] moves)
    {
        var initialJson = BoardSerializer.Serialize(initial);
        await using (var db = Db())
        {
            var match = await db.Matches.SingleAsync(m => m.Id == matchId);
            match.InitialBoardJson = initialJson;
            match.CurrentBoardJson = initialJson;
            match.SideToMove = "red";
            match.MoveNumber = 0;
            match.Version = 1;
            await db.SaveChangesAsync();
        }

        var boards = new List<Board> { initial };
        foreach (var next in moves)
        {
            await using var db = Db();
            var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
            Assert.Equal(next.Side.ToString().ToLowerInvariant(), match.SideToMove);
            var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
            var legalMove = new LegalMove(next.PieceId, new(next.Col, next.Row));
            Assert.True(XiangqiRules.IsLegal(board, next.Side, legalMove),
                $"Expected legal move: {next.Side} {next.PieceId} to {next.Col},{next.Row}");

            var playerId = next.Side == Side.Red ? red : black;
            await Run(s => s.Move(matchId, playerId,
                new MoveInput(Guid.NewGuid(), match.Version, next.PieceId, next.Col, next.Row)));

            await using var afterDb = Db();
            var after = await afterDb.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
            boards.Add(BoardSerializer.Deserialize(after.CurrentBoardJson));
        }

        return boards;
    }

    private async Task AssertRepeatedMatchContinues(Guid matchId, Board initial, IReadOnlyList<Board> positions)
    {
        var initialKey = PositionKey(initial, Side.Red);
        Assert.Equal(3, positions.Select((board, index) =>
                PositionKey(board, index % 2 == 0 ? Side.Red : Side.Black))
            .Count(key => key == initialKey));

        await using var db = Db();
        var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        var moves = await db.Moves.AsNoTracking().Where(m => m.MatchId == matchId).OrderBy(m => m.Ply).ToListAsync();
        Assert.Equal("playing", match.Status);
        Assert.Equal("red", match.SideToMove);
        Assert.Null(match.Winner);
        Assert.Null(match.ResultReason);
        Assert.Equal(moves.Count, match.MoveNumber);

        var replay = BoardSerializer.Deserialize(match.InitialBoardJson);
        foreach (var move in moves)
            replay = replay.ApplyMove(move.PieceId, new(move.ToCol, move.ToRow), out _);
        Assert.Equal(PositionKey(replay, Side.Red), PositionKey(BoardSerializer.Deserialize(match.CurrentBoardJson), Side.Red));
    }

    [Fact]
    public async Task GCT112_three_occurrences_of_same_position_do_not_auto_draw()
    {
        var (_, matchId) = await Start();
        var board = new Board(new[]
        {
            P("red-general-1", PieceType.General, Side.Red, 5, 9),
            P("black-general-1", PieceType.General, Side.Black, 3, 0),
            P("red-chariot-1", PieceType.Chariot, Side.Red, 0, 8),
            P("black-chariot-1", PieceType.Chariot, Side.Black, 8, 1)
        });
        var positions = await PlaySequence(matchId, board,
            ("red-chariot-1", Side.Red, 0, 7), ("black-chariot-1", Side.Black, 8, 2),
            ("red-chariot-1", Side.Red, 0, 8), ("black-chariot-1", Side.Black, 8, 1),
            ("red-chariot-1", Side.Red, 0, 7), ("black-chariot-1", Side.Black, 8, 2),
            ("red-chariot-1", Side.Red, 0, 8), ("black-chariot-1", Side.Black, 8, 1));

        await AssertRepeatedMatchContinues(matchId, board, positions);
    }

    [Fact]
    public async Task GCT112_repeated_check_sequence_is_legal_but_not_adjudicated()
    {
        var (_, matchId) = await Start();
        var board = new Board(new[]
        {
            P("red-general-1", PieceType.General, Side.Red, 5, 9),
            P("black-general-1", PieceType.General, Side.Black, 4, 0),
            P("red-chariot-1", PieceType.Chariot, Side.Red, 3, 1)
        });
        var moves = new (string PieceId, Side Side, int Col, int Row)[]
        {
            ("red-chariot-1", Side.Red, 4, 1), ("black-general-1", Side.Black, 3, 0),
            ("red-chariot-1", Side.Red, 3, 1), ("black-general-1", Side.Black, 4, 0),
            ("red-chariot-1", Side.Red, 4, 1), ("black-general-1", Side.Black, 3, 0),
            ("red-chariot-1", Side.Red, 3, 1), ("black-general-1", Side.Black, 4, 0)
        };
        var positions = await PlaySequence(matchId, board, moves);

        for (var index = 0; index < moves.Length; index += 2)
            Assert.True(XiangqiRules.IsInCheck(positions[index + 1], Side.Black));
        await AssertRepeatedMatchContinues(matchId, board, positions);
    }

    [Fact]
    public async Task GCT112_repeated_attack_on_same_piece_is_not_classified_as_chase()
    {
        var (_, matchId) = await Start();
        var board = new Board(new[]
        {
            P("red-general-1", PieceType.General, Side.Red, 5, 9),
            P("black-general-1", PieceType.General, Side.Black, 3, 0),
            P("red-chariot-1", PieceType.Chariot, Side.Red, 0, 6),
            P("black-horse-1", PieceType.Horse, Side.Black, 1, 2)
        });
        var moves = new (string PieceId, Side Side, int Col, int Row)[]
        {
            ("red-chariot-1", Side.Red, 1, 6), ("black-horse-1", Side.Black, 0, 4),
            ("red-chariot-1", Side.Red, 0, 6), ("black-horse-1", Side.Black, 1, 2),
            ("red-chariot-1", Side.Red, 1, 6), ("black-horse-1", Side.Black, 0, 4),
            ("red-chariot-1", Side.Red, 0, 6), ("black-horse-1", Side.Black, 1, 2)
        };
        var positions = await PlaySequence(matchId, board, moves);

        for (var index = 0; index < moves.Length; index += 2)
        {
            var attacked = positions[index + 1];
            var horse = attacked.GetPieceById("black-horse-1")!;
            Assert.True(XiangqiRules.IsLegal(attacked, Side.Red, new("red-chariot-1", horse.Position)));
        }
        await AssertRepeatedMatchContinues(matchId, board, positions);
    }

    [Fact] public async Task Create_retry_join_race_and_access_checks()
    {
        var input = new CreateRoomInput(Guid.NewGuid(),"Race room");
        var a = await Run(s => s.Create(red,input)); var retry = await Run(s => s.Create(red,input));
        Assert.Equal(a.Id,retry.Id);
        Assert.Equal("ALREADY_IN_ROOM",(await Assert.ThrowsAsync<GameFault>(() => Run(s => s.Create(red,new(Guid.NewGuid(),"Second"))))).Code);
        async Task<bool> Join(Guid user) { try { await Run(s => s.Join(user,a.Code)); return true; } catch (GameFault e) { Assert.Equal("ROOM_FULL",e.Code); return false; } }
        var outcomes = await Task.WhenAll(Join(black),Join(outsider)); Assert.Single(outcomes,x=>x);
        var room = await Run(s=>s.Snapshot(a.Id,red)); Assert.Equal(2,room.Members.Count);
        var denied = room.Members.Any(m=>m.UserId==black) ? outsider : black;
        Assert.Equal(403,(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Snapshot(a.Id,denied)))).Status);
        Assert.Equal("NOT_READY",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Start(a.Id,red,new(Guid.NewGuid()))))).Code);
    }
    [Fact] public async Task Start_and_move_retries_commit_once_and_history_replays()
    {
        var (room,id) = await Start();
        var starts = await Task.WhenAll(Run(s=>s.Start(room.Id,red,new(Guid.NewGuid()))),Run(s=>s.Start(room.Id,red,new(Guid.NewGuid()))));
        Assert.All(starts,s=>Assert.Equal(id,s.MatchId));
        var bad = new MoveInput(Guid.NewGuid(),1,"red-chariot-1",1,8);
        Assert.Equal("INVALID_MOVE",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,bad)))).Code);
        var move = new MoveInput(Guid.NewGuid(),1,"red-soldier-1",0,5);
        await Task.WhenAll(Run(s=>s.Move(id,red,move)),Run(s=>s.Move(id,red,move)));
        await using var db = Db();
        Assert.Equal(1,await db.Moves.CountAsync(m=>m.MatchId==id)); Assert.Equal(1,await db.Matches.CountAsync(m=>m.RoomId==room.Id));
        Assert.Equal("REQUEST_CONFLICT",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,move with { ToRow=4 })))).Code);
        Assert.Equal("VERSION_CONFLICT",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,black,new(Guid.NewGuid(),1,"black-soldier-1",0,4))))).Code);
        Assert.Equal("NOT_YOUR_TURN",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Move(id,red,new(Guid.NewGuid(),2,"red-soldier-1",0,4))))).Code);
        var saved = await db.Matches.AsNoTracking().SingleAsync(m=>m.Id==id);
        var replay = BoardSerializer.Deserialize(saved.InitialBoardJson);
        foreach (var m in await db.Moves.Where(m=>m.MatchId==id).OrderBy(m=>m.Ply).ToListAsync()) replay = replay.ApplyMove(m.PieceId,new(m.ToCol,m.ToRow),out _);
        Assert.Equal(saved.CurrentBoardJson,BoardSerializer.Serialize(replay));
        await Run(s=>s.End(id,red,false)); await Run(s=>s.End(id,black,false));
        await using var verify = Db(); var ended=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal("black",ended.Winner); Assert.Equal("resignation",ended.ResultReason);
    }

    [Fact] public async Task GCT89_invalid_moves_preserve_state_and_black_moves_use_canonical_coordinates()
    {
        var (_, id) = await Start();
        await using (var beforeDb = Db())
        {
            var before = await beforeDb.Matches.AsNoTracking().SingleAsync(m => m.Id == id);
            var invalid = await Assert.ThrowsAsync<GameFault>(() =>
                Run(s => s.Move(id, red, new MoveInput(Guid.NewGuid(), before.Version, "red-chariot-1", 1, 8))));
            Assert.Equal("INVALID_MOVE", invalid.Code);

            await using var afterDb = Db();
            var after = await afterDb.Matches.AsNoTracking().SingleAsync(m => m.Id == id);
            Assert.Equal(before.CurrentBoardJson, after.CurrentBoardJson);
            Assert.Equal(before.Version, after.Version);
            Assert.Equal(before.MoveNumber, after.MoveNumber);
            Assert.Equal(before.SideToMove, after.SideToMove);
            Assert.Empty(await afterDb.Moves.Where(m => m.MatchId == id).ToListAsync());
        }

        await Run(s => s.Move(id, red, new MoveInput(Guid.NewGuid(), 1, "red-soldier-1", 0, 5)));
        var blackRequest = new MoveInput(Guid.NewGuid(), 2, "black-soldier-1", 0, 4);
        var committed = await Run(s => s.Move(id, black, blackRequest));
        var duplicate = await Run(s => s.Move(id, black, blackRequest));

        await using var verify = Db();
        var match = await verify.Matches.AsNoTracking().SingleAsync(m => m.Id == id);
        var board = BoardSerializer.Deserialize(match.CurrentBoardJson);
        Assert.Equal("red", match.SideToMove);
        Assert.Equal(3, match.Version);
        Assert.Equal(2, match.MoveNumber);
        Assert.Equal(0, board.GetPieceById("black-soldier-1")!.Position.Col);
        Assert.Equal(4, board.GetPieceById("black-soldier-1")!.Position.Row);
        Assert.Equal(2, await verify.Moves.CountAsync(m => m.MatchId == id));

        using var committedJson = JsonDocument.Parse(JsonSerializer.Serialize(committed, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var duplicateJson = JsonDocument.Parse(JsonSerializer.Serialize(duplicate, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(3, committedJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(committedJson.RootElement.GetProperty("version").GetInt64(), duplicateJson.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(2, duplicateJson.RootElement.GetProperty("moveNumber").GetInt32());
        var blackLastMove = duplicateJson.RootElement.GetProperty("lastMove");
        Assert.Equal(0, blackLastMove.GetProperty("fromCol").GetInt32());
        Assert.Equal(3, blackLastMove.GetProperty("fromRow").GetInt32());
        Assert.Equal(0, blackLastMove.GetProperty("toCol").GetInt32());
        Assert.Equal(4, blackLastMove.GetProperty("toRow").GetInt32());
    }

    [Fact] public async Task GCT16_surrender_requires_confirmation_and_finishes_once_for_confirming_player()
    {
        var (_, id) = await Start();

        Assert.Equal("CONFIRMATION_REQUIRED", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.Surrender(id, black, false)))).Code);
        await using (var afterCancel = Db())
        {
            var cancelledState = await afterCancel.Matches.SingleAsync(m => m.Id == id);
            Assert.Equal("playing", cancelledState.Status);
            Assert.Equal(1, cancelledState.Version);
            Assert.Null(cancelledState.Winner);
            Assert.Null(cancelledState.ResultReason);
            Assert.Null(cancelledState.EndedAt);
        }

        await Run(s => s.Surrender(id, black, true));

        await using var db = Db();
        var finished = await db.Matches.SingleAsync(m => m.Id == id);
        var endedAt = finished.EndedAt;
        var version = finished.Version;
        Assert.Equal("finished", finished.Status);
        Assert.Equal("red", finished.Winner);
        Assert.Equal("resignation", finished.ResultReason);
        Assert.NotNull(endedAt);

        await Run(s => s.Surrender(id, red, true));
        await using var verify = Db();
        var unchanged = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal(endedAt, unchanged.EndedAt);
        Assert.Equal(version, unchanged.Version);
        Assert.Equal("red", unchanged.Winner);
    }

    [Fact] public async Task GCT88_ended_match_locks_board_and_returns_authoritative_surrender_result()
    {
        var (_, id) = await Start();
        await Run(s => s.Surrender(id, black, true));

        var webJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var redResult = JsonSerializer.SerializeToElement(await Run(s => s.MatchSnapshot(id, red)), webJson);
        var blackResult = JsonSerializer.SerializeToElement(await Run(s => s.MatchSnapshot(id, black)), webJson);
        Assert.Equal(redResult.GetProperty("result").GetRawText(), blackResult.GetProperty("result").GetRawText());
        Assert.Equal("finished", redResult.GetProperty("result").GetProperty("status").GetString());
        Assert.Equal("red", redResult.GetProperty("result").GetProperty("winner").GetString());
        Assert.Equal("red-win", redResult.GetProperty("result").GetProperty("outcome").GetString());
        Assert.Equal("surrender", redResult.GetProperty("result").GetProperty("reasonCode").GetString());
        Assert.Equal("resignation", redResult.GetProperty("result").GetProperty("reason").GetString());
        Assert.NotEqual(default, redResult.GetProperty("result").GetProperty("endedAt").GetDateTimeOffset());
        Assert.Empty(await Run(s => s.LegalMoves(id, red, "red-soldier-1")));

        await using var beforeMove = Db();
        var savedBeforeMove = await beforeMove.Matches.AsNoTracking().SingleAsync(m => m.Id == id);
        var boardBeforeMove = savedBeforeMove.CurrentBoardJson;
        var versionBeforeMove = savedBeforeMove.Version;
        var moveNumberBeforeMove = savedBeforeMove.MoveNumber;
        var moveFault = await Assert.ThrowsAsync<GameFault>(() =>
            Run(s => s.Move(id, red, new(Guid.NewGuid(), versionBeforeMove, "red-soldier-1", 0, 5))));
        Assert.Equal("MATCH_NOT_PLAYING", moveFault.Code);

        await using var verify = Db();
        var savedAfterMove = await verify.Matches.AsNoTracking().SingleAsync(m => m.Id == id);
        Assert.Equal("finished", savedAfterMove.Status);
        Assert.Equal(boardBeforeMove, savedAfterMove.CurrentBoardJson);
        Assert.Equal(versionBeforeMove, savedAfterMove.Version);
        Assert.Equal(moveNumberBeforeMove, savedAfterMove.MoveNumber);
        Assert.Empty(await verify.Moves.Where(m => m.MatchId == id).ToListAsync());
    }

    [Fact] public async Task GCT87_match_actions_are_restricted_to_match_players()
    {
        var (_, id) = await Start();
        var offerId = Guid.NewGuid();

        Assert.Equal("NOT_MATCH_PLAYER", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.Surrender(id, outsider, true)))).Code);
        Assert.Equal("NOT_MATCH_PLAYER", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.OfferDraw(id, outsider)))).Code);
        Assert.Equal("NOT_MATCH_PLAYER", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.AcceptDraw(id, outsider, offerId)))).Code);
        Assert.Equal("NOT_MATCH_PLAYER", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.RejectDraw(id, outsider, offerId)))).Code);

        await using var db = Db();
        var unchanged = await db.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal("playing", unchanged.Status);
        Assert.Null(unchanged.DrawOfferId);
    }

    [Fact] public async Task GCT16_draw_offer_can_be_rejected_or_accepted_only_by_opponent()
    {
        var (_, id) = await Start();

        await Run(s => s.OfferDraw(id, red));
        Guid offerId;
        await using (var db = Db())
        {
            var offered = await db.Matches.SingleAsync(m => m.Id == id);
            Assert.Equal("red", offered.DrawOfferedBy);
            Assert.NotNull(offered.DrawOfferId);
            offerId = offered.DrawOfferId.Value;
            Assert.NotNull(offered.DrawCreatedAt);
            Assert.Equal(30, (offered.DrawExpiresAt!.Value - offered.DrawCreatedAt!.Value).TotalSeconds);
        }

        Assert.Equal("DRAW_OFFER_NOT_YOURS", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.AcceptDraw(id, red, offerId)))).Code);
        Assert.Equal("DRAW_OFFER_NOT_YOURS", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.RejectDraw(id, red, offerId)))).Code);
        await Run(s => s.RejectDraw(id, black, offerId));

        await using (var db = Db())
        {
            var rejected = await db.Matches.SingleAsync(m => m.Id == id);
            Assert.Equal("playing", rejected.Status);
            Assert.Null(rejected.DrawOfferId);
            Assert.Null(rejected.DrawOfferedBy);
            Assert.Null(rejected.DrawCreatedAt);
            Assert.Null(rejected.DrawExpiresAt);
        }

        var rejectedOfferId = offerId;
        await Run(s => s.OfferDraw(id, red));
        await using (var db = Db())
            offerId = (await db.Matches.SingleAsync(m => m.Id == id)).DrawOfferId!.Value;
        Assert.Equal("DRAW_OFFER_STALE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.AcceptDraw(id, black, rejectedOfferId)))).Code);
        await Run(s => s.AcceptDraw(id, black, offerId));
        await using var verify = Db();
        var accepted = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal("finished", accepted.Status);
        Assert.Null(accepted.Winner);
        Assert.Equal("draw-agreement", accepted.ResultReason);
        Assert.Null(accepted.DrawOfferId);
    }

    [Theory]
    [InlineData("ai")]
    [InlineData("pve")]
    public async Task GCT87_draw_negotiation_is_disabled_against_computer(string mode)
    {
        var (_, id) = await Start();
        await using (var db = Db())
        {
            var match = await db.Matches.SingleAsync(m => m.Id == id);
            match.Mode = mode;
            await db.SaveChangesAsync();
        }

        Assert.Equal("DRAW_NOT_AVAILABLE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.OfferDraw(id, red)))).Code);
        Assert.Equal("DRAW_NOT_AVAILABLE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.AcceptDraw(id, black, Guid.NewGuid())))).Code);
        Assert.Equal("DRAW_NOT_AVAILABLE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.RejectDraw(id, black, Guid.NewGuid())))).Code);
        Assert.Equal("DRAW_NOT_AVAILABLE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.End(id, red, true)))).Code);

        await using var verify = Db();
        var unchanged = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal("playing", unchanged.Status);
        Assert.Null(unchanged.DrawOfferId);
    }

    [Fact] public async Task GCT87_simultaneous_draw_acceptance_and_surrender_finish_match_once()
    {
        var (_, id) = await Start();
        await Run(s => s.OfferDraw(id, red));
        Guid offerId;
        await using (var db = Db())
            offerId = (await db.Matches.SingleAsync(m => m.Id == id)).DrawOfferId!.Value;

        async Task<string> RunAndCapture(Func<GameService, Task<object>> action)
        {
            try
            {
                await Run(action);
                return "OK";
            }
            catch (GameFault fault)
            {
                return fault.Code;
            }
        }

        var outcomes = await Task.WhenAll(
            RunAndCapture(s => s.AcceptDraw(id, black, offerId)),
            RunAndCapture(s => s.Surrender(id, red, true)));

        Assert.Contains("OK", outcomes);
        await using var verify = Db();
        var finished = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal("finished", finished.Status);
        Assert.Contains(finished.ResultReason, new[] { "draw-agreement", "resignation" });
        var resultReason = finished.ResultReason;
        var winner = finished.Winner;
        var endedAt = finished.EndedAt;
        var version = finished.Version;

        await Run(s => s.Surrender(id, red, true));
        await using var afterRetry = Db();
        var unchanged = await afterRetry.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal(resultReason, unchanged.ResultReason);
        Assert.Equal(winner, unchanged.Winner);
        Assert.Equal(endedAt, unchanged.EndedAt);
        Assert.Equal(version, unchanged.Version);
    }

    [Fact] public async Task GCT16_draw_offer_expires_after_deadline_without_ending_match()
    {
        var (_, id) = await Start();
        await Run(s => s.OfferDraw(id, red));
        await using (var db = Db())
        {
            var match = await db.Matches.SingleAsync(m => m.Id == id);
            match.DrawExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        Assert.Single(await Run(s => s.ExpireDrawOffers()));
        Assert.Empty(await Run(s => s.ExpireDrawOffers()));
        await using (var db = Db())
        {
            var expired = await db.Matches.SingleAsync(m => m.Id == id);
            Assert.Equal("playing", expired.Status);
            Assert.Null(expired.DrawOfferId);
            Assert.Null(expired.DrawOfferedBy);
            Assert.Null(expired.DrawCreatedAt);
            Assert.Null(expired.DrawExpiresAt);
        }
        Assert.Equal("DRAW_OFFER_NOT_FOUND", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.AcceptDraw(id, black, Guid.NewGuid())))).Code);
    }

    [Fact] public async Task GCT16_expired_offer_cannot_be_accepted_before_expiration_worker_runs()
    {
        var (_, id) = await Start();
        await Run(s => s.OfferDraw(id, red));
        await using (var db = Db())
        {
            var match = await db.Matches.SingleAsync(m => m.Id == id);
            match.DrawExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        Guid offerId;
        await using (var db = Db())
            offerId = (await db.Matches.SingleAsync(m => m.Id == id)).DrawOfferId!.Value;
        await Run(s => s.AcceptDraw(id, black, offerId));
        await using var verify = Db();
        var unchanged = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Equal("playing", unchanged.Status);
        Assert.Null(unchanged.DrawOfferId);
        Assert.Null(unchanged.DrawOfferedBy);
        Assert.Null(unchanged.DrawCreatedAt);
        Assert.Null(unchanged.DrawExpiresAt);
    }

    [Fact] public async Task GCT16_valid_move_invalidates_draw_offer_but_invalid_move_does_not()
    {
        var (_, id) = await Start();
        await Run(s => s.OfferDraw(id, red));
        var invalidMove = new MoveInput(Guid.NewGuid(), 2, "red-soldier-1", 0, 8);
        Assert.Equal("INVALID_MOVE", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.Move(id, red, invalidMove)))).Code);
        await using (var db = Db())
            Assert.NotNull((await db.Matches.SingleAsync(m => m.Id == id)).DrawOfferId);

        await Run(s => s.Move(id, red, new MoveInput(Guid.NewGuid(), 2, "red-soldier-1", 0, 5)));
        await using var verify = Db();
        var moved = await verify.Matches.SingleAsync(m => m.Id == id);
        Assert.Null(moved.DrawOfferId);
        Assert.Null(moved.DrawOfferedBy);
        Assert.Null(moved.DrawCreatedAt);
        Assert.Null(moved.DrawExpiresAt);
    }

    [Fact] public async Task Concurrent_start_commands_create_only_one_active_match()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(), "Concurrent start")));
        await Run(s => s.Join(black, room.Code));
        await Run(s => s.Ready(room.Id, red, true));
        await Run(s => s.Ready(room.Id, black, true));

        var starts = await Task.WhenAll(
            Run(s => s.Start(room.Id, red, new(Guid.NewGuid()))),
            Run(s => s.Start(room.Id, red, new(Guid.NewGuid()))));

        Assert.NotNull(starts[0].MatchId);
        Assert.Equal(starts[0].MatchId, starts[1].MatchId);
        await using var db = Db();
        Assert.Equal(1, await db.Matches.CountAsync(m => m.RoomId == room.Id));
        Assert.Equal(1, await db.Matches.CountAsync(m => m.RoomId == room.Id && (m.Status == "playing" || m.Status == "paused")));
        Assert.Equal("playing", (await db.Rooms.SingleAsync(r => r.Id == room.Id)).Status);
    }
    [Fact] public async Task Room_state_transitions_enforce_ready_playing_finished_and_closed_rules()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(), "Room lifecycle")));
        await Run(s => s.Join(black, room.Code));
        await Run(s => s.Ready(room.Id, red, true));
        await Run(s => s.Ready(room.Id, black, true));

        var waiting = await Run(s => s.Snapshot(room.Id, red));
        Assert.Equal("waiting", waiting.Status);
        Assert.All(waiting.Members, member => Assert.True(member.Ready));

        var playing = await Run(s => s.Start(room.Id, red, new(Guid.NewGuid())));
        Assert.Equal("playing", playing.Status);
        Assert.Equal("ROOM_PLAYING", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.Ready(room.Id, red, false)))).Code);

        await Run(s => s.End(playing.MatchId!.Value, red, false));
        await using (var db = Db())
        {
            Assert.Equal("finished", (await db.Rooms.SingleAsync(r => r.Id == room.Id)).Status);
            Assert.Equal("finished", (await db.Matches.SingleAsync(m => m.Id == playing.MatchId)).Status);
        }

        var closed = await Run(s => s.Leave(room.Id, red, true));
        Assert.Equal("closed", closed.Status);
        Assert.Empty(closed.Members);
        Assert.Equal("ROOM_CLOSED", (await Assert.ThrowsAsync<GameFault>(
            () => Run(s => s.Snapshot(room.Id, red)))).Code);
    }
    [Fact] public async Task Stale_room_update_is_rejected_by_optimistic_concurrency()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(), "Stale room update")));
        await using var firstDb = Db();
        await using var staleDb = Db();
        var current = await firstDb.Rooms.SingleAsync(r => r.Id == room.Id);
        var stale = await staleDb.Rooms.SingleAsync(r => r.Id == room.Id);

        current.Version++;
        await firstDb.SaveChangesAsync();
        stale.Version++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
    }
    [Fact] public async Task Concurrent_leaves_close_room_once_without_leaving_active_seats()
    {
        var room = await Run(s => s.Create(red, new(Guid.NewGuid(), "Concurrent leave")));
        await Run(s => s.Join(black, room.Code));

        var leaves = await Task.WhenAll(
            Leave(room.Id, red, true),
            Leave(room.Id, black, false));

        Assert.Contains(true, leaves);
        await using var db = Db();
        var savedRoom = await db.Rooms.SingleAsync(r => r.Id == room.Id);
        Assert.Equal("closed", savedRoom.Status);
        Assert.NotNull(savedRoom.ClosedAt);
        Assert.Empty(await db.RoomMemberships.Where(m => m.RoomId == room.Id && m.LeftAt == null).ToListAsync());
    }
    private async Task<bool> Leave(Guid roomId, Guid userId, bool close)
    {
        try { await Run(s => s.Leave(roomId, userId, close)); return true; }
        catch (GameFault) { return false; }
    }
    [Fact] public async Task Save_failure_rolls_back_move_board_and_turn()
    {
        var (_,id)=await Start();
        await using(var db=Db()) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_move BEFORE INSERT ON Moves BEGIN SELECT RAISE(ABORT, 'test write failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(()=>Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5))));
        await using var verify=Db(); var match=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal(1,match.Version); Assert.Equal("red",match.SideToMove); Assert.Equal(0,match.MoveNumber); Assert.Empty(await verify.Moves.ToListAsync());
        Assert.Equal(match.InitialBoardJson,match.CurrentBoardJson);
    }
    [Fact] public async Task Restart_recovery_keeps_committed_moves_and_waits_for_both_players()
    {
        var (room,id)=await Start();
        await Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5)));
        DateTimeOffset blackDeadline;
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            match.RedRecoveryRemainingMs=42000;
            match.BlackRecoveryRemainingMs=31000;
            blackDeadline=DateTimeOffset.UtcNow.AddSeconds(25);
            match.BlackDisconnectedAt=DateTimeOffset.UtcNow.AddSeconds(-5);
            match.BlackDeadlineAt=blackDeadline;
            var blackMembership=await db.RoomMemberships.SingleAsync(m=>m.RoomId==room.Id && m.UserId==black);
            blackMembership.IsConnected=false;
            blackMembership.ReconnectDeadlineAt=blackDeadline;
            await db.SaveChangesAsync();
        }

        Assert.Equal(1,await Run(s=>s.RecoverAfterRestart()));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            Assert.Equal("paused",match.Status);
            Assert.Equal("serverRestart",match.RecoveryReason);
            Assert.Null(match.TurnStartedAt);
            Assert.Equal(42000,match.RedRecoveryRemainingMs);
            Assert.Equal(31000,match.BlackRecoveryRemainingMs);
            Assert.Equal(blackDeadline,match.BlackDeadlineAt);
            Assert.True(match.RedDeadlineAt > DateTimeOffset.UtcNow);
            Assert.Single(await db.Moves.Where(m=>m.MatchId==id).ToListAsync());
            Assert.NotEqual(match.InitialBoardJson,match.CurrentBoardJson);
        }

        await Run(s=>s.Reconnect(id,red));
        await using(var db=Db()) Assert.Equal("paused",(await db.Matches.SingleAsync(m=>m.Id==id)).Status);
        await Run(s=>s.Reconnect(id,red));
        await using(var db=Db()) Assert.Equal("paused",(await db.Matches.SingleAsync(m=>m.Id==id)).Status);
        await Run(s=>s.Reconnect(id,black));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            Assert.Equal("playing",match.Status);
            Assert.Null(match.RecoveryReason);
            Assert.Equal(1,match.MoveNumber);
            Assert.InRange(match.RedRecoveryRemainingMs,0,42000);
            Assert.InRange(match.BlackRecoveryRemainingMs,0,31000);
            Assert.Single(await db.Moves.Where(m=>m.MatchId==id).ToListAsync());
        }
    }

    [Fact] public async Task Disconnect_pauses_and_locks_moves_then_reconnect_restores_the_same_match()
    {
        var (room,id)=await Start();
        await Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5)));

        await Run(s=>s.Presence(room.Id,black,false));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            Assert.Equal("paused",match.Status);
            Assert.Equal("disconnect",match.RecoveryReason);
            Assert.Null(match.TurnStartedAt);
            Assert.NotNull(match.BlackDeadlineAt);
            Assert.Equal("MATCH_NOT_PLAYING",(await Assert.ThrowsAsync<GameFault>(
                ()=>Run(s=>s.Move(id,black,new(Guid.NewGuid(),match.Version,"black-soldier-1",0,4))))).Code);
        }

        var recovery=await Run(s=>s.MatchSnapshot(id,red));
        using(var json=JsonDocument.Parse(JsonSerializer.Serialize(recovery,new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            Assert.Equal("waiting_reconnect",json.RootElement.GetProperty("recoveryState").GetString());
            Assert.InRange(json.RootElement.GetProperty("blackReconnectCountdownMs").GetInt32(),1,60000);
        }

        var restored=await Run(s=>s.Reconnect(id,black));
        using(var json=JsonDocument.Parse(JsonSerializer.Serialize(restored,new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            Assert.Equal(id,json.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("playing",json.RootElement.GetProperty("status").GetString());
            Assert.Equal("black",json.RootElement.GetProperty("playerSide").GetString());
            Assert.Equal("black",json.RootElement.GetProperty("sideToMove").GetString());
            Assert.Equal(1,json.RootElement.GetProperty("moveNumber").GetInt32());
            Assert.NotEmpty(json.RootElement.GetProperty("pieces").EnumerateArray());
        }

        await using var verify=Db();
        Assert.Equal(1,await verify.Matches.CountAsync(m=>m.RoomId==room.Id));
        Assert.Equal("playing",(await verify.Matches.SingleAsync(m=>m.Id==id)).Status);
    }

    [Fact] public async Task Disconnect_timeout_forfeits_and_finishes_the_existing_match()
    {
        var (room,id)=await Start();
        await Run(s=>s.Presence(room.Id,black,false));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            match.BlackDisconnectedAt=DateTimeOffset.UtcNow.AddSeconds(-61);
            match.BlackDeadlineAt=DateTimeOffset.UtcNow.AddMilliseconds(-1);
            var membership=await db.RoomMemberships.SingleAsync(m=>m.RoomId==room.Id && m.UserId==black);
            membership.ReconnectDeadlineAt=match.BlackDeadlineAt;
            await db.SaveChangesAsync();
        }

        Assert.Equal(1,await Run(s=>s.ExpireRecoveries()));

        await using var verify=Db();
        var matchState=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal("finished",matchState.Status);
        Assert.Equal("red",matchState.Winner);
        Assert.Equal("disconnect-timeout",matchState.ResultReason);
        Assert.Equal("finished",(await verify.Rooms.SingleAsync(r=>r.Id==room.Id)).Status);
        Assert.Equal(1,await verify.Matches.CountAsync(m=>m.RoomId==room.Id));
        var result = JsonSerializer.SerializeToElement(
            await Run(s => s.MatchSnapshot(id, red)), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("timeout", result.GetProperty("reasonCode").GetString());
        Assert.Equal("timeout", result.GetProperty("result").GetProperty("reasonCode").GetString());
    }

    [Fact] public async Task Restart_while_waiting_for_disconnect_requires_both_players_to_reconnect()
    {
        var (room,id)=await Start();
        await Run(s=>s.Presence(room.Id,black,false));
        DateTimeOffset blackDeadline;
        await using(var db=Db())
            blackDeadline=(await db.Matches.SingleAsync(m=>m.Id==id)).BlackDeadlineAt!.Value;

        Assert.Equal(1,await Run(s=>s.RecoverAfterRestart()));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            Assert.Equal("disconnect",match.RecoveryReason);
            Assert.Equal(blackDeadline,match.BlackDeadlineAt);
            Assert.True(match.RedDeadlineAt>DateTimeOffset.UtcNow);
            Assert.False(await db.RoomMemberships.Where(m=>m.RoomId==room.Id && m.LeftAt==null).AnyAsync(m=>m.IsConnected));
        }

        await Run(s=>s.Reconnect(id,black));
        await using(var db=Db()) Assert.Equal("paused",(await db.Matches.SingleAsync(m=>m.Id==id)).Status);
        var restored=await Run(s=>s.Reconnect(id,red));
        using(var json=JsonDocument.Parse(JsonSerializer.Serialize(restored,new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            Assert.Equal(id,json.RootElement.GetProperty("id").GetGuid());
            Assert.Equal("playing",json.RootElement.GetProperty("status").GetString());
        }
        await using var verify=Db();
        Assert.Equal(1,await verify.Matches.CountAsync(m=>m.RoomId==room.Id));
    }

    [Fact] public async Task Expired_restart_recovery_cancels_without_assigning_a_winner()
    {
        var (room,id)=await Start();
        await Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5)));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            match.BlackDeadlineAt=DateTimeOffset.UtcNow.AddMilliseconds(-1);
            var blackMembership=await db.RoomMemberships.SingleAsync(m=>m.RoomId==room.Id && m.UserId==black);
            blackMembership.IsConnected=false;
            await db.SaveChangesAsync();
        }
        await Run(s=>s.RecoverAfterRestart());
        Assert.Equal(1,await Run(s=>s.ExpireRecoveries()));

        await using var verify=Db();
        var cancelled=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal("cancelled",cancelled.Status);
        Assert.Equal("serverRestart",cancelled.ResultReason);
        Assert.Null(cancelled.Winner);
        Assert.Single(await verify.Moves.Where(m=>m.MatchId==id).ToListAsync());
    }
    [Fact] public async Task Reconnect_racing_recovery_cleanup_cannot_resurrect_an_expired_match()
    {
        var (room,id)=await Start();
        await Run(s=>s.Move(id,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5)));
        await using(var db=Db())
        {
            var match=await db.Matches.SingleAsync(m=>m.Id==id);
            var now=DateTimeOffset.UtcNow;
            match.Status="paused";
            match.RecoveryReason="serverRestart";
            match.RedDeadlineAt=now.AddMilliseconds(-1);
            match.BlackDeadlineAt=now.AddSeconds(1);
            var memberships=await db.RoomMemberships.Where(m=>m.RoomId==room.Id && m.LeftAt==null).ToListAsync();
            foreach(var membership in memberships)
            {
                membership.IsConnected=false;
                membership.ReconnectDeadlineAt=membership.UserId==red ? match.RedDeadlineAt : match.BlackDeadlineAt;
            }
            await db.SaveChangesAsync();
        }

        var reconnect=Run(s=>s.Reconnect(id,red));
        var cleanup=Run(s=>s.ExpireRecoveries());
        await Task.WhenAll(reconnect,cleanup);

        await using var verify=Db();
        var savedMatch=await verify.Matches.SingleAsync(m=>m.Id==id);
        Assert.Equal("cancelled",savedMatch.Status);
        Assert.Equal("serverRestart",savedMatch.ResultReason);
        Assert.Equal("finished",(await verify.Rooms.SingleAsync(r=>r.Id==room.Id)).Status);
        Assert.Single(await verify.Moves.Where(m=>m.MatchId==id).ToListAsync());
    }
    [Fact] public async Task Chat_is_idempotent_limited_and_members_only()
    {
        var room=await Run(s=>s.Create(red,new(Guid.NewGuid(),"Chat")));
        var input=new ChatInput(Guid.NewGuid(),"<script>literal text</script>");
        var message=await Run(s=>s.Chat(room.Id,red,input)); var retry=await Run(s=>s.Chat(room.Id,red,input)); Assert.Equal(message.Id,retry.Id);
        Assert.Equal("<script>literal text</script>",message.Content);
        await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,outsider,input)));
        Assert.Equal("VALIDATION_ERROR",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),"  "))))).Code);
        Assert.Equal("VALIDATION_ERROR",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),new string('x',501)))))).Code);
        for(int i=0;i<4;i++) await Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),$"Message {i}")));
        Assert.Equal("RATE_LIMITED",(await Assert.ThrowsAsync<GameFault>(()=>Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),"Sixth"))))).Code);
        var messages=await Run(s=>s.Messages(room.Id,red));
        Assert.Equal(5,messages.Count);
        Assert.Equal("<script>literal text</script>",messages[0].Content);
    }
    [Fact] public async Task Chat_rate_limit_does_not_change_match_state_or_block_moves_and_result()
    {
        var (room,matchId)=await Start();
        for(var i=0;i<5;i++)
            await Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),$"Message {i}")));

        var rateLimited=await Assert.ThrowsAsync<GameFault>(()=>
            Run(s=>s.Chat(room.Id,red,new(Guid.NewGuid(),"Too fast"))));
        Assert.Equal("RATE_LIMITED",rateLimited.Code);

        await using(var db=Db())
        {
            var unchanged=await db.Matches.AsNoTracking().SingleAsync(m=>m.Id==matchId);
            Assert.Equal("playing",unchanged.Status);
            Assert.Equal(0,unchanged.MoveNumber);
            Assert.Null(unchanged.Winner);
        }

        await Run(s=>s.Move(matchId,red,new(Guid.NewGuid(),1,"red-soldier-1",0,5)));
        await Run(s=>s.End(matchId,black,false));

        await using var verify=Db();
        var final=await verify.Matches.AsNoTracking().SingleAsync(m=>m.Id==matchId);
        Assert.Equal("finished",final.Status);
        Assert.Equal("red",final.Winner);
        Assert.Equal("resignation",final.ResultReason);
        Assert.Single(await verify.Moves.Where(m=>m.MatchId==matchId).ToListAsync());
    }
    [Fact] public void Video_session_blocks_wrong_ids_wrong_answerer_and_stale_ice()
    {
        var calls=new VideoCalls(); var room=Guid.NewGuid(); var id=Guid.NewGuid();
        calls.Validate(room,red,new(id,black,"offer","{}"));
        Assert.Throws<GameFault>(()=>calls.Validate(room,red,new(id,black,"answer","{}")));
        Assert.Throws<GameFault>(()=>calls.Validate(room,black,new(Guid.NewGuid(),red,"ice","{}")));
        calls.Validate(room,black,new(id,red,"answer","{}")); calls.Validate(room,red,new(id,black,"ice","{}"));
        calls.Validate(room,black,new(id,red,"hangup","{}"));
        Assert.Throws<GameFault>(()=>calls.Validate(room,red,new(id,black,"ice","{}")));
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); }
}
