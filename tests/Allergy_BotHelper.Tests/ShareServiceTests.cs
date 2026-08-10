using System.Text.RegularExpressions;
using Allergy_BotHelper.Tests.Fakes;
using MongoDB.Bson;

namespace Allergy_BotHelper.Tests;

public class ShareServiceTests
{
    [Fact]
    public async Task GenerateToken_DelegatesToRepository_AndPersistsToken()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);
        var service = new ShareService(fake);

        var token = await service.GenerateTokenAsync(owner.Id);

        Assert.Equal(token, owner.ShareToken);
        Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), token);
    }

    [Fact]
    public async Task RevokeToken_DelegatesToRepository_AndClearsToken()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);
        await fake.GenerateTokenAsync(owner.Id);
        var service = new ShareService(fake);

        await service.RevokeTokenAsync(owner.Id);

        Assert.Null(owner.ShareToken);
    }

    [Fact]
    public async Task GenerateToken_UnknownUser_ThrowsInvalidOperation()
    {
        var fake = new FakeUserRepository();
        var service = new ShareService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateTokenAsync(ObjectId.GenerateNewId()));
    }

    [Fact]
    public async Task RevokeToken_UnknownUser_ThrowsInvalidOperation()
    {
        var fake = new FakeUserRepository();
        var service = new ShareService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RevokeTokenAsync(ObjectId.GenerateNewId()));
    }
}
