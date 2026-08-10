namespace Allergy_BotHelper.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ISessionStore"/> with compare-and-swap emulation, used by the
/// <c>SessionAwareHandler</c> unit tests. <see cref="ConflictOnce"/> makes the next save
/// fail as if a concurrent writer won the CAS, so tests can exercise the replay path.
/// </summary>
public class FakeSessionStore : ISessionStore
{
    private readonly Dictionary<long, ChatSession> _sessions = new();
    private readonly object _lock = new();

    public bool ConflictOnce { get; set; }
    public bool FailAlways { get; set; }
    public int LoadCalls { get; private set; }
    public int SaveCalls { get; private set; }

    public Task<ChatSession> LoadAsync(long chatId, CancellationToken ct = default)
    {
        LoadCalls++;
        lock (_lock)
        {
            return Task.FromResult(_sessions.TryGetValue(chatId, out var session) ? Clone(session) : new ChatSession { ChatId = chatId });
        }
    }

    public Task<bool> SaveAsync(long chatId, ChatSession session, long expectedVersion, CancellationToken ct = default)
    {
        SaveCalls++;
        lock (_lock)
        {
            if (FailAlways)
            {
                return Task.FromResult(false);
            }

            if (ConflictOnce)
            {
                ConflictOnce = false;
                return Task.FromResult(false);
            }

            if (_sessions.TryGetValue(chatId, out var existing))
            {
                if (existing.Version != expectedVersion)
                {
                    return Task.FromResult(false);
                }
            }
            else if (expectedVersion != 0)
            {
                return Task.FromResult(false);
            }

            session.ChatId = chatId;
            session.Version = expectedVersion + 1;
            session.UpdatedAt = DateTime.UtcNow;
            _sessions[chatId] = Clone(session);
            return Task.FromResult(true);
        }
    }

    public ChatSession? Lookup(long chatId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(chatId, out var session) ? Clone(session) : null;
        }
    }

    private static ChatSession Clone(ChatSession source) => new()
    {
        ChatId = source.ChatId,
        State = source.State,
        Role = source.Role,
        UserId = source.UserId,
        GuestToken = source.GuestToken,
        PendingEmail = source.PendingEmail,
        UpdatedAt = source.UpdatedAt,
        Version = source.Version
    };
}
