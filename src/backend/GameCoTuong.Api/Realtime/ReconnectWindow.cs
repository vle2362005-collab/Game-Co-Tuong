namespace GameCoTuong.Api.Realtime;

public static class ReconnectWindow
{
    public static DateTimeOffset StartDeadline(DateTimeOffset now, int remainingMs) =>
        now.AddMilliseconds(Math.Clamp(remainingMs, 0, MatchReconnectService.RecoveryWindowMs));

    public static int GetRemainingMilliseconds(DateTimeOffset deadline, DateTimeOffset now) =>
        Math.Clamp((int)Math.Floor((deadline - now).TotalMilliseconds), 0, MatchReconnectService.RecoveryWindowMs);
}
