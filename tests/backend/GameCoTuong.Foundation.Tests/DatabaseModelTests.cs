using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Foundation.Tests;

public sealed class DatabaseModelTests
{
    private readonly GameDbContext context;

    public DatabaseModelTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlServer("Server=localhost;Database=GameCoTuongTest;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options;

        context = new GameDbContext(options);
    }

    [Fact]
    public void All_expected_entities_are_registered_in_model()
    {
        var entityTypes = context.Model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();

        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(Room), entityTypes);
        Assert.Contains(typeof(RoomMembership), entityTypes);
        Assert.Contains(typeof(Match), entityTypes);
        Assert.Contains(typeof(MatchPlayer), entityTypes);
        Assert.Contains(typeof(Move), entityTypes);
        Assert.Contains(typeof(CommandReceipt), entityTypes);
        Assert.Contains(typeof(ChatMessage), entityTypes);
        Assert.Contains(typeof(OutboxMessage), entityTypes);
        Assert.Contains(typeof(RevokedToken), entityTypes);
        Assert.Equal(10, entityTypes.Count);
    }

    [Fact]
    public void Foreign_keys_do_not_use_cascade_delete()
    {
        var foreignKeys = context.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys());

        foreach (var fk in foreignKeys)
        {
            Assert.NotEqual(DeleteBehavior.Cascade, fk.DeleteBehavior);
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void Unique_indexes_and_constraints_are_properly_configured()
    {
        // Users: NormalizedUsername & NormalizedEmail must be unique
        var userType = context.Model.FindEntityType(typeof(User))!;
        Assert.Contains(userType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Any(p => p.Name == nameof(User.NormalizedUsername)));
        Assert.Contains(userType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Any(p => p.Name == nameof(User.NormalizedEmail)));

        // Moves: (MatchId, Ply) and (MatchId, RequestId) must be unique
        var moveType = context.Model.FindEntityType(typeof(Move))!;
        Assert.Contains(moveType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Move.MatchId), nameof(Move.Ply) }));
        Assert.Contains(moveType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Move.MatchId), nameof(Move.RequestId) }));

        // CommandReceipts: (UserId, RequestId) must be unique
        var receiptType = context.Model.FindEntityType(typeof(CommandReceipt))!;
        Assert.Contains(receiptType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CommandReceipt.UserId), nameof(CommandReceipt.RequestId) }));

        // ChatMessages: (RoomId, Sequence) and (RoomId, RequestId) must be unique
        var chatType = context.Model.FindEntityType(typeof(ChatMessage))!;
        Assert.Contains(chatType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ChatMessage.RoomId), nameof(ChatMessage.Sequence) }));
        Assert.Contains(chatType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ChatMessage.RoomId), nameof(ChatMessage.RequestId) }));

        // Rooms: Code has unique filtered index
        var roomType = context.Model.FindEntityType(typeof(Room))!;
        Assert.Contains(roomType.GetIndexes(), idx => idx.IsUnique && idx.Properties.Any(p => p.Name == nameof(Room.Code)) && idx.GetFilter() == "[Status] <> 'closed'");
    }

    [Fact]
    public void Seed_data_contains_demo_accounts_only()
    {
        var designTimeModel = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(context).Model;
        var userType = designTimeModel.FindEntityType(typeof(User))!;
        var seeds = userType.GetSeedData().ToList();

        Assert.Equal(2, seeds.Count);
        var usernames = seeds.Select(s => s[nameof(User.Username)]?.ToString()).ToHashSet();
        Assert.Contains("kythu_do", usernames);
        Assert.Contains("kythu_den", usernames);
    }
}
