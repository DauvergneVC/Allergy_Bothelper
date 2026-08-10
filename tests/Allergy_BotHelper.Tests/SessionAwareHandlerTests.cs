using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class SessionAwareHandlerTests
{
    private const long ChatA = 1001;
    private const long ChatB = 1002;

    private static (SessionAwareHandler Handler, FakeSessionStore Store) Create()
    {
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash));
        var inner = new BotAuthHandler(new AuthService(fake), new ShareService(fake));
        var store = new FakeSessionStore();
        return (new SessionAwareHandler(inner, store), store);
    }

    private static async Task CompleteOwnerLoginAsync(SessionAwareHandler handler, long chatId)
    {
        await handler.HandleAsync(chatId, new ChatSession(), "/login", null, CancellationToken.None);
        await handler.HandleAsync(chatId, new ChatSession(), "owner@example.com", null, CancellationToken.None);
        await handler.HandleAsync(chatId, new ChatSession(), "password123", null, CancellationToken.None);
    }

    [Fact]
    public async Task MutatingFlow_LoadsPersistsAndIncrementsVersion()
    {
        var (handler, store) = Create();

        var reply = await handler.HandleAsync(ChatA, new ChatSession(), "/start", null, CancellationToken.None);
        Assert.NotNull(reply);

        await handler.HandleAsync(ChatA, new ChatSession(), "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, new ChatSession(), "owner@example.com", null, CancellationToken.None);
        var final = await handler.HandleAsync(ChatA, new ChatSession(), "password123", null, CancellationToken.None);

        Assert.Equal(BotCopy.LoginOwnerSuccess + "\n\n" + BotCopy.HelpCommands, final!.Text);

        var stored = store.Lookup(ChatA)!;
        Assert.Equal(SessionState.Idle, stored.State);
        Assert.Equal(ChatRole.Owner, stored.Role);
        Assert.NotNull(stored.UserId);
        Assert.Equal(3, stored.Version);
        Assert.Equal(4, store.LoadCalls);
        Assert.Equal(3, store.SaveCalls);
    }

    [Fact]
    public async Task NonMutating_StartAndHelp_AreLoadedButNotSaved()
    {
        var (handler, store) = Create();

        var start = await handler.HandleAsync(ChatA, new ChatSession(), "/start", null, CancellationToken.None);
        var help = await handler.HandleAsync(ChatA, new ChatSession(), "/help", null, CancellationToken.None);

        Assert.Equal(BotCopy.MenuTitle, start!.Text);
        Assert.Equal(BotCopy.HelpCommandsLoggedOut, help!.Text);
        Assert.Equal(0, store.SaveCalls);
        Assert.Equal(2, store.LoadCalls);
        Assert.Null(store.Lookup(ChatA));
    }

    [Fact]
    public async Task Logout_ClearsRoleAndWrites()
    {
        var (handler, store) = Create();
        await CompleteOwnerLoginAsync(handler, ChatA);

        var reply = await handler.HandleAsync(ChatA, new ChatSession(), "/logout", null, CancellationToken.None);

        Assert.Equal(BotCopy.LogoutSuccess, reply!.Text);
        var stored = store.Lookup(ChatA)!;
        Assert.Equal(ChatRole.None, stored.Role);
        Assert.Null(stored.UserId);
        Assert.Null(stored.GuestToken);
        Assert.Null(stored.PendingEmail);
    }

    [Fact]
    public async Task Rehydrates_PersistedState_BetweenUpdates()
    {
        var (handler, store) = Create();
        await CompleteOwnerLoginAsync(handler, ChatA);
        Assert.Equal(ChatRole.Owner, store.Lookup(ChatA)!.Role);

        // Second handler instance over the same store sees the persisted owner session.
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash));
        var inner = new BotAuthHandler(new AuthService(fake), new ShareService(fake));
        var second = new SessionAwareHandler(inner, store);

        var reply = await second.HandleAsync(ChatA, new ChatSession(), "/help", null, CancellationToken.None);

        Assert.Equal(BotCopy.HelpCommands, reply!.Text);
    }

    [Fact]
    public async Task CasConflict_ReloadsAndReplays_UntilSaveSucceeds()
    {
        var (handler, store) = Create();
        store.ConflictOnce = true;
        var reply = await handler.HandleAsync(ChatA, new ChatSession(), "/start", null, CancellationToken.None);
        Assert.Equal(BotCopy.MenuTitle, reply!.Text);

        var reply2 = await handler.HandleAsync(ChatA, new ChatSession(), "/login", null, CancellationToken.None);
        Assert.Equal(BotCopy.PromptLoginEmail, reply2!.Text);

        Assert.Equal(3, store.LoadCalls);
        Assert.Equal(2, store.SaveCalls);
        Assert.Equal(SessionState.AwaitingLoginEmail, store.Lookup(ChatA)!.State);
    }

    private sealed class SequenceReplyHandler : IBotAuthHandler
    {
        private readonly string[] _replies;
        private int _calls;

        public SequenceReplyHandler(params string[] replies) => _replies = replies;

        public Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct)
        {
            // Always mutating so the wrapper always attempts a save.
            session.State = SessionState.AwaitingLoginEmail;
            var reply = _calls < _replies.Length ? _replies[_calls] : _replies[^1];
            _calls++;
            return Task.FromResult<BotReply?>(new BotReply(reply));
        }
    }

    [Fact]
    public async Task CasConflict_ReplayReturnsLastReply()
    {
        var store = new FakeSessionStore();
        store.ConflictOnce = true;
        var inner = new SequenceReplyHandler("first", "last");
        var handler = new SessionAwareHandler(inner, store);

        var reply = await handler.HandleAsync(ChatA, new ChatSession(), null, null, CancellationToken.None);

        Assert.Equal("last", reply!.Text);
        Assert.Equal(2, store.LoadCalls);
        Assert.Equal(2, store.SaveCalls);
    }

    [Fact]
    public async Task PersistentCasFailure_BoundedToThreeAttempts()
    {
        var store = new FakeSessionStore();
        store.FailAlways = true;
        var inner = new SequenceReplyHandler("first", "last");
        var handler = new SessionAwareHandler(inner, store);

        var reply = await handler.HandleAsync(ChatA, new ChatSession(), null, null, CancellationToken.None);

        Assert.Equal("last", reply!.Text);
        Assert.Equal(3, store.LoadCalls);
        Assert.Equal(3, store.SaveCalls);
    }

    [Fact]
    public async Task SameChat_ConcurrentUpdates_AreSerialized_WithoutLostUpdates()
    {
        var (handler, store) = Create();

        var first = Task.Run(() => handler.HandleAsync(ChatA, new ChatSession(), "/login", null, CancellationToken.None));
        var second = Task.Run(() => handler.HandleAsync(ChatA, new ChatSession(), "/register", null, CancellationToken.None));

        await Task.WhenAll(first, second);

        Assert.Equal(2, store.LoadCalls);
        Assert.Equal(2, store.SaveCalls);
    }

    [Fact]
    public async Task TwoChats_ConcurrentRegisterAndGuestLogin_StayIndependent()
    {
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "GUEST-TOKEN" });
        var inner = new BotAuthHandler(new AuthService(fake), new ShareService(fake));
        var store = new FakeSessionStore();
        var handler = new SessionAwareHandler(inner, store);

        var registerTask = Task.Run(async () =>
        {
            await handler.HandleAsync(ChatA, new ChatSession(), "/register", null, CancellationToken.None);
            await handler.HandleAsync(ChatA, new ChatSession(), "newuser@example.com", null, CancellationToken.None);
            await handler.HandleAsync(ChatA, new ChatSession(), "password123", null, CancellationToken.None);
        });

        var guestLoginTask = Task.Run(async () =>
        {
            await handler.HandleAsync(ChatB, new ChatSession(), "/login", null, CancellationToken.None);
            await handler.HandleAsync(ChatB, new ChatSession(), "GUEST-TOKEN", null, CancellationToken.None);
        });

        await Task.WhenAll(registerTask, guestLoginTask);

        var sessionA = store.Lookup(ChatA)!;
        var sessionB = store.Lookup(ChatB)!;
        Assert.Equal(SessionState.Idle, sessionA.State);
        Assert.Equal(ChatRole.Owner, sessionA.Role);
        Assert.Equal(SessionState.Idle, sessionB.State);
        Assert.Equal(ChatRole.Guest, sessionB.Role);
        Assert.Equal("GUEST-TOKEN", sessionB.GuestToken);
    }

    [Fact]
    public async Task TwoChats_ConcurrentOwnerLogins_LogoutInA_LeavesBLoggedIn()
    {
        var fake = new FakeUserRepository();
        var auth = new AuthService(fake);
        await auth.RegisterAsync("owner@example.com", "password123");
        var inner = new BotAuthHandler(auth, new ShareService(fake));
        var store = new FakeSessionStore();
        var handler = new SessionAwareHandler(inner, store);

        var loginA = Task.Run(() => CompleteOwnerLoginAsync(handler, ChatA));
        var loginB = Task.Run(() => CompleteOwnerLoginAsync(handler, ChatB));
        await Task.WhenAll(loginA, loginB);

        Assert.Equal(ChatRole.Owner, store.Lookup(ChatA)!.Role);
        Assert.Equal(ChatRole.Owner, store.Lookup(ChatB)!.Role);

        var reply = await handler.HandleAsync(ChatA, new ChatSession(), "/logout", null, CancellationToken.None);

        Assert.Equal(BotCopy.LogoutSuccess, reply!.Text);
        Assert.Equal(ChatRole.None, store.Lookup(ChatA)!.Role);
        Assert.Equal(ChatRole.Owner, store.Lookup(ChatB)!.Role);
        Assert.Equal(SessionState.Idle, store.Lookup(ChatB)!.State);
    }
}
