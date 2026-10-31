using GameCoTuong.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameCoTuong.Infrastructure.Persistence;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMembership> RoomMemberships => Set<RoomMembership>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<Move> Moves => Set<Move>();
    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NormalizedUsername).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => e.NormalizedUsername).IsUnique();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
        });

        // 2. Rooms
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("Rooms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(6).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("waiting");
            entity.Property(e => e.Version).HasDefaultValue(1L);
            entity.Property(e => e.InitialMinutes).HasDefaultValue(15);
            entity.Property(e => e.IncrementSeconds).HasDefaultValue(10);
            entity.Property(e => e.LastActivityAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => e.Code).IsUnique().HasFilter("[Status] <> 'closed'");

            entity.HasOne(e => e.HostUser)
                .WithMany()
                .HasForeignKey(e => e.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CurrentMatch)
                .WithMany()
                .HasForeignKey(e => e.CurrentMatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. RoomMemberships
        modelBuilder.Entity<RoomMembership>(entity =>
        {
            entity.ToTable("RoomMemberships");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Side).HasMaxLength(10).IsRequired();
            entity.Property(e => e.IsReady).HasDefaultValue(false);
            entity.Property(e => e.IsConnected).HasDefaultValue(true);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => new { e.RoomId, e.Side }).IsUnique().HasFilter("[LeftAt] IS NULL");
            entity.HasIndex(e => e.UserId).IsUnique().HasFilter("[LeftAt] IS NULL");

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Memberships)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Matches
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("Matches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Mode).HasMaxLength(20).HasDefaultValue("pvp");
            entity.Property(e => e.RuleSetVersion).HasMaxLength(50).HasDefaultValue("gct-core-1");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("playing");
            entity.Property(e => e.Version).HasDefaultValue(1L);
            entity.Property(e => e.MoveNumber).HasDefaultValue(0);
            entity.Property(e => e.SideToMove).HasMaxLength(10).HasDefaultValue("red");
            entity.Property(e => e.InitialBoardJson).IsRequired();
            entity.Property(e => e.CurrentBoardJson).IsRequired();
            entity.Property(e => e.RecoveryReason).HasMaxLength(20);
            entity.Property(e => e.RedRecoveryRemainingMs).HasDefaultValue(60000);
            entity.Property(e => e.BlackRecoveryRemainingMs).HasDefaultValue(60000);
            entity.Property(e => e.DrawOfferedBy).HasMaxLength(10);
            entity.Property(e => e.Winner).HasMaxLength(10);
            entity.Property(e => e.ResultReason).HasMaxLength(30);
            entity.Property(e => e.StartedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Matches)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. MatchPlayers
        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.ToTable("MatchPlayers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Side).HasMaxLength(10).IsRequired();

            entity.HasIndex(e => new { e.MatchId, e.Side }).IsUnique();
            entity.HasIndex(e => new { e.MatchId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Match)
                .WithMany(m => m.Players)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. Moves
        modelBuilder.Entity<Move>(entity =>
        {
            entity.ToTable("Moves");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Side).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PieceId).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CapturedPieceId).HasMaxLength(32);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => new { e.MatchId, e.Ply }).IsUnique();
            entity.HasIndex(e => new { e.MatchId, e.RequestId }).IsUnique();

            entity.HasOne(e => e.Match)
                .WithMany(m => m.Moves)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. CommandReceipts
        modelBuilder.Entity<CommandReceipt>(entity =>
        {
            entity.ToTable("CommandReceipts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Operation).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Fingerprint).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ResponseBody).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => new { e.UserId, e.RequestId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. ChatMessages
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => new { e.RoomId, e.Sequence }).IsUnique();
            entity.HasIndex(e => new { e.RoomId, e.RequestId }).IsUnique();

            entity.HasOne(e => e.Room)
                .WithMany(r => r.ChatMessages)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SenderUser)
                .WithMany()
                .HasForeignKey(e => e.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. OutboxMessages
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.AggregateType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasFilter("[Status] = 'pending'");
        });

        // 10. RevokedTokens
        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("RevokedTokens");
            entity.HasKey(e => e.Jti);
            entity.Property(e => e.Jti).HasMaxLength(64);
            entity.Property(e => e.RevokedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed dữ liệu mẫu (GCT-26: dữ liệu demo riêng, không chứa tài khoản thật)
        SeedDemoData(modelBuilder);
        if (Database.IsSqlite())
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
                if (property.GetDefaultValueSql() == "SYSDATETIMEOFFSET()") property.SetDefaultValueSql("CURRENT_TIMESTAMP");
    }

    private static void SeedDemoData(ModelBuilder modelBuilder)
    {
        var userRedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userBlackId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = userRedId,
                Username = "kythu_do",
                NormalizedUsername = "KYTHU_DO",
                Email = "red@cotuong.local",
                NormalizedEmail = "RED@COTUONG.LOCAL",
                PasswordHash = "AQAAAAEAACcQAAAAEPlaceholderPasswordHashRedForLocalTesting123456789==",
                DisplayName = "Kỳ Thủ Đỏ",
                CreatedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                IsActive = true
            },
            new User
            {
                Id = userBlackId,
                Username = "kythu_den",
                NormalizedUsername = "KYTHU_DEN",
                Email = "black@cotuong.local",
                NormalizedEmail = "BLACK@COTUONG.LOCAL",
                PasswordHash = "AQAAAAEAACcQAAAAEPlaceholderPasswordHashBlackForLocalTesting123456789==",
                DisplayName = "Kỳ Thủ Đen",
                CreatedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
                IsActive = true
            }
        );
    }
}
