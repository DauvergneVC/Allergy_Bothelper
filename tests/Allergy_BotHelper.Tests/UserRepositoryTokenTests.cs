using System.Text;
using System.Text.RegularExpressions;
using Allergy_BotHelper.Tests.Fakes;
using MongoDB.Bson;

namespace Allergy_BotHelper.Tests;

public class UserRepositoryTokenTests
{
    private static readonly Regex TokenFormat = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    [Fact]
    public async Task GenerateToken_Returns43CharBase64UrlToken()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);

        var token = await fake.GenerateTokenAsync(owner.Id);

        Assert.Equal(43, token.Length);
        Assert.Matches(TokenFormat, token);
        Assert.Equal(token, owner.ShareToken);
    }

    [Fact]
    public async Task GenerateToken_Regenerating_ChangesTheToken()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);

        var first = await fake.GenerateTokenAsync(owner.Id);
        var second = await fake.GenerateTokenAsync(owner.Id);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task RevokeToken_SetsPersistedShareTokenToNull()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);
        await fake.GenerateTokenAsync(owner.Id);
        Assert.NotNull(owner.ShareToken);

        await fake.RevokeTokenAsync(owner.Id);

        Assert.Null(owner.ShareToken);
        Assert.Null((await fake.GetUserByIdAsync(owner.Id))!.ShareToken);
    }

    [Fact]
    public async Task RevokeToken_KnownUser_IsIdempotent()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash");
        fake.Seed(owner);

        await fake.RevokeTokenAsync(owner.Id);
        await fake.RevokeTokenAsync(owner.Id);
    }

    [Fact]
    public async Task GenerateToken_UnknownUser_ThrowsInvalidOperation()
    {
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", "hash"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fake.GenerateTokenAsync(ObjectId.GenerateNewId()));
    }

    [Fact]
    public async Task RevokeToken_UnknownUser_ThrowsInvalidOperation()
    {
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", "hash"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fake.RevokeTokenAsync(ObjectId.GenerateNewId()));
    }

    [Fact]
    public async Task GenerateAndRevoke_OnlyChangesShareToken()
    {
        var fake = new FakeUserRepository();
        var owner = new User("owner@example.com", "hash")
        {
            Allergies = new List<string> { "polen", "frutos secos" }
        };
        fake.Seed(owner);

        var emailBefore = Encoding.UTF8.GetBytes(owner.Email);
        var hashBefore = Encoding.UTF8.GetBytes(owner.PasswordHash!);
        var allergiesBefore = Encoding.UTF8.GetBytes(string.Join("\n", owner.Allergies!));

        await fake.GenerateTokenAsync(owner.Id);
        await fake.RevokeTokenAsync(owner.Id);

        Assert.Equal(emailBefore, Encoding.UTF8.GetBytes(owner.Email));
        Assert.Equal(hashBefore, Encoding.UTF8.GetBytes(owner.PasswordHash!));
        Assert.Equal(allergiesBefore, Encoding.UTF8.GetBytes(string.Join("\n", owner.Allergies!)));
        Assert.Null(owner.ShareToken);
    }
}
