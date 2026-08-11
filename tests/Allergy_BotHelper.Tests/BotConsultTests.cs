using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotConsultTests
{
    private const long ChatId = 6001;

    private static (BotAuthHandler Handler, FakeUserRepository Fake) Create()
    {
        var fake = new FakeUserRepository();
        var handler = new BotAuthHandler(
            new AuthService(fake),
            new ShareService(fake),
            new AllergyService(fake));
        return (handler, fake);
    }

    private static User SeedOwnerWith(FakeUserRepository fake, params string[] canonicalKeys)
    {
        var user = new User("owner@example.com", BcryptFixtures.Password123Hash);
        if (canonicalKeys.Length > 0)
        {
            user.Allergies = canonicalKeys.ToList();
        }
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

    [Fact]
    public async Task Consult_OwnerSingleItem_FlagsAllergenAndToken_NotUnknownCommand()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "maní", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.NotEqual(BotCopy.UnknownCommand, reply!.Text);
        Assert.Equal("Alérgeno detectado: peanut (maní)", reply.Text);
    }

    [Fact]
    public async Task Consult_OwnerList_FlagsEachMatchWithItsToken()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut", "lactose");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "maní, leche", null, CancellationToken.None);

        Assert.Equal(
            "Alérgeno detectado: peanut (maní)\nAlérgeno detectado: lactose (leche)",
            reply!.Text);
    }

    [Fact]
    public async Task Consult_ESPrefixStripped_VerdictInSpanish()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "mira esto: maní", null, CancellationToken.None);

        Assert.Equal("Alérgeno detectado: peanut (maní)", reply!.Text);
    }

    [Fact]
    public async Task Consult_ENPrefixStripped_VerdictInEnglish()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "lactose");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "check this\nleche", null, CancellationToken.None);

        Assert.Equal("Allergen detected: lactose (leche)", reply!.Text);
    }

    [Fact]
    public async Task Consult_ContainmentMatch_FlagsAllergen()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "gluten");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "contiene gluten", null, CancellationToken.None);

        Assert.Equal("Allergen detected: gluten (contiene gluten)", reply!.Text);
    }

    [Fact]
    public async Task Consult_NoMatches_EnglishSafeReply()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "agua y sal", null, CancellationToken.None);

        Assert.Equal(BotCopy.IngredientSafeEn, reply!.Text);
    }

    [Fact]
    public async Task Consult_NoMatches_SpanishSafeReply()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "azúcar", null, CancellationToken.None);

        Assert.Equal(BotCopy.IngredientSafeEs, reply!.Text);
    }

    [Fact]
    public async Task Consult_SingleItem_ProducesOneToken()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "lactose");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "leche", null, CancellationToken.None);

        Assert.Equal("Allergen detected: lactose (leche)", reply!.Text);
    }

    [Fact]
    public async Task Consult_ListInput_SplitsIntoMultipleTokens()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut", "lactose", "gluten");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(
            ChatId, session, "maní, leche\ncontiene gluten", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("peanut", reply!.Text);
        Assert.Contains("lactose", reply.Text);
        Assert.Contains("gluten", reply.Text);
    }

    [Fact]
    public async Task Consult_UnknownTokenAlongsideValid_ValidStillMatches()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "lactose");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "xyzzy, leche", null, CancellationToken.None);

        Assert.Equal("Allergen detected: lactose (leche)", reply!.Text);
    }

    [Fact]
    public async Task Consult_AllTokensUnknown_SafeReplyWithoutError()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "xyzzy, plumbus", null, CancellationToken.None);

        Assert.Equal(BotCopy.IngredientSafeEn, reply!.Text);
    }

    [Fact]
    public async Task Consult_LoggedOut_LoginPrompt_NoServiceCalls()
    {
        var (handler, fake) = Create();
        var session = LoggedOutSession();

        var reply = await handler.HandleAsync(ChatId, session, "maní", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyLoginPromptEs, reply!.Text);
        Assert.Equal(0, fake.GetUserByIdAsyncCalls);
    }

    [Fact]
    public async Task Consult_Guest_ConsultsAgainstOwnerAllergies()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = GuestSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "maní", null, CancellationToken.None);

        Assert.Equal("Alérgeno detectado: peanut (maní)", reply!.Text);
    }

    [Fact]
    public async Task Consult_NoStoredAllergies_PromptsToAddAllergensFirst()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "maní", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyUsageEs, reply!.Text);
    }

    [Fact]
    public async Task Consult_SameMessageTwice_IdenticalVerdictAndLanguage()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut", "lactose");
        var session = OwnerSession(owner);

        var first = await handler.HandleAsync(ChatId, session, "maní, leche", null, CancellationToken.None);
        var second = await handler.HandleAsync(ChatId, session, "maní, leche", null, CancellationToken.None);

        Assert.Equal(first!.Text, second!.Text);
        Assert.StartsWith("Alérgeno detectado:", first.Text);
    }

    [Fact]
    public async Task Consult_UnknownCommand_StillUnknownCommand()
    {
        var (handler, fake) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/xyz", null, CancellationToken.None);

        Assert.Equal(BotCopy.UnknownCommand, reply!.Text);
    }
}
