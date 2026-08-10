using MongoDB.Driver;

public class MongoSessionStore : ISessionStore
{
    private const string CollectionName = "Sessions";

    private readonly IMongoCollection<ChatSession> _collection;

    public MongoSessionStore(MongoDbContext context)
    {
        _collection = context.GetCollection<ChatSession>(CollectionName);
    }

    public async Task<ChatSession> LoadAsync(long chatId, CancellationToken ct = default)
    {
        var session = await _collection
            .Find(s => s.ChatId == chatId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return session ?? new ChatSession { ChatId = chatId };
    }

    public async Task<bool> SaveAsync(long chatId, ChatSession session, long expectedVersion, CancellationToken ct = default)
    {
        session.ChatId = chatId;
        session.UpdatedAt = DateTime.UtcNow;

        if (expectedVersion == 0)
        {
            // First write: insert. A duplicate-key error means a concurrent writer already
            // created the document; the caller must reload and replay (CAS conflict).
            session.Version = 1;
            try
            {
                await _collection.InsertOneAsync(session, cancellationToken: ct).ConfigureAwait(false);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        // Compare-and-swap: replace only when the stored version matches the one we loaded.
        session.Version = expectedVersion + 1;
        var filter = Builders<ChatSession>.Filter.Eq(s => s.ChatId, chatId)
            & Builders<ChatSession>.Filter.Eq(s => s.Version, expectedVersion);
        var result = await _collection.ReplaceOneAsync(filter, session, cancellationToken: ct).ConfigureAwait(false);
        return result.ModifiedCount == 1;
    }
}
