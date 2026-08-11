using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotAddCommandTests
{
    private const long ChatId = 5001;

    private static (BotAuthHandler Handler, FakeUserRepository Fake) Create()
    {
        var fake = new FakeUserRepository();
        var handler = new BotAuthHandler(
            new AuthService(fake),
            new ShareService(fake),
            new AllergyService(fake));
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

    private static async Task<User> OwnerWithStoredAsync(FakeUserRepository fake, User owner)
    {
        var stored = await fake.GetUserByIdAsync(owner.Id);
        return stored!;
    }

    [Fact]
    public async Task Add_SingleItem_PersistsCanonicalAndEchoesDisplay()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.Equal("Alergias agregadas: maní", reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Equal(new[] { "peanut" }, persisted.Allergies);
        Assert.Equal(new[] { "maní" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_MixedSeparators_SplitsIntoFourItems_CanonicalDedupeStoresGlutenOnce()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(
            ChatId, session, "/add maní, trigo; avena\nlácteos", null, CancellationToken.None);

        Assert.Contains("maní", reply!.Text);
        // "avena" was parsed but dedupes as a gluten synonym (ADD-9), so it is
        // echoed as already on the list instead of being stored a second time.
        Assert.Contains("avena", reply.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Equal(new[] { "peanut", "gluten", "lactose" }, persisted.Allergies);
        Assert.Equal(new[] { "maní", "trigo", "lácteos" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_BulletsAndNumbering_SplitsIntoThreeItems()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(
            ChatId, session, "/add - maní\n• trigo\n1. leche", null, CancellationToken.None);

        Assert.NotNull(reply);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Equal(new[] { "peanut", "gluten", "lactose" }, persisted.Allergies);
        Assert.Equal(new[] { "maní", "trigo", "leche" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task Add_NoSeparators_TreatedAsSingleItem()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Single(persisted.Allergies!);
        Assert.Single(persisted.AllergyDisplay!);
    }

    [Fact]
    public async Task Add_Uppercased_IsNotMatched_NothingPersisted()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/Add maní", null, CancellationToken.None);

        Assert.Equal(BotCopy.UnknownCommand, reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Null(persisted.Allergies);
    }

    [Fact]
    public async Task Add_Owner_ReplyEchoesAllAddedAllergens()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add maní, trigo", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Contains("maní", reply!.Text);
        Assert.Contains("trigo", reply.Text);
    }

    [Fact]
    public async Task Add_SpanishInput_RepliesInSpanish()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.Equal("Alergias agregadas: maní", reply!.Text);
    }

    [Fact]
    public async Task Add_EnglishInput_RepliesInEnglish()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add peanut", null, CancellationToken.None);

        Assert.Equal("Added allergies: peanut", reply!.Text);
    }

    [Fact]
    public async Task Add_IdenticalInputTwice_ChoosesSameLanguageBothTimes()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var first = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);
        var second = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.StartsWith("Alergias agregadas:", first!.Text);
        Assert.StartsWith("Ya estaban en tu lista:", second!.Text);
    }

    [Fact]
    public async Task Add_BareCommand_RepliesUsage_NothingPersisted()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyUsageEn, reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Null(persisted.Allergies);
    }

    [Fact]
    public async Task Add_WhitespaceOnlyArgument_RepliesUsage_NothingPersisted()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add   ", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyUsageEn, reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Null(persisted.Allergies);
    }

    [Fact]
    public async Task Add_LoggedOut_RepliesLoginPrompt_NothingPersisted()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = LoggedOutSession();

        var reply = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyLoginPromptEs, reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Null(persisted.Allergies);
    }

    [Fact]
    public async Task Add_Guest_RepliesOwnerOnly_NothingPersisted()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        var session = GuestSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.Equal(BotCopy.AllergyOwnerOnlyEs, reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Null(persisted.Allergies);
    }

    [Fact]
    public async Task Add_RepeatedExact_StoredOnce_ReplyNotesDuplicate()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        owner.Allergies = new List<string> { "peanut" };
        owner.AllergyDisplay = new List<string> { "maní" };
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add maní", null, CancellationToken.None);

        Assert.Equal("Ya estaban en tu lista: maní", reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Single(persisted.Allergies!);
        Assert.Single(persisted.AllergyDisplay!);
    }

    [Fact]
    public async Task Add_SynonymDuplicate_StoredOnce_ReplyNotesDuplicate()
    {
        var (handler, fake) = Create();
        var owner = SeedOwner(fake);
        owner.Allergies = new List<string> { "peanut" };
        owner.AllergyDisplay = new List<string> { "maní" };
        var session = OwnerSession(owner);

        var reply = await handler.HandleAsync(ChatId, session, "/add cacahuete", null, CancellationToken.None);

        Assert.Equal("Already on your list: cacahuete", reply!.Text);
        var persisted = await OwnerWithStoredAsync(fake, owner);
        Assert.Equal(new[] { "peanut" }, persisted.Allergies);
        Assert.Equal(new[] { "maní" }, persisted.AllergyDisplay);
    }
}
