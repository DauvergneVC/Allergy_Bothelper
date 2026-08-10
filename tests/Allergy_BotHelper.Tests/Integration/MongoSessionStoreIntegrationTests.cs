using MongoDB.Bson;
using MongoDB.Driver;

namespace Allergy_BotHelper.Tests.Integration;

public class MongoSessionStoreIntegrationTests : IntegrationTestBase
{
    private const long ChatId = 424242;

    [MongoFact]
    public async Task SaveThenLoad_RoundTrip_PreservesAllFields()
    {
        var session = new ChatSession
        {
            ChatId = ChatId,
            State = SessionState.AwaitingRegisterPassword,
            Role = ChatRole.Owner,
            UserId = ObjectId.GenerateNewId(),
            GuestToken = "GUEST-TOKEN",
            PendingEmail = "owner@example.com"
        };

        Assert.True(await SessionStore.SaveAsync(ChatId, session, expectedVersion: 0));

        var loaded = await SessionStore.LoadAsync(ChatId);

        Assert.Equal(ChatId, loaded.ChatId);
        Assert.Equal(SessionState.AwaitingRegisterPassword, loaded.State);
        Assert.Equal(ChatRole.Owner, loaded.Role);
        Assert.Equal(session.UserId, loaded.UserId);
        Assert.Equal("GUEST-TOKEN", loaded.GuestToken);
        Assert.Equal("owner@example.com", loaded.PendingEmail);
    }

    [MongoFact]
    public async Task FirstWrite_StoresVersionOne_AndTouchesUpdatedAt()
    {
        var session = new ChatSession { ChatId = ChatId };

        Assert.True(await SessionStore.SaveAsync(ChatId, session, expectedVersion: 0));

        var loaded = await SessionStore.LoadAsync(ChatId);
        Assert.Equal(1, loaded.Version);
        Assert.NotEqual(default, loaded.UpdatedAt);
    }

    [MongoFact]
    public async Task LoadAbsentChat_ReturnsFreshIdleSession_WithChatIdSet()
    {
        var loaded = await SessionStore.LoadAsync(424299);

        Assert.Equal(424299, loaded.ChatId);
        Assert.Equal(SessionState.Idle, loaded.State);
        Assert.Equal(ChatRole.None, loaded.Role);
        Assert.Equal(0, loaded.Version);
    }

    [MongoFact]
    public async Task MatchingVersion_Save_Succeeds_AndIncrementsVersion()
    {
        Assert.True(await SessionStore.SaveAsync(ChatId, new ChatSession { ChatId = ChatId }, expectedVersion: 0));

        var second = new ChatSession { ChatId = ChatId, State = SessionState.AwaitingLoginEmail };
        Assert.True(await SessionStore.SaveAsync(ChatId, second, expectedVersion: 1));

        var loaded = await SessionStore.LoadAsync(ChatId);
        Assert.Equal(2, loaded.Version);
        Assert.Equal(SessionState.AwaitingLoginEmail, loaded.State);
    }

    [MongoFact]
    public async Task StaleCas_InsertAndReplace_Fail_AndLeaveDocumentUnchanged()
    {
        Assert.True(await SessionStore.SaveAsync(ChatId, new ChatSession { ChatId = ChatId }, expectedVersion: 0));
        var owner = new ChatSession { ChatId = ChatId, Role = ChatRole.Owner };
        Assert.True(await SessionStore.SaveAsync(ChatId, owner, expectedVersion: 1));

        // Stale insert: current version is 2, so a fresh insert collides on the _id.
        Assert.False(await SessionStore.SaveAsync(ChatId, new ChatSession { ChatId = ChatId }, expectedVersion: 0));
        // Stale replace: expected version 1 no longer matches the stored version 2.
        Assert.False(await SessionStore.SaveAsync(ChatId, new ChatSession { ChatId = ChatId }, expectedVersion: 1));

        var loaded = await SessionStore.LoadAsync(ChatId);
        Assert.Equal(2, loaded.Version);
        Assert.Equal(ChatRole.Owner, loaded.Role);
    }

    [MongoFact]
    public async Task TtlIndex_OnUpdatedAt_Exists_WithOneHourExpiry()
    {
        await Context.EnsureIndexesAsync();

        using var cursor = await Sessions.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();

        var ttlIndex = indexes.FirstOrDefault(d =>
            d.TryGetValue("key", out var key)
            && key.AsBsonDocument.Contains("UpdatedAt")
            && d.TryGetValue("expireAfterSeconds", out var expireAfter)
            && expireAfter.ToInt64() == 3600);

        Assert.NotNull(ttlIndex);
    }
}
