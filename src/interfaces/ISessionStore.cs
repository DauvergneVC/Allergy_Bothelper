public interface ISessionStore
{
    /// <summary>
    /// Returns the persisted session for the chat, or a fresh idle session (version 0)
    /// with <see cref="ChatSession.ChatId"/> set when none is stored.
    /// </summary>
    Task<ChatSession> LoadAsync(long chatId, CancellationToken ct = default);

    /// <summary>
    /// Persists the session only when the stored version equals <paramref name="expectedVersion"/>.
    /// Returns <c>false</c> on a compare-and-swap conflict (document unchanged); infrastructure
    /// errors are surfaced as exceptions.
    /// </summary>
    Task<bool> SaveAsync(long chatId, ChatSession session, long expectedVersion, CancellationToken ct = default);
}
