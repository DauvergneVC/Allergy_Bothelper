using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotListRemoveCommandTests
{
    private const long ChatId = 6001;

    private static (BotAuthHandler Handler, FakeUserRepository Fake) Create()
    {
        var fake = new FakeUserRepository();
        var handler = new BotAuthHandler(
            new AuthService(fake),
            new ShareService(fake),
            new AllergyService(fake),
            new StubOcrService());
        return (handler, fake);
    }

    private static User SeedOwner(FakeUserRepository fake)
    {
        var user = new User("owner@example.com", BcryptFixtures.Password123Hash);
        fake.Seed(user);
        return user;
    }

    private static ChatSession OwnerSession(User owner) => new()
    {
        ChatId = ChatId,
        State = SessionState.Idle,
        Role = ChatRole.Owner,
        UserId = owner.Id,
    };

    private static ChatSession GuestSession(User owner) => new()
    {
        ChatId = ChatId,
        State = SessionState.Idle,
        Role = ChatRole.Guest,
        UserId = owner.Id,
    };

    private static ChatSession LoggedOutSession() => new()
    {
        ChatId = ChatId,
        State = SessionState.Idle,
        Role = ChatRole.None,
    };

    // /listar tests

    [Fact]
    public async Task List_OwnerWithAllergens_ListsAll()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        await handler.HandleAsync(ChatId, OwnerSession(owner), "/add maní, trigo", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/listar", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains(BotCopy.AllergyListHeaderEs, reply!.Text);
        Assert.Contains("maní", reply.Text);
        Assert.Contains("trigo", reply.Text);
        Assert.Contains("peanut", reply.Text); // canonical
        Assert.Contains("gluten", reply.Text); // canonical
    }

    [Fact]
    public async Task List_OwnerWithNoAllergens_ShowsEmpty()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/listar", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyListEmptyEs, reply!.Text);
    }

    [Fact]
    public async Task List_Guest_OwnerOnlyMessage()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(ChatId, GuestSession(owner), "/listar", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyListOwnerOnlyEs, reply!.Text);
    }

    [Fact]
    public async Task List_LoggedOut_LoginPrompt()
    {
        var (handler, _) = Create();

        var reply = await handler.HandleAsync(ChatId, LoggedOutSession(), "/listar", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyListLoginPromptEs, reply!.Text);
    }

    // /remove tests

    [Fact]
    public async Task Remove_Owner_RemovesSpecifiedAllergens()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        await handler.HandleAsync(ChatId, OwnerSession(owner), "/add maní, trigo, leche", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/remove maní", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("maní", reply!.Text); // Shows display name

        // Verify only trigo and leche remain
        var listReply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/listar", null, CancellationToken.None);
        Assert.DoesNotContain("maní", listReply!.Text);
        Assert.Contains("trigo", listReply.Text);
        Assert.Contains("leche", listReply.Text);
    }

    [Fact]
    public async Task Remove_Owner_MultipleAllergens()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        await handler.HandleAsync(ChatId, OwnerSession(owner), "/add maní, trigo, leche", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/remove maní, leche", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("maní", reply!.Text);
        Assert.Contains("leche", reply.Text);

        var listReply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/listar", null, CancellationToken.None);
        Assert.DoesNotContain("maní", listReply!.Text);
        Assert.DoesNotContain("leche", listReply.Text);
        Assert.Contains("trigo", listReply.Text);
    }

    [Fact]
    public async Task Remove_Owner_UnknownAllergen_ShowsNotFound()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        await handler.HandleAsync(ChatId, OwnerSession(owner), "/add maní", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/remove pescado", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("pescado", reply!.Text); // Shows the item that wasn't found
        Assert.Contains("Not on your list", reply.Text); // English or Spanish version
    }

    [Fact]
    public async Task Remove_Owner_SynonymCanonicalizes()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        await handler.HandleAsync(ChatId, OwnerSession(owner), "/add maní", null, CancellationToken.None);

        // "cacahuete" is a synonym for "peanut"
        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/remove cacahuete", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("maní", reply!.Text); // Shows the original display name

        var listReply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/listar", null, CancellationToken.None);
        Assert.DoesNotContain("maní", listReply!.Text);
    }

    [Fact]
    public async Task Remove_BareCommand_ShowsUsage()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/remove", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyRemoveUsageEs, reply!.Text);
    }

    [Fact]
    public async Task Remove_Guest_OwnerOnlyMessage()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(ChatId, GuestSession(owner), "/remove maní", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyRemoveOwnerOnlyEs, reply!.Text);
    }

    [Fact]
    public async Task Remove_LoggedOut_LoginPrompt()
    {
        var (handler, _) = Create();

        var reply = await handler.HandleAsync(ChatId, LoggedOutSession(), "/remove maní", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.AllergyRemoveLoginPromptEs, reply!.Text);
    }

    [Fact]
    public async Task Remove_CaseSensitive_UppercaseFails()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);

        // "/Remove" is not the command (case-sensitive)
        var reply = await handler.HandleAsync(ChatId, OwnerSession(owner), "/Remove maní", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.UnknownCommand, reply!.Text);
    }
}
