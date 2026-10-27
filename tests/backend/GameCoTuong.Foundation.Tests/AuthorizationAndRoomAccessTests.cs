using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Api.Hubs;
using GameCoTuong.Api.Middleware;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Services;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Domain.Entities;
using GameCoTuong.Infrastructure.Auth;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameCoTuong.Foundation.Tests;

public sealed class AuthorizationAndRoomAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=GameCoTuong;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationAndRoomAccessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new GameDbContext(options);
    }

    private async Task<(User User, string Token)> CreateUserAndLoginAsync(HttpClient client, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"{prefix}_{suffix}";
        var password = "Password123!";

        var reg = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: username,
            DisplayName: $"Display {username}",
            Email: $"{username}@example.com",
            Password: password
        );
        var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", reg);
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var login = new LoginRequest(Guid.NewGuid(), username, password);
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", login);
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var session = await loginResp.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>();
        Assert.NotNull(session);

        using var db = CreateDbContext();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Username == username);

        return (user, session.Data.AccessToken);
    }

    // ========================================================================
    // 1. Missing Token Tests (401 UNAUTHORIZED)
    // ========================================================================

    [Fact]
    public async Task StartRoom_returns_401_when_token_is_missing()
    {
        using var client = _factory.CreateClient();
        var roomId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start", new StartRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task CloseRoom_returns_401_when_token_is_missing()
    {
        using var client = _factory.CreateClient();
        var roomId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/close", new CloseRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task SubmitMove_returns_401_when_token_is_missing()
    {
        using var client = _factory.CreateClient();
        var matchId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", new SubmitMoveRequest(Guid.NewGuid(), "C2-C5"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task VideoSignal_returns_401_when_token_is_missing()
    {
        using var client = _factory.CreateClient();
        var roomId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/video/signal", new VideoSignalRequest(Guid.NewGuid(), "offer", "sdp-data"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    // ========================================================================
    // 2. Tampered / Expired Token Tests (401 UNAUTHORIZED)
    // ========================================================================

    [Fact]
    public async Task ProtectedEndpoints_reject_tampered_token()
    {
        using var client = _factory.CreateClient();
        var (_, validToken) = await CreateUserAndLoginAsync(client, "tamper_test");

        var parts = validToken.Split('.');
        var tamperedToken = $"{parts[0]}.{parts[1]}.bad_signature_here";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var response = await client.PostAsJsonAsync($"/api/v1/rooms/{Guid.NewGuid()}/start", new StartRoomRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_reject_expired_token()
    {
        using var client = _factory.CreateClient();
        var (user, _) = await CreateUserAndLoginAsync(client, "expired_test");

        var tokenService = _factory.Services.GetRequiredService<ITokenService>();
        var userDto = new UserPublicDto(user.Id, user.Username, user.DisplayName);
        var expiredSession = tokenService.GenerateToken(userDto, TimeSpan.FromSeconds(-60));
        var expiredToken = expiredSession.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var response = await client.PostAsJsonAsync($"/api/v1/rooms/{Guid.NewGuid()}/start", new StartRoomRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ========================================================================
    // 3. SignalR Authentication Tests
    // ========================================================================

    [Fact]
    public async Task SignalR_negotiate_rejects_unauthenticated_request()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/hubs/game/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task SignalR_negotiate_rejects_invalid_token()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
        var response = await client.PostAsync("/hubs/game/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignalR_negotiate_accepts_valid_token_in_bearer_header()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await CreateUserAndLoginAsync(client, "sig_bearer");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync("/hubs/game/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("connectionId", content);
    }

    [Fact]
    public async Task SignalR_negotiate_accepts_valid_token_in_query_parameter()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await CreateUserAndLoginAsync(client, "sig_query");

        var response = await client.PostAsync($"/hubs/game/negotiate?negotiateVersion=1&access_token={token}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("connectionId", content);
    }

    // ========================================================================
    // 4. Room Host Authorization & Anti-Spoofing Tests
    // ========================================================================

    [Fact]
    public async Task StartRoom_allows_host_and_blocks_guest_and_outsider()
    {
        using var client = _factory.CreateClient();
        var (hostUser, hostToken) = await CreateUserAndLoginAsync(client, "host_u");
        var (guestUser, guestToken) = await CreateUserAndLoginAsync(client, "guest_u");
        var (outsiderUser, outsiderToken) = await CreateUserAndLoginAsync(client, "outsider_u");

        var roomId = Guid.NewGuid();
        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Test Room",
                HostUserId = hostUser.Id,
                Status = "waiting",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = hostUser.Id,
                Side = "red",
                IsReady = true,
                JoinedAt = DateTimeOffset.UtcNow
            });

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = guestUser.Id,
                Side = "black",
                IsReady = true,
                JoinedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // 1. Host calls start -> 200 OK
        using var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hostToken);
        var hostResp = await hostClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start", new StartRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, hostResp.StatusCode);

        // 2. Guest calls start -> 403 Forbidden
        using var guestClient = _factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        var guestResp = await guestClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start", new StartRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, guestResp.StatusCode);

        // 3. Outsider calls start -> 403 Forbidden
        using var outsiderClient = _factory.CreateClient();
        outsiderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderToken);
        var outsiderResp = await outsiderClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start", new StartRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, outsiderResp.StatusCode);

        // 4. Anti-Spoofing: Outsider tries to spoof Host's userId in request body -> Still rejected with 403
        var spoofResp = await outsiderClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/start", new StartRoomRequest(Guid.NewGuid(), hostUser.Id));
        Assert.Equal(HttpStatusCode.Forbidden, spoofResp.StatusCode);
    }

    [Fact]
    public async Task CloseRoom_allows_host_and_blocks_guest_and_outsider()
    {
        using var client = _factory.CreateClient();
        var (hostUser, hostToken) = await CreateUserAndLoginAsync(client, "close_h");
        var (guestUser, guestToken) = await CreateUserAndLoginAsync(client, "close_g");

        var roomId = Guid.NewGuid();
        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Close Test Room",
                HostUserId = hostUser.Id,
                Status = "waiting",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = hostUser.Id,
                Side = "red",
                JoinedAt = DateTimeOffset.UtcNow
            });

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = guestUser.Id,
                Side = "black",
                JoinedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Guest cannot close room
        using var guestClient = _factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        var guestResp = await guestClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/close", new CloseRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, guestResp.StatusCode);

        // Host can close room
        using var hostClient = _factory.CreateClient();
        hostClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hostToken);
        var hostResp = await hostClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/close", new CloseRoomRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, hostResp.StatusCode);
    }

    // ========================================================================
    // 5. Match Turn Authorization & Anti-Spoofing Tests
    // ========================================================================

    [Fact]
    public async Task SubmitMove_authorizes_turn_and_blocks_opponent_outsider_and_spoofing()
    {
        using var client = _factory.CreateClient();
        var (redUser, redToken) = await CreateUserAndLoginAsync(client, "m_red");
        var (blackUser, blackToken) = await CreateUserAndLoginAsync(client, "m_blk");
        var (outsiderUser, outsiderToken) = await CreateUserAndLoginAsync(client, "m_out");

        var roomId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Match Room",
                HostUserId = redUser.Id,
                Status = "playing",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            var match = new Match
            {
                Id = matchId,
                RoomId = roomId,
                Status = "playing",
                SideToMove = "red",
                InitialBoardJson = "{}",
                CurrentBoardJson = "{}",
                StartedAt = DateTimeOffset.UtcNow
            };
            db.Matches.Add(match);

            db.MatchPlayers.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = redUser.Id,
                Side = "red"
            });

            db.MatchPlayers.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = blackUser.Id,
                Side = "black"
            });

            room.CurrentMatchId = matchId;
            await db.SaveChangesAsync();
        }

        // 1. Red user (turn is red) -> 200 OK
        using var redClient = _factory.CreateClient();
        redClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", redToken);
        var redResp = await redClient.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", new SubmitMoveRequest(Guid.NewGuid(), "P2-5"));
        Assert.Equal(HttpStatusCode.OK, redResp.StatusCode);

        // 2. Black user (not their turn) -> 403 Forbidden (NOT_YOUR_TURN)
        using var blackClient = _factory.CreateClient();
        blackClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", blackToken);
        var blackResp = await blackClient.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", new SubmitMoveRequest(Guid.NewGuid(), "P2-5"));
        Assert.Equal(HttpStatusCode.Forbidden, blackResp.StatusCode);
        var blackError = await blackResp.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.Equal("NOT_YOUR_TURN", blackError!.Error.Code);

        // 3. Outsider -> 403 Forbidden (NOT_MATCH_PLAYER)
        using var outsiderClient = _factory.CreateClient();
        outsiderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderToken);
        var outResp = await outsiderClient.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", new SubmitMoveRequest(Guid.NewGuid(), "P2-5"));
        Assert.Equal(HttpStatusCode.Forbidden, outResp.StatusCode);
        var outError = await outResp.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.Equal("NOT_MATCH_PLAYER", outError!.Error.Code);

        // 4. Anti-Spoofing: Outsider supplies Red user's ID in body -> Server still detects outsider identity from token and blocks with 403
        var spoofResp = await outsiderClient.PostAsJsonAsync($"/api/v1/matches/{matchId}/moves", new SubmitMoveRequest(Guid.NewGuid(), "P2-5", redUser.Id));
        Assert.Equal(HttpStatusCode.Forbidden, spoofResp.StatusCode);
        var spoofError = await spoofResp.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.Equal("NOT_MATCH_PLAYER", spoofError!.Error.Code);
    }

    // ========================================================================
    // 6. Video Call Authorization Tests
    // ========================================================================

    [Fact]
    public async Task VideoSignal_allows_active_members_and_blocks_outsiders_and_left_members()
    {
        using var client = _factory.CreateClient();
        var (memberUser, memberToken) = await CreateUserAndLoginAsync(client, "vid_mem");
        var (leftUser, leftToken) = await CreateUserAndLoginAsync(client, "vid_left");
        var (outsiderUser, outsiderToken) = await CreateUserAndLoginAsync(client, "vid_out");

        var roomId = Guid.NewGuid();
        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Video Room",
                HostUserId = memberUser.Id,
                Status = "waiting",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);

            // Active member
            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = memberUser.Id,
                Side = "red",
                JoinedAt = DateTimeOffset.UtcNow,
                LeftAt = null
            });

            // Member who left
            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = leftUser.Id,
                Side = "black",
                JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                LeftAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

            await db.SaveChangesAsync();
        }

        // Active member -> 200 OK
        using var memClient = _factory.CreateClient();
        memClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var memResp = await memClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/video/signal", new VideoSignalRequest(Guid.NewGuid(), "offer", "sdp-1"));
        Assert.Equal(HttpStatusCode.OK, memResp.StatusCode);

        // Left member -> 403 Forbidden
        using var leftClient = _factory.CreateClient();
        leftClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", leftToken);
        var leftResp = await leftClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/video/signal", new VideoSignalRequest(Guid.NewGuid(), "offer", "sdp-2"));
        Assert.Equal(HttpStatusCode.Forbidden, leftResp.StatusCode);

        // Outsider -> 403 Forbidden
        using var outClient = _factory.CreateClient();
        outClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderToken);
        var outResp = await outClient.PostAsJsonAsync($"/api/v1/rooms/{roomId}/video/signal", new VideoSignalRequest(Guid.NewGuid(), "offer", "sdp-3"));
        Assert.Equal(HttpStatusCode.Forbidden, outResp.StatusCode);
    }

    // ========================================================================
    // 7. SignalR GameHub Unit / Direct Tests
    // ========================================================================

    [Fact]
    public async Task GameHub_rejects_unauthorized_actions_with_HubException()
    {
        using var client = _factory.CreateClient();
        var (hostUser, _) = await CreateUserAndLoginAsync(client, "hub_host");
        var (outsiderUser, _) = await CreateUserAndLoginAsync(client, "hub_out");

        var roomId = Guid.NewGuid();
        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Hub Test Room",
                HostUserId = hostUser.Id,
                Status = "waiting",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = hostUser.Id,
                Side = "red",
                JoinedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var roomAuthService = scope.ServiceProvider.GetRequiredService<IRoomAuthorizationService>();
        var hub = new GameHub(roomAuthService);

        // Mock HubCallerContext for outsider
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, outsiderUser.Id.ToString()),
            new Claim(ClaimTypes.Name, outsiderUser.Username)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var mockContext = new TestHubCallerContext(principal);
        hub.Context = mockContext;

        // Outsider attempting to start room -> HubException
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.StartRoom(roomId));
        Assert.Contains("FORBIDDEN", ex.Message);

        // Outsider attempting to close room -> HubException
        var closeEx = await Assert.ThrowsAsync<HubException>(() => hub.CloseRoom(roomId));
        Assert.Contains("FORBIDDEN", closeEx.Message);

        // Outsider attempting to send video signal -> HubException
        var vidEx = await Assert.ThrowsAsync<HubException>(() => hub.SendVideoSignal(roomId, "data"));
        Assert.Contains("FORBIDDEN", vidEx.Message);
    }

    // ========================================================================
    // 8. Log Sanitizer Tests (BR-12)
    // ========================================================================

    [Fact]
    public void LogSanitizer_redacts_tokens_from_url_headers_and_messages()
    {
        var rawJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.doNotLogThisSecretSignature12345";

        // URL query parameter redaction
        var urlWithToken = $"/hubs/game?negotiateVersion=1&access_token={rawJwt}&transport=webSockets";
        var sanitizedUrl = LogSanitizer.SanitizeUrl(urlWithToken);
        Assert.DoesNotContain(rawJwt, sanitizedUrl);
        Assert.Contains("access_token=[REDACTED]", sanitizedUrl);
        Assert.Contains("transport=webSockets", sanitizedUrl);

        // Authorization header redaction
        var authHeader = $"Bearer {rawJwt}";
        var sanitizedHeader = LogSanitizer.SanitizeAuthorizationHeader(authHeader);
        Assert.DoesNotContain(rawJwt, sanitizedHeader);
        Assert.Equal("Bearer [REDACTED]", sanitizedHeader);

        // Arbitrary log message containing token
        var logMessage = $"User connection attempt with token: {rawJwt} failed due to signature.";
        var sanitizedMessage = LogSanitizer.SanitizeMessage(logMessage);
        Assert.DoesNotContain(rawJwt, sanitizedMessage);
        Assert.Contains("[REDACTED]", sanitizedMessage);
    }

    // ========================================================================
    // 9. GCT-30: Session Policy & Profile Retrieval Tests
    // ========================================================================

    [Fact]
    public async Task GetMe_cannot_be_spoofed_by_passing_arbitrary_user_id()
    {
        using var client = _factory.CreateClient();
        var (userA, tokenA) = await CreateUserAndLoginAsync(client, "prof_a");
        var (userB, _) = await CreateUserAndLoginAsync(client, "prof_b");

        // User A tries to pass User B's ID in query string
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await client.GetAsync($"/api/v1/users/me?userId={userB.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respData = await response.Content.ReadFromJsonAsync<ApiSuccessResponse<UserPublicDto>>();
        Assert.NotNull(respData);

        // Must strictly return User A's profile, completely ignoring userB.Id query parameter!
        Assert.Equal(userA.Id, respData.Data.Id);
        Assert.Equal(userA.Username, respData.Data.Username);
        Assert.NotEqual(userB.Id, respData.Data.Id);
    }

    [Fact]
    public async Task Logout_revokes_token_and_rejects_subsequent_requests()
    {
        using var client = _factory.CreateClient();
        var (user, token) = await CreateUserAndLoginAsync(client, "rev_test");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Initial request with token succeeds
        var beforeResp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, beforeResp.StatusCode);

        // 2. Perform logout
        var logoutResp = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResp.StatusCode);
        var logoutData = await logoutResp.Content.ReadFromJsonAsync<ApiSuccessResponse<LogoutResponseDto>>();
        Assert.NotNull(logoutData);
        Assert.True(logoutData.Data.Acknowledged);
        Assert.True(logoutData.Data.Revoked);
        Assert.Contains("thu hồi", logoutData.Data.PolicyMessage);

        // 3. Subsequent API request with the same revoked token must be rejected with 401 UNAUTHORIZED
        var afterResp = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterResp.StatusCode);
        var errEnvelope = await afterResp.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(errEnvelope);
        Assert.Equal("UNAUTHORIZED", errEnvelope.Error.Code);

        // 4. SignalR connection attempt with the revoked token must also be rejected
        var sigResp = await client.PostAsync($"/hubs/game/negotiate?negotiateVersion=1&access_token={token}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, sigResp.StatusCode);
    }

    [Fact]
    public async Task Logout_closes_waiting_room_and_vacates_membership_for_host()
    {
        using var client = _factory.CreateClient();
        var (hostUser, hostToken) = await CreateUserAndLoginAsync(client, "host_rm_close");

        var roomId = Guid.NewGuid();
        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Waiting Room Host Logout",
                HostUserId = hostUser.Id,
                Status = "waiting",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = hostUser.Id,
                Side = "red",
                JoinedAt = DateTimeOffset.UtcNow,
                LeftAt = null
            });

            await db.SaveChangesAsync();
        }

        // Host logs out
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hostToken);
        var logoutResp = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResp.StatusCode);

        // Verify room is closed and membership left
        using (var db = CreateDbContext())
        {
            var room = await db.Rooms.AsNoTracking().FirstAsync(r => r.Id == roomId);
            Assert.Equal("closed", room.Status);
            Assert.NotNull(room.ClosedAt);

            var membership = await db.RoomMemberships.AsNoTracking().FirstAsync(m => m.RoomId == roomId && m.UserId == hostUser.Id);
            Assert.NotNull(membership.LeftAt);
            Assert.False(membership.IsConnected);
        }
    }

    [Fact]
    public async Task Logout_during_active_match_resigns_match_and_awards_opponent()
    {
        using var client = _factory.CreateClient();
        var (redUser, redToken) = await CreateUserAndLoginAsync(client, "red_res_logout");
        var (blackUser, _) = await CreateUserAndLoginAsync(client, "blk_res_logout");

        var roomId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        using (var db = CreateDbContext())
        {
            var room = new Room
            {
                Id = roomId,
                Code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Active Match Logout Room",
                HostUserId = redUser.Id,
                Status = "playing",
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();

            var match = new Match
            {
                Id = matchId,
                RoomId = roomId,
                Status = "playing",
                SideToMove = "red",
                InitialBoardJson = "{}",
                CurrentBoardJson = "{}",
                StartedAt = DateTimeOffset.UtcNow
            };
            db.Matches.Add(match);

            db.MatchPlayers.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = redUser.Id,
                Side = "red"
            });

            db.MatchPlayers.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                UserId = blackUser.Id,
                Side = "black"
            });

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = redUser.Id,
                Side = "red",
                JoinedAt = DateTimeOffset.UtcNow,
                LeftAt = null
            });

            db.RoomMemberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                UserId = blackUser.Id,
                Side = "black",
                JoinedAt = DateTimeOffset.UtcNow,
                LeftAt = null
            });

            room.CurrentMatchId = matchId;
            await db.SaveChangesAsync();
        }

        // Red player logs out while match is playing
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", redToken);
        var logoutResp = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResp.StatusCode);

        // Verify match finished with opponent (black) winning by resignation
        using (var db = CreateDbContext())
        {
            var match = await db.Matches.AsNoTracking().FirstAsync(m => m.Id == matchId);
            Assert.Equal("finished", match.Status);
            Assert.Equal("black", match.Winner);
            Assert.Equal("resignation", match.ResultReason);
            Assert.NotNull(match.EndedAt);

            var redMembership = await db.RoomMemberships.AsNoTracking().FirstAsync(m => m.RoomId == roomId && m.UserId == redUser.Id);
            Assert.NotNull(redMembership.LeftAt);
            Assert.False(redMembership.IsConnected);
        }
    }
}

// Test helper class to simulate HubCallerContext
internal sealed class TestHubCallerContext : HubCallerContext
{
    private readonly ClaimsPrincipal _user;
    private readonly string _connectionId = Guid.NewGuid().ToString("N");

    public TestHubCallerContext(ClaimsPrincipal user)
    {
        _user = user;
    }

    public override string ConnectionId => _connectionId;
    public override string? UserIdentifier => _user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public override ClaimsPrincipal? User => _user;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public override void Abort() { }
}
