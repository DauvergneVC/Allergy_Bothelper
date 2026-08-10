using MongoDB.Driver;

namespace Allergy_BotHelper.Tests.Integration;

/// <summary>
/// Base for MongoDB integration tests. Gated on RUN_MONGO_TESTS=1: when unset, each
/// test skips cleanly via SkipException before touching any Mongo state.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private const string UsersCollectionName = "Users";
    private const string SessionsCollectionName = "Sessions";
    private readonly bool _enabled;

    protected MongoDbContext Context { get; } = null!;
    protected UserRepository Repository { get; } = null!;
    protected AuthService Auth { get; } = null!;
    protected IMongoCollection<User> Users { get; } = null!;
    protected IMongoCollection<ChatSession> Sessions { get; } = null!;
    protected MongoSessionStore SessionStore { get; } = null!;

    protected IntegrationTestBase()
    {
        if (Environment.GetEnvironmentVariable("RUN_MONGO_TESTS") != "1")
        {
            _enabled = false;
            return;
        }

        _enabled = true;
        DotNetEnv.Env.Load();

        var mongoUri = Environment.GetEnvironmentVariable("MONGO_URI")
            ?? throw new InvalidOperationException("MONGO_URI is not set.");
        var databaseName = Environment.GetEnvironmentVariable("MONGO_INITDB_DATABASE")
            ?? throw new InvalidOperationException("MONGO_INITDB_DATABASE is not set.");

        Context = new MongoDbContext(mongoUri, databaseName);
        Repository = new UserRepository(Context);
        Auth = new AuthService(Repository);
        Users = Context.GetCollection<User>(UsersCollectionName);
        Sessions = Context.GetCollection<ChatSession>(SessionsCollectionName);
        SessionStore = new MongoSessionStore(Context);
    }

    public virtual async Task InitializeAsync()
    {
        if (!_enabled)
        {
            return;
        }

        // Clean slate before index creation: leftover documents from a previous
        // crashed run would otherwise break the unique email index creation.
        await Users.DeleteManyAsync(FilterDefinition<User>.Empty);
        await Sessions.DeleteManyAsync(FilterDefinition<ChatSession>.Empty);
        await Context.EnsureIndexesAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (!_enabled)
        {
            return;
        }

        await Users.DeleteManyAsync(FilterDefinition<User>.Empty);
        await Sessions.DeleteManyAsync(FilterDefinition<ChatSession>.Empty);
    }
}
