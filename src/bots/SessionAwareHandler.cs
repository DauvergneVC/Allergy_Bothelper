using System.Collections.Concurrent;

/// <summary>
/// Decorator over <see cref="IBotAuthHandler"/> that owns session lifecycle: per-chat
/// serialization, load, dirty-checked persistence and compare-and-swap replay.
///
/// The <c>session</c> argument is ignored — the wrapper loads the session itself and
/// delegates a fresh instance to the inner handler. Dirty check covers the five mutable
/// fields, so non-mutating updates (<c>/help</c>, idle <c>/start</c>, unknown commands)
/// never bump <see cref="ChatSession.Version"/>. On a CAS conflict the session is reloaded
/// and the full handler pass re-runs, bounded to <see cref="MaxAttempts"/>; the last reply
/// wins.
/// </summary>
public sealed class SessionAwareHandler : IBotAuthHandler
{
    private const int MaxAttempts = 3;

    private readonly IBotAuthHandler _inner;
    private readonly ISessionStore _store;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _gates = new();

    public SessionAwareHandler(IBotAuthHandler inner, ISessionStore store)
    {
        _inner = inner;
        _store = store;
    }

    public async Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = await _store.LoadAsync(chatId, ct).ConfigureAwait(false);
            BotReply? lastReply = null;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var before = Snapshot(current);
                var reply = await _inner.HandleAsync(chatId, current, text, callbackData, ct).ConfigureAwait(false);
                lastReply = reply;

                if (!IsDirty(before, current))
                {
                    return reply;
                }

                if (await _store.SaveAsync(chatId, current, before.Version, ct).ConfigureAwait(false))
                {
                    return reply;
                }

                // CAS conflict: another instance advanced this chat. Reload and replay —
                // but only when another attempt remains.
                if (attempt < MaxAttempts - 1)
                {
                    current = await _store.LoadAsync(chatId, ct).ConfigureAwait(false);
                }
            }

            return lastReply;
        }
        finally
        {
            gate.Release();
        }
    }

    private static ChatSession Snapshot(ChatSession session) => new()
    {
        ChatId = session.ChatId,
        State = session.State,
        Role = session.Role,
        UserId = session.UserId,
        GuestToken = session.GuestToken,
        PendingEmail = session.PendingEmail,
        UpdatedAt = session.UpdatedAt,
        Version = session.Version
    };

    private static bool IsDirty(ChatSession before, ChatSession after)
        => before.State != after.State
        || before.Role != after.Role
        || before.UserId != after.UserId
        || before.GuestToken != after.GuestToken
        || before.PendingEmail != after.PendingEmail;
}
