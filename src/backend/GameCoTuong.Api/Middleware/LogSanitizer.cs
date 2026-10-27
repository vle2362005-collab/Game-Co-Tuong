namespace GameCoTuong.Api.Middleware;

public static class LogSanitizer
{
    private const string Redacted = "[REDACTED]";

    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        var tokenIndex = url.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex == -1)
        {
            tokenIndex = url.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex == -1) return url;
        }

        var equalIndex = url.IndexOf('=', tokenIndex);
        if (equalIndex == -1) return url;

        var nextAmpersand = url.IndexOf('&', equalIndex);
        if (nextAmpersand == -1)
        {
            return url.Substring(0, equalIndex + 1) + Redacted;
        }

        return url.Substring(0, equalIndex + 1) + Redacted + url.Substring(nextAmpersand);
    }

    public static string SanitizeAuthorizationHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return string.Empty;

        if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "Bearer " + Redacted;
        }

        return Redacted;
    }

    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;

        // Redact JWT pattern (starts with eyJ)
        var result = message;
        var eyJIndex = result.IndexOf("eyJ", StringComparison.Ordinal);
        while (eyJIndex != -1)
        {
            var endIndex = eyJIndex;
            while (endIndex < result.Length && !char.IsWhiteSpace(result[endIndex]) && result[endIndex] != '"' && result[endIndex] != '\'')
            {
                endIndex++;
            }

            var tokenLength = endIndex - eyJIndex;
            // Only redact if it looks like a JWT segment (at least 20 chars)
            if (tokenLength > 20)
            {
                result = result.Substring(0, eyJIndex) + Redacted + result.Substring(endIndex);
            }
            else
            {
                break;
            }

            eyJIndex = result.IndexOf("eyJ", eyJIndex + Redacted.Length, StringComparison.Ordinal);
        }

        return result;
    }
}
