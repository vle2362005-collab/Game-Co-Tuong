using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Validation;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Auth;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameCoTuong.Foundation.Tests;

public sealed class RegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=GameCoTuong;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;
    private readonly PasswordHasher passwordHasher = new();

    public RegistrationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GameDatabase"] = ConnectionString
                });
            });
        });

        client = this.factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });
    }

    private static GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new GameDbContext(options);
    }

    // ========================================================================
    // 1. PasswordHasher Tests
    // ========================================================================
    [Fact]
    public void PasswordHasher_hashes_and_verifies_correct_password()
    {
        var password = "SecurePassword@123";
        var hash = passwordHasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.StartsWith("100000.", hash);
        Assert.Equal(3, hash.Split('.').Length);

        Assert.True(passwordHasher.VerifyPassword(password, hash));
        Assert.False(passwordHasher.VerifyPassword("WrongPassword@123", hash));
        Assert.False(passwordHasher.VerifyPassword("", hash));
    }

    [Fact]
    public void PasswordHasher_uses_cryptographic_salt_producing_different_hashes()
    {
        var password = "SecurePassword@123";
        var hash1 = passwordHasher.HashPassword(password);
        var hash2 = passwordHasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
        Assert.True(passwordHasher.VerifyPassword(password, hash1));
        Assert.True(passwordHasher.VerifyPassword(password, hash2));
    }

    // ========================================================================
    // 2. RegisterRequestValidator Tests
    // ========================================================================
    [Fact]
    public void Validator_passes_for_valid_request()
    {
        var request = new RegisterRequest(
            Guid.NewGuid(),
            "valid_user123",
            "Kỳ Thủ Mới",
            "ValidPass@123",
            "valid.user@example.com"
        );

        var errors = RegisterRequestValidator.Validate(request);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ab")] // < 3 chars
    [InlineData("this_username_is_way_too_long_for_registration_12345")] // > 30 chars
    [InlineData("user name")] // contains space
    [InlineData("user@name")] // special char
    [InlineData("user-name")] // hyphen not allowed
    public void Validator_fails_for_invalid_username(string invalidUsername)
    {
        var request = new RegisterRequest(
            Guid.NewGuid(),
            invalidUsername,
            "Tên Hợp Lệ",
            "ValidPass@123",
            "email@example.com"
        );

        var errors = RegisterRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("username"));
    }

    [Theory]
    [InlineData("short")] // < 8 chars
    [InlineData("")]
    public void Validator_fails_for_invalid_password(string invalidPassword)
    {
        var request = new RegisterRequest(
            Guid.NewGuid(),
            "valid_user",
            "Tên Hợp Lệ",
            invalidPassword,
            "email@example.com"
        );

        var errors = RegisterRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@missinglocal.com")]
    [InlineData("space in@email.com")]
    public void Validator_fails_for_invalid_email(string invalidEmail)
    {
        var request = new RegisterRequest(
            Guid.NewGuid(),
            "valid_user",
            "Tên Hợp Lệ",
            "ValidPass@123",
            invalidEmail
        );

        var errors = RegisterRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("email"));
    }

    // ========================================================================
    // 3. RegistrationService & Concurrency Tests (Direct Service & Database)
    // ========================================================================
    [Fact]
    public async Task Service_normalizes_username_email_and_does_not_leak_password_in_result()
    {
        using var db = CreateDbContext();
        var service = new RegistrationService(db, passwordHasher);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var rawUsername = $"  User_{suffix}  ";
        var rawEmail = $"  TEST_{suffix}@Example.COM  ";
        var expectedNormalizedUser = $"user_{suffix}".ToLowerInvariant();
        var expectedNormalizedEmail = $"test_{suffix}@example.com".ToLowerInvariant();

        var request = new RegisterRequest(
            Guid.NewGuid(),
            rawUsername,
            "  Người Chơi Mới  ",
            "SuperSecretPass@2026",
            rawEmail
        );

        var result = await service.RegisterAsync(request);

        try
        {
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.User);
            Assert.Equal(expectedNormalizedUser, result.User.Username);
            Assert.Equal("Người Chơi Mới", result.User.DisplayName);

            // Verify in DB
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == result.User.Id);
            Assert.NotNull(dbUser);
            Assert.Equal(expectedNormalizedUser, dbUser.NormalizedUsername);
            Assert.Equal(expectedNormalizedEmail, dbUser.NormalizedEmail);
            Assert.True(passwordHasher.VerifyPassword("SuperSecretPass@2026", dbUser.PasswordHash));
            Assert.NotEqual("SuperSecretPass@2026", dbUser.PasswordHash);
        }
        finally
        {
            if (result.User is not null)
            {
                var createdUser = await db.Users.FindAsync(result.User.Id);
                if (createdUser is not null)
                {
                    db.Users.Remove(createdUser);
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    [Fact]
    public async Task Service_blocks_duplicate_username_regardless_of_casing()
    {
        using var db = CreateDbContext();
        var service = new RegistrationService(db, passwordHasher);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"player_{suffix}";

        var req1 = new RegisterRequest(Guid.NewGuid(), username.ToLowerInvariant(), "Player 1", "Pass@123456", $"p1_{suffix}@test.com");
        var req2 = new RegisterRequest(Guid.NewGuid(), username.ToUpperInvariant(), "Player 2", "Pass@123456", $"p2_{suffix}@test.com");

        var res1 = await service.RegisterAsync(req1);
        Assert.True(res1.IsSuccess);

        try
        {
            var res2 = await service.RegisterAsync(req2);
            Assert.False(res2.IsSuccess);
            Assert.Equal("USERNAME_TAKEN", res2.ErrorCode);
        }
        finally
        {
            var u1 = await db.Users.FindAsync(res1.User!.Id);
            if (u1 is not null)
            {
                db.Users.Remove(u1);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Service_blocks_duplicate_email_regardless_of_casing()
    {
        using var db = CreateDbContext();
        var service = new RegistrationService(db, passwordHasher);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"duplicate_{suffix}@test.com";

        var req1 = new RegisterRequest(Guid.NewGuid(), $"user1_{suffix}", "User 1", "Pass@123456", email.ToLowerInvariant());
        var req2 = new RegisterRequest(Guid.NewGuid(), $"user2_{suffix}", "User 2", "Pass@123456", email.ToUpperInvariant());

        var res1 = await service.RegisterAsync(req1);
        Assert.True(res1.IsSuccess);

        try
        {
            var res2 = await service.RegisterAsync(req2);
            Assert.False(res2.IsSuccess);
            Assert.Equal("EMAIL_TAKEN", res2.ErrorCode);
        }
        finally
        {
            var u1 = await db.Users.FindAsync(res1.User!.Id);
            if (u1 is not null)
            {
                db.Users.Remove(u1);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Concurrency_creates_exactly_one_account_when_registering_simultaneously()
    {
        // AC-01: "Đăng ký đồng thời cùng username hoặc email khác hoa/thường: Chỉ một tài khoản"
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var commonUsername = $"concurrent_{suffix}";
        var commonEmail = $"concurrent_{suffix}@test.com";

        const int attempts = 6;
        var results = new ConcurrentBag<RegistrationResult>();
        Guid? createdUserId = null;

        var tasks = Enumerable.Range(0, attempts).Select(async i =>
        {
            using var localDb = CreateDbContext();
            var service = new RegistrationService(localDb, passwordHasher);

            // Khác biệt chữ hoa / chữ thường giữa các request đồng thời
            var usernameVariant = i % 2 == 0 ? commonUsername.ToUpperInvariant() : commonUsername.ToLowerInvariant();
            var emailVariant = i % 3 == 0 ? commonEmail.ToUpperInvariant() : commonEmail.ToLowerInvariant();

            var req = new RegisterRequest(
                Guid.NewGuid(),
                usernameVariant,
                $"Concurrent User {i}",
                "SafePassword@123",
                emailVariant
            );

            var res = await service.RegisterAsync(req);
            results.Add(res);
            if (res.IsSuccess && res.User is not null)
            {
                createdUserId = res.User.Id;
            }
        });

        await Task.WhenAll(tasks);

        try
        {
            var successCount = results.Count(r => r.IsSuccess);
            var failureCount = results.Count(r => !r.IsSuccess);

            // Đúng 1 tài khoản được tạo thành công
            Assert.Equal(1, successCount);
            // Các request còn lại bị từ chối với lỗi trùng lặp
            Assert.Equal(attempts - 1, failureCount);
            Assert.All(results.Where(r => !r.IsSuccess), r =>
                Assert.True(r.ErrorCode is "USERNAME_TAKEN" or "EMAIL_TAKEN"));

            // Xác nhận trong CSDL chỉ có đúng 1 tài khoản tồn tại
            using var verifyDb = CreateDbContext();
            var countInDb = await verifyDb.Users.CountAsync(u => u.NormalizedUsername == commonUsername.ToLowerInvariant());
            Assert.Equal(1, countInDb);
        }
        finally
        {
            if (createdUserId.HasValue)
            {
                using var cleanupDb = CreateDbContext();
                var userToDelete = await cleanupDb.Users.FindAsync(createdUserId.Value);
                if (userToDelete is not null)
                {
                    cleanupDb.Users.Remove(userToDelete);
                    await cleanupDb.SaveChangesAsync();
                }
            }
        }
    }

    // ========================================================================
    // 4. API HTTP Endpoint Integration Tests (POST /api/v1/auth/register)
    // ========================================================================
    [Fact]
    public async Task Http_register_endpoint_returns_201_with_valid_contract_json()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var requestId = Guid.NewGuid();
        var request = new
        {
            requestId,
            username = $"http_user_{suffix}",
            displayName = "HTTP Player",
            password = "StrongPassword@999",
            email = $"http_{suffix}@test.local"
        };

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        // Verify top-level structure: requestId, serverTime, data
        Assert.Equal(requestId.ToString(), root.GetProperty("requestId").GetString());
        Assert.True(root.TryGetProperty("serverTime", out _));

        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("id", out var idProp));
        Assert.Equal($"http_user_{suffix}".ToLowerInvariant(), data.GetProperty("username").GetString());
        Assert.Equal("HTTP Player", data.GetProperty("displayName").GetString());

        // CRITICAL SECURITY ASSERTION: Response MUST NOT contain password, passwordHash or email
        Assert.False(data.TryGetProperty("password", out _), "Public response MUST NOT expose password!");
        Assert.False(data.TryGetProperty("passwordHash", out _), "Public response MUST NOT expose passwordHash!");
        Assert.False(data.TryGetProperty("email", out _), "UserPublic MUST NOT expose email!");

        // Cleanup
        var createdId = Guid.Parse(idProp.GetString()!);
        using var db = CreateDbContext();
        var createdUser = await db.Users.FindAsync(createdId);
        if (createdUser is not null)
        {
            db.Users.Remove(createdUser);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Http_register_endpoint_returns_400_with_field_errors_on_invalid_input()
    {
        var requestId = Guid.NewGuid();
        var invalidRequest = new
        {
            requestId,
            username = "ab", // too short
            displayName = "", // empty
            password = "123", // too short
            email = "bad-email" // invalid format
        };

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        Assert.Equal(requestId.ToString(), root.GetProperty("requestId").GetString());
        var error = root.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());

        var fieldErrors = error.GetProperty("fieldErrors");
        Assert.True(fieldErrors.TryGetProperty("username", out _));
        Assert.True(fieldErrors.TryGetProperty("displayName", out _));
        Assert.True(fieldErrors.TryGetProperty("password", out _));
        Assert.True(fieldErrors.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Http_register_endpoint_returns_409_when_username_taken()
    {
        // kythu_do is seeded in database
        var request = new
        {
            requestId = Guid.NewGuid(),
            username = "KYTHU_DO", // different case
            displayName = "Thử Trùng",
            password = "ValidPassword@123",
            email = "new_unique_email@test.com"
        };

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("USERNAME_TAKEN", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Http_register_endpoint_returns_409_when_email_taken()
    {
        // red@cotuong.local is seeded in database
        var request = new
        {
            requestId = Guid.NewGuid(),
            username = $"unique_user_{Guid.NewGuid():N}"[..25],
            displayName = "Thử Trùng Email",
            password = "ValidPassword@123",
            email = "RED@COTUONG.LOCAL" // different case
        };

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("EMAIL_TAKEN", error.GetProperty("code").GetString());
    }
}
