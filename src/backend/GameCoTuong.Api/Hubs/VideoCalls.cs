using GameCoTuong.Application.Game;

namespace GameCoTuong.Api.Hubs;

public sealed class VideoCalls
{
    private sealed record Call(Guid Id, Guid Caller, Guid Callee, DateTimeOffset Created, bool Answered);
    private readonly Dictionary<Guid, Call> calls = new();
    public void Validate(Guid roomId, Guid senderId, SignalInput signal)
    {
        lock (calls)
        {
            if (signal.CallId == Guid.Empty || signal.RecipientId == senderId || string.IsNullOrWhiteSpace(signal.Data) || signal.Data.Length > 65536)
                throw new GameFault("INVALID_SIGNAL", "Tín hiệu video không hợp lệ.", 400);
            calls.TryGetValue(roomId, out var call);
            if (call is not null && !call.Answered && DateTimeOffset.UtcNow - call.Created > TimeSpan.FromSeconds(90)) { calls.Remove(roomId); call = null; }
            if (signal.Type == "offer")
            {
                if (call is not null) throw new GameFault("CALL_BUSY", "Phòng đang có một cuộc gọi.");
                calls[roomId] = new Call(signal.CallId, senderId, signal.RecipientId, DateTimeOffset.UtcNow, false);
                return;
            }
            if (call is null || call.Id != signal.CallId ||
                !((call.Caller == senderId && call.Callee == signal.RecipientId) || (call.Callee == senderId && call.Caller == signal.RecipientId)))
                throw new GameFault("STALE_CALL", "Cuộc gọi không còn hiệu lực.");
            if (signal.Type == "answer")
            {
                if (call.Callee != senderId || call.Answered) throw new GameFault("INVALID_SIGNAL", "Sai bên trả lời.");
                calls[roomId] = call with { Answered = true };
            }
            else if (signal.Type is "hangup" or "reject") calls.Remove(roomId);
            else if (signal.Type != "ice") throw new GameFault("INVALID_SIGNAL", "Loại tín hiệu không được hỗ trợ.");
        }
    }
    public Guid? End(Guid roomId, Guid userId)
    {
        lock (calls)
        {
            if (!calls.TryGetValue(roomId, out var c) || (c.Caller != userId && c.Callee != userId)) return null;
            calls.Remove(roomId); return c.Id;
        }
    }
}
