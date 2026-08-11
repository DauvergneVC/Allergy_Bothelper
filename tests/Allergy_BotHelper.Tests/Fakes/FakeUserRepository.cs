using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace Allergy_BotHelper.Tests.Fakes;

/// <summary>
/// In-memory IUserRepository used by the auth-service unit tests.
/// Duplicate Email inserts throw a MongoWriteException whose WriteError.Category is
/// DuplicateKey, mirroring the real unique-index behavior. GetUserByEmailAsync and
/// GetByUserShareTokenAsync record their call counts so tests can assert the exact
/// lookup traffic the service performs.
/// </summary>
public class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public int GetUserByEmailAsyncCalls { get; private set; }
    public int GetByUserShareTokenAsyncCalls { get; private set; }
    public int GetUserByIdAsyncCalls { get; private set; }

    /// <summary>
    /// When true, GetUserByEmailAsync reports no match even if the email exists.
    /// Used to simulate the register race where the pre-check misses a concurrent insert.
    /// </summary>
    public bool HideExistingUsersOnLookup { get; set; }

    public void Seed(User user) => _users.Add(user);

    public Task<User?> GetUserByIdAsync(ObjectId userId)
    {
        GetUserByIdAsyncCalls++;
        return Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));
    }

    public Task<User?> GetUserByEmailAsync(string email)
    {
        GetUserByEmailAsyncCalls++;
        if (HideExistingUsersOnLookup)
        {
            return Task.FromResult<User?>(null);
        }
        return Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
    }

    public Task<User?> GetByUserShareTokenAsync(string token)
    {
        GetByUserShareTokenAsyncCalls++;
        // Exact-match semantics: no trimming, no case folding, no fuzzy matching.
        return Task.FromResult(_users.FirstOrDefault(u => u.ShareToken == token));
    }

    public Task CreateUserAsync(User user)
    {
        if (_users.Any(u => u.Email == user.Email))
        {
            throw CreateDuplicateKeyException();
        }
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(User user)
    {
        var index = _users.FindIndex(u => u.Id == user.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("User not found.");
        }
        _users[index] = user;
        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(ObjectId userId)
    {
        _users.RemoveAll(u => u.Id == userId);
        return Task.CompletedTask;
    }

    public Task<string> GenerateTokenAsync(ObjectId user)
    {
        var index = _users.FindIndex(u => u.Id == user);
        if (index < 0)
        {
            throw new InvalidOperationException("User not found.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _users[index].ShareToken = token;
        return Task.FromResult(token);
    }

    public Task RevokeTokenAsync(ObjectId userId)
    {
        var index = _users.FindIndex(u => u.Id == userId);
        if (index < 0)
        {
            throw new InvalidOperationException("User not found.");
        }

        _users[index].ShareToken = null;
        return Task.CompletedTask;
    }

    private static MongoWriteException CreateDuplicateKeyException()
    {
        // MongoDB.Driver 3.10.0's WriteError has no public constructor; build the
        // duplicate-key write error through its internal ctor (pinned driver version).
        var writeErrorCtor = typeof(WriteError).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ServerErrorCategory), typeof(int), typeof(string), typeof(BsonDocument) },
            null) ?? throw new InvalidOperationException("WriteError internal ctor not found.");

        var writeError = (WriteError)writeErrorCtor.Invoke(new object[]
        {
            ServerErrorCategory.DuplicateKey,
            11000,
            "E11000 duplicate key error",
            new BsonDocument()
        });

        var connectionId = new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017)));
        return new MongoWriteException(connectionId, writeError, null, null);
    }
}
