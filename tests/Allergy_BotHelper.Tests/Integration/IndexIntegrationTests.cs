using MongoDB.Bson;
using MongoDB.Driver;

namespace Allergy_BotHelper.Tests.Integration;

public class IndexIntegrationTests : IntegrationTestBase
{
    [MongoFact]
    public async Task EmailIndex_Exists_AndIsUnique()
    {
        var index = await FindIndexByNameAsync("Email_1", "Email");

        Assert.NotNull(index);
        Assert.True(IsUnique(index!));
    }

    [MongoFact]
    public async Task ShareTokenIndex_Exists_AndIsNotUnique()
    {
        var index = await FindIndexByNameAsync("ShareToken_1", "ShareToken");

        Assert.NotNull(index);
        Assert.False(IsUnique(index!));
    }

    [MongoFact]
    public async Task ShareToken_Lookup_IsExactMatch()
    {
        var owner = await Auth.RegisterAsync("owner@example.com", "password123");
        owner.ShareToken = "TOKEN-ABC";
        await Repository.UpdateUserAsync(owner);

        var exactMatch = await Repository.GetByUserShareTokenAsync("TOKEN-ABC");
        var caseVariant = await Repository.GetByUserShareTokenAsync("token-abc");

        Assert.NotNull(exactMatch);
        Assert.Equal(owner.Id, exactMatch!.Id);
        Assert.Null(caseVariant);
    }

    private async Task<BsonDocument?> FindIndexByNameAsync(string expectedName, string keyField)
    {
        using var cursor = await Users.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();

        return indexes.FirstOrDefault(d =>
            (d.TryGetValue("name", out var name) && name.AsString == expectedName)
            || (d.TryGetValue("key", out var key) && key.AsBsonDocument.Contains(keyField)));
    }

    private static bool IsUnique(BsonDocument index)
        => index.TryGetValue("unique", out var unique) && unique.AsBoolean;
}
