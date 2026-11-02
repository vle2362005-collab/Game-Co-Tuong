namespace GameCoTuong.Api.Middleware;

public static class LogSanitizer
{
    private const string Redacted = "[REDACTED]";
    private static readonly System.Text.RegularExpressions.Regex SensitiveValue = new(
        """(?i)(["']?(?:password|passwd|pwd|token|access_token|refresh_token|authorization|secret|signingkey|credential|connectionstring|ice-pwd|ice-ufrag)["']?\s*[:=]\s*)(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\s,;&}]+)""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex JwtToken = new(
        """\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex VideoPayload = new(
        """(?is)(["']?(?:sdp|candidate|iceServers)["']?\s*[:=]).*""",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex BearerToken = new(
        """(?i)(Bearer\s+)[A-Za-z0-9._~+/-]+=*""",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        return SanitizeMessage(url);
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

        // Discard the rest of an SDP-bearing exception: it may contain multiline
        // network/device details. Correlation IDs are logged in separate fields.
        var result = VideoPayload.Replace(message, $"$1{Redacted}");
        result = JwtToken.Replace(result, Redacted);
        result = SensitiveValue.Replace(result, $"$1{Redacted}");
        return BearerToken.Replace(result, $"$1{Redacted}");
    }
}
