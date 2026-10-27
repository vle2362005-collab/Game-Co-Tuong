namespace GameCoTuong.Api.Realtime;

public sealed record MatchReconnectExpiryDecision(string Status, string? Winner, string Reason);

public static class MatchReconnectExpiryPolicy
{
    public static MatchReconnectExpiryDecision? Decide(
        bool redExpired,
        bool blackExpired,
        bool redDisconnected,
        bool blackDisconnected)
    {
        if (redExpired && blackExpired)
        {
            return new MatchReconnectExpiryDecision("cancelled", null, "both_disconnected");
        }

        if (redExpired && !blackDisconnected)
        {
            return new MatchReconnectExpiryDecision("ended", "black", "disconnect_timeout");
        }

        if (blackExpired && !redDisconnected)
        {
            return new MatchReconnectExpiryDecision("ended", "red", "disconnect_timeout");
        }

        return null;
    }
}
