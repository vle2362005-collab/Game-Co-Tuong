using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameCoTuong.Application.Auth.Models;
using GameCoTuong.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace GameCoTuong.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
    private static readonly byte[] FallbackKey = Encoding.UTF8.GetBytes("GameCoTuong-DefaultSecretKeyForLocalDevAndTests-MustBeAtLeast32Bytes!");
    private readonly JwtOptions _options;
    private readonly byte[] _key;

    private sealed record JwtHeader(
        [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("typ")] string Typ
    );

    private sealed record JwtPayload(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("jti")] string Jti,
        [property: JsonPropertyName("iss")] string Iss,
        [property: JsonPropertyName("aud")] string Aud,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp
    );

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value ?? new JwtOptions();
        if (!string.IsNullOrWhiteSpace(_options.SigningKey) && _options.SigningKey.Length >= 16)
        {
            _key = Encoding.UTF8.GetBytes(_options.SigningKey);
        }
        else
        {
            _key = FallbackKey;
        }
    }

    public AuthSessionDto GenerateToken(UserPublicDto user, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTimeOffset.UtcNow;
        var expiration = lifetime.HasValue
            ? now.Add(lifetime.Value)
            : now.AddMinutes(_options.ExpirationMinutes > 0 ? _options.ExpirationMinutes : 1440);

        var header = new JwtHeader("HS256", "JWT");
        var payload = new JwtPayload(
            Sub: user.Id.ToString(),
            Name: user.Username,
            DisplayName: user.DisplayName,
            Jti: Guid.NewGuid().ToString("N"),
            Iss: _options.Issuer,
            Aud: _options.Audience,
            Iat: now.ToUnixTimeSeconds(),
            Exp: expiration.ToUnixTimeSeconds()
        );

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";

        var signature = ComputeHmacSha256(unsignedToken, _key);
        var encodedSignature = Base64UrlEncode(signature);

        var token = $"{unsignedToken}.{encodedSignature}";

        return new AuthSessionDto(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresAt: expiration,
            User: user
        );
    }

    public ClaimsPrincipal? ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var encodedHeader = parts[0];
        var encodedPayload = parts[1];
        var encodedSignature = parts[2];

        // 1. Verify signature using constant-time comparison
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";
        var expectedSignature = ComputeHmacSha256(unsignedToken, _key);

        byte[] providedSignature;
        try
        {
            providedSignature = Base64UrlDecode(encodedSignature);
        }
        catch (FormatException)
        {
            return null;
        }

        if (providedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return null; // Tampered or invalid signature
        }

        // 2. Decode header and verify alg
        try
        {
            var headerBytes = Base64UrlDecode(encodedHeader);
            var header = JsonSerializer.Deserialize<JwtHeader>(headerBytes);
            if (header is null || !string.Equals(header.Alg, "HS256", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        // 3. Decode payload and verify expiration, issuer, audience
        JwtPayload? payload;
        try
        {
            var payloadBytes = Base64UrlDecode(encodedPayload);
            payload = JsonSerializer.Deserialize<JwtPayload>(payloadBytes);
            if (payload is null)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        var currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.Exp <= currentEpoch)
        {
            return null; // Expired
        }

        if (!string.IsNullOrEmpty(_options.Issuer) && !string.Equals(payload.Iss, _options.Issuer, StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(_options.Audience) && !string.Equals(payload.Aud, _options.Audience, StringComparison.Ordinal))
        {
            return null;
        }

        // 4. Construct ClaimsPrincipal
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload.Sub),
            new(ClaimTypes.Name, payload.Name),
            new("displayName", payload.DisplayName),
            new("jti", payload.Jti)
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }

    private static byte[] ComputeHmacSha256(string data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Base64UrlEncode(byte[] input)
    {
        var output = Convert.ToBase64String(input);
        output = output.Split('=')[0]; // Remove padding
        output = output.Replace('+', '-');
        output = output.Replace('/', '_');
        return output;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }
}
