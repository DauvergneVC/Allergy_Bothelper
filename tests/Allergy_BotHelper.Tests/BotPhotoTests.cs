using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotPhotoTests
{
    private const long ChatId = 7001;

    private static (BotAuthHandler Handler, FakeUserRepository Fake, FakeOcrService Ocr) Create()
    {
        var fake = new FakeUserRepository();
        var ocr = new FakeOcrService();
        var handler = new BotAuthHandler(
            new AuthService(fake),
            new ShareService(fake),
            new AllergyService(fake),
            ocr);
        return (handler, fake, ocr);
    }

    private static User SeedOwner(FakeUserRepository fake)
    {
        var user = new User("owner@example.com", BcryptFixtures.Password123Hash);
        fake.Seed(user);
        return user;
    }

    private static User SeedOwnerWith(FakeUserRepository fake, params string[] canonicalKeys)
    {
        var user = SeedOwner(fake);
        if (canonicalKeys.Length > 0)
        {
            user.Allergies = canonicalKeys.ToList();
        }
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

    private static async Task<User?> StoredAsync(FakeUserRepository fake, User owner)
        => await fake.GetUserByIdAsync(owner.Id);

    // ---- Photo /add (ADD-3, ADD-8) ----

    [Fact]
    public async Task PhotoAdd_Owner_OcrTextParsed_PersistsCanonicalAndEchoes()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);
        ocr.Text = "maní, leche";

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "/add", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal("Added allergies: maní, leche", reply!.Text);
        var persisted = await StoredAsync(fake, owner);
        Assert.Equal(new[] { "peanut", "lactose" }, persisted!.Allergies);
        Assert.Equal(new[] { "maní", "leche" }, persisted.AllergyDisplay);
    }

    [Fact]
    public async Task PhotoAdd_OcrFailure_FriendlyReply_NothingPersisted()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);
        ocr.ThrowOcrFailure = true;

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "/add", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.OcrFailureEn, reply!.Text);
        Assert.Equal(1, ocr.Calls);
        Assert.Null((await StoredAsync(fake, owner))!.Allergies);
    }

    [Fact]
    public async Task PhotoAdd_OversizeEmptyBytes_FriendlyReply_NoOcrCall()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "/add", null, CancellationToken.None, Array.Empty<byte>());

        Assert.Equal(BotCopy.OcrFailureEn, reply!.Text);
        Assert.Equal(0, ocr.Calls);
        Assert.Null((await StoredAsync(fake, owner))!.Allergies);
    }

    [Fact]
    public async Task PhotoAdd_EmptyOcrText_UsageReply_NothingPersisted()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);
        ocr.Text = "   ";

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "/add", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.AllergyUsageEn, reply!.Text);
        Assert.Null((await StoredAsync(fake, owner))!.Allergies);
    }

    [Fact]
    public async Task PhotoAdd_LoggedOut_LoginPrompt_NoOcrCall()
    {
        var (handler, _, ocr) = Create();

        var reply = await handler.HandleAsync(
            ChatId, LoggedOutSession(), "/add", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.AllergyLoginPromptEn, reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task PhotoAdd_Guest_OwnerOnly_NoOcrCall()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(
            ChatId, GuestSession(owner), "/add", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.AllergyOwnerOnlyEn, reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task Photo_CommandCaption_NonAdd_CommandPathNeverOcr()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "/help", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.HelpCommands, reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }

    // ---- Photo consult (CONSULT-2, CONSULT-4, OCR-5, OCR-6) ----

    [Fact]
    public async Task PhotoConsult_NoCaption_OcrTextConsulted_VerdictInEnglish()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        ocr.Text = "maní";

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), null, null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal("Allergen detected: peanut (maní)", reply!.Text);
    }

    [Fact]
    public async Task PhotoConsult_NonCommandCaption_PrefixStripped_OcrAppended()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut", "lactose");
        ocr.Text = "leche";

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "mira esto", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal("Alérgeno detectado: lactose (leche)", reply!.Text);
    }

    [Fact]
    public async Task PhotoConsult_FreeTextCaption_AppendsOcrText()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        ocr.Text = "maní";

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "mira esta salsa", null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal("Alérgeno detectado: peanut (maní)", reply!.Text);
    }

    [Fact]
    public async Task PhotoConsult_LoggedOut_LoginPrompt_NoOcrCall()
    {
        var (handler, _, ocr) = Create();

        var reply = await handler.HandleAsync(
            ChatId, LoggedOutSession(), null, null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.AllergyLoginPromptEn, reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task PhotoConsult_OcrFailure_FriendlyReply_NoCrash()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        ocr.ThrowOcrFailure = true;

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), null, null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.OcrFailureEn, reply!.Text);
        Assert.Equal(1, ocr.Calls);
    }

    [Fact]
    public async Task PhotoConsult_OversizeEmptyBytes_FriendlyReply_NoOcrCall()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), null, null, CancellationToken.None, Array.Empty<byte>());

        Assert.Equal(BotCopy.OcrFailureEn, reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task PhotoConsult_NoOcrText_SafeVerdict()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");
        ocr.Text = null;

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), null, null, CancellationToken.None, new byte[] { 1 });

        Assert.Equal(BotCopy.IngredientSafeEn, reply!.Text);
    }

    [Fact]
    public async Task PhotoDuringPendingFlow_IsIgnored_NoOcrCall()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwner(fake);
        var session = OwnerSession(owner);
        session.State = SessionState.AwaitingLoginEmail;

        var reply = await handler.HandleAsync(
            ChatId, session, null, null, CancellationToken.None, new byte[] { 1 });

        Assert.Null(reply);
        Assert.Equal(0, ocr.Calls);
    }

    // ---- Tri-state (null / empty / non-empty) ----

    [Fact]
    public async Task PhotoBytesNull_TextFlowUnchanged_NoOcrCall()
    {
        var (handler, fake, ocr) = Create();
        var owner = SeedOwnerWith(fake, "peanut");

        var reply = await handler.HandleAsync(
            ChatId, OwnerSession(owner), "maní", null, CancellationToken.None, null);

        Assert.Equal("Alérgeno detectado: peanut (maní)", reply!.Text);
        Assert.Equal(0, ocr.Calls);
    }
}
