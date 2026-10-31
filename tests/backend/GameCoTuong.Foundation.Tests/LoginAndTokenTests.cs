using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using GameCoTuong.Api.Endpoints;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Auth.Validation;
using GameCoTuong.Application.Common.Interfaces;
using GameCoTuong.Infrastructure.Auth;
using GameCoTuong.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameCoTuong.Foundation.Tests;

public sealed class LoginAndTokenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoginAndTokenTests(WebApplicationFactory<Program> factory)
    {
        _factory = TestDatabase.Configure(factory);
    }

    [Fact]
    public void Validator_rejects_null_request()
    {
        var errors = LoginRequestValidator.Validate(null);
        Assert.True(errors.ContainsKey("request"));
    }

    [Fact]
    public void Validator_rejects_empty_request_id()
    {
        var request = new LoginRequest(Guid.Empty, "valid_user", "password123");
        var errors = LoginRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("requestId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")] // < 3 chars
    [InlineData("this_username_is_way_too_long_for_the_system_limit_30")] // > 30 chars
    public void Validator_rejects_invalid_usernames(string username)
    {
        var request = new LoginRequest(Guid.NewGuid(), username, "password123");
        var errors = LoginRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("username"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")] // < 8 chars
    public void Validator_rejects_invalid_passwords(string password)
    {
        var request = new LoginRequest(Guid.NewGuid(), "valid_user", password);
        var errors = LoginRequestValidator.Validate(request);
        Assert.True(errors.ContainsKey("password"));
    }

    [Fact]
    public void Validator_accepts_valid_login_request()
    {
        var request = new LoginRequest(Guid.NewGuid(), "valid_user", "password123");
        var errors = LoginRequestValidator.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void TokenService_generates_valid_jwt_and_validates_claims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-32-bytes-long-super-secret!!",
            ExpirationMinutes = 60
        });

        var tokenService = new JwtTokenService(options);
        var user = new UserPublicDto(Guid.NewGuid(), "testuser", "Test User");

        var session = tokenService.GenerateToken(user);

        Assert.NotNull(session);
        Assert.Equal("Bearer", session.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);

        var principal = tokenService.ValidateToken(session.AccessToken);
        Assert.NotNull(principal);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(user.Username, principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal(user.DisplayName, principal.FindFirst("displayName")?.Value);
    }

    [Fact]
    public void TokenService_rejects_tampered_token_payload()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-32-bytes-long-super-secret!!",
            ExpirationMinutes = 60
        });

        var tokenService = new JwtTokenService(options);
        var user = new UserPublicDto(Guid.NewGuid(), "testuser", "Test User");
        var session = tokenService.GenerateToken(user);

        var parts = session.AccessToken.Split('.');
        Assert.Equal(3, parts.Length);

        // Tamper with payload by replacing a character
        var tamperedPayload = parts[1].EndsWith('A') ? parts[1][..^1] + "B" : parts[1][..^1] + "A";
        var tamperedToken = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        var principal = tokenService.ValidateToken(tamperedToken);
        Assert.Null(principal);
    }

    [Fact]
    public void TokenService_rejects_tampered_signature()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-32-bytes-long-super-secret!!",
            ExpirationMinutes = 60
        });

        var tokenService = new JwtTokenService(options);
        var user = new UserPublicDto(Guid.NewGuid(), "testuser", "Test User");
        var session = tokenService.GenerateToken(user);

        var parts = session.AccessToken.Split('.');
        var tamperedSig = parts[2].EndsWith('X') ? parts[2][..^1] + "Y" : parts[2][..^1] + "X";
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSig}";

        var principal = tokenService.ValidateToken(tamperedToken);
        Assert.Null(principal);
    }

    [Fact]
    public void TokenService_rejects_expired_token()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-32-bytes-long-super-secret!!",
            ExpirationMinutes = 60
        });

        var tokenService = new JwtTokenService(options);
        var user = new UserPublicDto(Guid.NewGuid(), "testuser", "Test User");

        // Expired 5 seconds ago
        var session = tokenService.GenerateToken(user, TimeSpan.FromSeconds(-5));

        var principal = tokenService.ValidateToken(session.AccessToken);
        Assert.Null(principal);
    }

    [Fact]
    public void TokenService_rejects_token_signed_with_different_key()
    {
        var options1 = Options.Create(new JwtOptions
        {
            SigningKey = "signing-key-alpha-32-bytes-minimum-key!!"
        });
        var options2 = Options.Create(new JwtOptions
        {
            SigningKey = "signing-key-beta-32-bytes-minimum-key-!!"
        });

        var service1 = new JwtTokenService(options1);
        var service2 = new JwtTokenService(options2);

        var user = new UserPublicDto(Guid.NewGuid(), "user_key_test", "Key Test");
        var session = service1.GenerateToken(user);

        var principal = service2.ValidateToken(session.AccessToken);
        Assert.Null(principal);
    }

    [Fact]
    public async Task Login_returns_401_invalid_credentials_when_user_does_not_exist()
    {
        using var client = _factory.CreateClient();

        var request = new LoginRequest(
            RequestId: Guid.NewGuid(),
            Username: "non_existent_user_9999",
            Password: "SecurePassword123!"
        );

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var errorResp = await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(errorResp);
        Assert.Equal("INVALID_CREDENTIALS", errorResp.Error.Code);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không chính xác.", errorResp.Error.Message);
        Assert.False(errorResp.Error.Retryable);
    }

    [Fact]
    public async Task Login_returns_401_invalid_credentials_when_password_is_wrong()
    {
        using var client = _factory.CreateClient();

        // 1. Register user
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var regRequest = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: $"user_{uniqueSuffix}",
            DisplayName: "Login Test User",
            Email: $"login_{uniqueSuffix}@example.com",
            Password: "CorrectPassword123!"
        );

        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // 2. Attempt login with wrong password
        var loginRequest = new LoginRequest(
            RequestId: Guid.NewGuid(),
            Username: $"user_{uniqueSuffix}",
            Password: "WrongPassword999!"
        );

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        var errorResp = await loginResponse.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(errorResp);
        Assert.Equal("INVALID_CREDENTIALS", errorResp.Error.Code);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không chính xác.", errorResp.Error.Message);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials_and_allows_case_insensitive_username()
    {
        using var client = _factory.CreateClient();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var originalUsername = $"CaseUser_{uniqueSuffix}";
        var regRequest = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: originalUsername,
            DisplayName: "Case Sensitive Test",
            Email: $"case_{uniqueSuffix}@example.com",
            Password: "MyStrongPassword123!"
        );

        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Login with uppercase username
        var loginRequest = new LoginRequest(
            RequestId: Guid.NewGuid(),
            Username: originalUsername.ToUpperInvariant(),
            Password: "MyStrongPassword123!"
        );

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var sessionResp = await loginResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>();
        Assert.NotNull(sessionResp);
        Assert.NotNull(sessionResp.Data);
        Assert.Equal("Bearer", sessionResp.Data.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(sessionResp.Data.AccessToken));
        Assert.True(sessionResp.Data.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(originalUsername, sessionResp.Data.User.Username);
        Assert.Equal("Case Sensitive Test", sessionResp.Data.User.DisplayName);

        // Check response security: does not contain password or hash
        var rawJson = await loginResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("MyStrongPassword123!", rawJson);
        Assert.DoesNotContain("passwordHash", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMe_endpoint_returns_user_profile_with_valid_token()
    {
        using var client = _factory.CreateClient();

        // 1. Register & login
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"me_user_{uniqueSuffix}";
        var regRequest = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: username,
            DisplayName: "Profile Owner",
            Email: $"profile_{uniqueSuffix}@example.com",
            Password: "Password123!"
        );

        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var loginRequest = new LoginRequest(Guid.NewGuid(), username, "Password123!");
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var sessionResp = await loginResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>();
        Assert.NotNull(sessionResp);
        var token = sessionResp.Data.AccessToken;

        // 2. Call GET /api/v1/users/me with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meResp = await meResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<UserPublicDto>>();
        Assert.NotNull(meResp);
        Assert.Equal(username, meResp.Data.Username);
        Assert.Equal("Profile Owner", meResp.Data.DisplayName);
    }

    [Fact]
    public async Task GetMe_endpoint_returns_401_when_token_is_missing()
    {
        using var client = _factory.CreateClient();

        var meResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        var errorResp = await meResponse.Content.ReadFromJsonAsync<ApiErrorEnvelope>();
        Assert.NotNull(errorResp);
        Assert.Equal("UNAUTHORIZED", errorResp.Error.Code);
    }

    [Fact]
    public async Task GetMe_endpoint_returns_401_when_token_is_tampered()
    {
        using var client = _factory.CreateClient();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"tamper_{uniqueSuffix}";
        var regRequest = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: username,
            DisplayName: "Tamper Test",
            Email: $"tamper_{uniqueSuffix}@example.com",
            Password: "Password123!"
        );

        await client.PostAsJsonAsync("/api/v1/auth/register", regRequest);
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Guid.NewGuid(), username, "Password123!"));
        var sessionResp = await loginResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>();
        var token = sessionResp!.Data.AccessToken;

        // Tamper token
        var parts = token.Split('.');
        var tamperedToken = $"{parts[0]}.{parts[1]}.tampered_signature";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var meResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_succeeds_with_valid_token()
    {
        using var client = _factory.CreateClient();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"logout_{uniqueSuffix}";
        var regRequest = new RegisterRequest(
            RequestId: Guid.NewGuid(),
            Username: username,
            DisplayName: "Logout Test",
            Email: $"logout_{uniqueSuffix}@example.com",
            Password: "Password123!"
        );

        await client.PostAsJsonAsync("/api/v1/auth/register", regRequest);
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Guid.NewGuid(), username, "Password123!"));
        var sessionResp = await loginResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<AuthSessionDto>>();
        var token = sessionResp!.Data.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var ackResp = await logoutResponse.Content.ReadFromJsonAsync<ApiSuccessResponse<AckDto>>();
        Assert.NotNull(ackResp);
        Assert.True(ackResp.Data.Acknowledged);
    }
}
