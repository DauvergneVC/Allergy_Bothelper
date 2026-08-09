using MongoDB.Driver;
using MongoDB.Bson;

public class MongoDbContext
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return _database.GetCollection<T>(collectionName);
    }

    public async Task EnsureIndexesAsync()
    {
        var usersCollection = _database.GetCollection<User>("Users");
        await usersCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true }
            )
        );
    }

    public async Task PingAsync()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            Console.WriteLine("Successfully connected to MongoDB.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to MongoDB: {ex.Message}");
            throw;
        }
    }
}