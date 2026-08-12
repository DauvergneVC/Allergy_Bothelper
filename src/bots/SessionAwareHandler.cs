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
    private readonly ConcurrentDictionary<long, (SemaphoreSlim gate, DateTime lastUsed)> _gates = new();

    public SessionAwareHandler(IBotAuthHandler inner, ISessionStore store)
    {
        _inner = inner;
        _store = store;
    }

    public async Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct, byte[]? photoBytes = null)
    {
        var entry = _gates.GetOrAdd(chatId, _ => (new SemaphoreSlim(1, 1), DateTime.UtcNow));
        _gates[chatId] = (entry.gate, DateTime.UtcNow); // Update lastUsed
        await entry.gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = await _store.LoadAsync(chatId, ct).ConfigureAwait(false);
            BotReply? lastReply = null;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var before = Snapshot(current);
                var reply = await _inner.HandleAsync(chatId, current, text, callbackData, ct, photoBytes).ConfigureAwait(false);
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
            entry.gate.Release();
        }
    }

    /// <summary>
    /// Removes gates that haven't been used within the specified age. Call this periodically
    /// (e.g., from a hosted service) to prevent unbounded memory growth in long-running bots.
    /// </summary>
    /// <param name="maxAge">Maximum age of a gate before it's eligible for removal.</param>
    /// <returns>Number of gates removed.</returns>
    public int CleanupGates(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var removed = 0;

        foreach (var kvp in _gates)
        {
            if (kvp.Value.lastUsed < cutoff)
            {
                // Try to remove only if the value hasn't changed (thread-safe)
                if (_gates.TryRemove(kvp))
                {
                    kvp.Value.gate.Dispose();
                    removed++;
                }
            }
        }

        return removed;
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
