using System.Text.RegularExpressions;
using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotAuthHandlerTests
{
    private const long ChatA = 1001;
    private const long ChatB = 1002;

    private static (BotAuthHandler Handler, FakeUserRepository Fake, AuthService Auth, ShareService Share) Create()
    {
        var fake = new FakeUserRepository();
        var auth = new AuthService(fake);
        var share = new ShareService(fake);
        var handler = new BotAuthHandler(auth, share);
        return (handler, fake, auth, share);
    }

    private static async Task CompleteOwnerLoginAsync(BotAuthHandler handler, long chatId)
    {
        await handler.HandleAsync(chatId, "/login", null, CancellationToken.None);
        await handler.HandleAsync(chatId, "owner@example.com", null, CancellationToken.None);
        await handler.HandleAsync(chatId, "password123", null, CancellationToken.None);
    }

    [Fact]
    public async Task FreshHandler_HasNoSessions()
    {
        var (handler, _, _, _) = Create();

        Assert.Null(handler.GetSession(ChatA));
        Assert.Null(handler.GetSession(ChatB));
    }

    [Fact]
    public async Task Start_ShowsMenuWithLoginAndRegisterButtons_StaysIdle()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, "/start", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.MenuTitle, reply!.Text);
        Assert.NotNull(reply.Buttons);
        Assert.Collection(
            reply.Buttons!,
            b => { Assert.Equal(BotCopy.ButtonLogin, b.Text); Assert.Equal("login", b.CallbackValue); },
            b => { Assert.Equal(BotCopy.ButtonRegister, b.Text); Assert.Equal("register", b.CallbackValue); });

        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(ChatRole.None, session.Role);
    }

    [Theory]
    [InlineData("login", SessionState.AwaitingLoginEmail, "Enter your email to log in, or a share token to continue as guest.")]
    [InlineData("register", SessionState.AwaitingRegisterEmail, "Enter your email to create an account.")]
    public async Task IdleCallbacks_StartCorrespondingFlows(string callback, SessionState expectedState, string expectedText)
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, null, callback, CancellationToken.None);

        Assert.Equal(expectedText, reply!.Text);
        Assert.Equal(expectedState, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task UnknownCallback_ReturnsNull()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, null, "bogus", CancellationToken.None);

        Assert.Null(reply);
    }

    [Fact]
    public async Task Callback_WhilePending_IsIgnored()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatA, null, "register", CancellationToken.None);

        Assert.Null(reply);
        Assert.Equal(SessionState.AwaitingLoginEmail, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task UnknownCommand_RepliesUnknownCommand_StaysIdle()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, "/bogus", null, CancellationToken.None);

        Assert.Equal(BotCopy.UnknownCommand, reply!.Text);
        Assert.Equal(SessionState.Idle, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task Register_TwoSteps_Succeeds_AndPreservesPasswordSpacing()
    {
        var (handler, fake, _, _) = Create();

        var first = await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);
        Assert.Equal(BotCopy.PromptRegisterEmail, first!.Text);
        Assert.Equal(SessionState.AwaitingRegisterEmail, handler.GetSession(ChatA)!.State);

        var second = await handler.HandleAsync(ChatA, "owner@example.com", null, CancellationToken.None);
        Assert.Equal(BotCopy.PromptPassword, second!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingRegisterPassword, session.State);
        Assert.Equal("owner@example.com", session.PendingEmail);

        var third = await handler.HandleAsync(ChatA, "  spaced  ", null, CancellationToken.None);
        Assert.Equal(BotCopy.RegisterSuccess, third!.Text);

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(ChatRole.Owner, session.Role);
        Assert.NotNull(session.UserId);
        Assert.Null(session.PendingEmail);

        var stored = await fake.GetUserByEmailAsync("owner@example.com");
        Assert.NotNull(stored);
        Assert.True(BCrypt.Net.BCrypt.Verify("  spaced  ", stored!.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("spaced", stored.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsToAwaitingRegisterEmail_AndClearsPendingEmail()
    {
        var (handler, _, auth, _) = Create();
        await auth.RegisterAsync("dup@example.com", "password123");

        await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "dup@example.com", null, CancellationToken.None);
        var reply = await handler.HandleAsync(ChatA, "password123", null, CancellationToken.None);

        Assert.Equal(BotCopy.DuplicateEmail, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingRegisterEmail, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Register_InvalidEmail_RepliesInvalidInput_WithoutServiceCall(string email)
    {
        var (handler, fake, _, _) = Create();
        await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatA, email, null, CancellationToken.None);

        Assert.Equal(BotCopy.InvalidInput, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingRegisterEmail, session.State);
        Assert.Null(session.PendingEmail);
        Assert.Equal(0, fake.GetUserByEmailAsyncCalls);
        Assert.Equal(0, fake.GetByUserShareTokenAsyncCalls);
    }

    [Fact]
    public async Task Login_OwnerPasswordFlow_Succeeds()
    {
        var (handler, fake, _, _) = Create();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash));

        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "owner@example.com", null, CancellationToken.None);
        var reply = await handler.HandleAsync(ChatA, "password123", null, CancellationToken.None);

        Assert.Equal(BotCopy.LoginOwnerSuccess, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(ChatRole.Owner, session.Role);
        Assert.NotNull(session.UserId);
        Assert.Null(session.GuestToken);
        Assert.Null(session.PendingEmail);
    }

    [Theory]
    [InlineData("ghost@example.com", "password123", "No account found with that email.")]
    [InlineData("owner@example.com", "wrong-password", "Incorrect password.")]
    public async Task Login_EmailStepErrors_ReturnToAwaitingLoginEmail_WithPinnedCopy(
        string email, string password, string expected)
    {
        var (handler, fake, _, _) = Create();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash));

        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, email, null, CancellationToken.None);
        var reply = await handler.HandleAsync(ChatA, password, null, CancellationToken.None);

        Assert.Equal(expected, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingLoginEmail, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Login_TokenPath_EstablishesGuestSession_WithTokenStored()
    {
        var (handler, fake, _, _) = Create();
        var owner = new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "SHARED-TOKEN-1" };
        fake.Seed(owner);

        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        var reply = await handler.HandleAsync(ChatA, "SHARED-TOKEN-1", null, CancellationToken.None);

        Assert.Equal(BotCopy.LoginGuestSuccess, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(ChatRole.Guest, session.Role);
        Assert.Equal(owner.Id, session.UserId);
        Assert.Equal("SHARED-TOKEN-1", session.GuestToken);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Login_TokenPath_InvalidToken_StaysAwaitingLoginEmail()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatA, "NO-SUCH-TOKEN", null, CancellationToken.None);

        Assert.Equal(BotCopy.InvalidToken, reply!.Text);
        Assert.Equal(SessionState.AwaitingLoginEmail, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task Share_NotLoggedIn_ShowsPrompt()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, "/share", null, CancellationToken.None);

        Assert.Equal(BotCopy.ShareNotLoggedIn, reply!.Text);
    }

    [Fact]
    public async Task Share_Guest_IsDenied()
    {
        var (handler, fake, _, _) = Create();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "GUEST-TOKEN" });
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "GUEST-TOKEN", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatA, "/share", null, CancellationToken.None);

        Assert.Equal(BotCopy.ShareGuestDenied, reply!.Text);
    }

    [Fact]
    public async Task Share_Owner_RepliesTokenInsideWarning()
    {
        var (handler, _, auth, _) = Create();
        await auth.RegisterAsync("owner@example.com", "password123");
        await CompleteOwnerLoginAsync(handler, ChatA);

        var reply = await handler.HandleAsync(ChatA, "/share", null, CancellationToken.None);

        const string prefix = "Anyone with this token can view your allergies: ";
        const string suffix = " The previous token no longer works.";
        Assert.StartsWith(prefix, reply!.Text);
        Assert.EndsWith(suffix, reply.Text);

        var token = reply.Text[prefix.Length..^suffix.Length];
        Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), token);
        Assert.Equal(SessionState.Idle, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task Revoke_Owner_Succeeds_AndClearsStoredToken()
    {
        var (handler, fake, auth, _) = Create();
        await auth.RegisterAsync("owner@example.com", "password123");
        await fake.GenerateTokenAsync((await fake.GetUserByEmailAsync("owner@example.com"))!.Id);
        await CompleteOwnerLoginAsync(handler, ChatA);

        var reply = await handler.HandleAsync(ChatA, "/revoke", null, CancellationToken.None);

        Assert.Equal(BotCopy.RevokeSuccess, reply!.Text);
        var stored = await fake.GetUserByEmailAsync("owner@example.com");
        Assert.Null(stored!.ShareToken);
    }

    [Fact]
    public async Task Revoke_Guest_IsDenied()
    {
        var (handler, fake, _, _) = Create();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "GUEST-TOKEN" });
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "GUEST-TOKEN", null, CancellationToken.None);

        var reply = await handler.HandleAsync(ChatA, "/revoke", null, CancellationToken.None);

        Assert.Equal(BotCopy.ShareGuestDenied, reply!.Text);
    }

    [Fact]
    public async Task Logout_LoggedIn_ClearsSession()
    {
        var (handler, _, auth, _) = Create();
        await auth.RegisterAsync("owner@example.com", "password123");
        await CompleteOwnerLoginAsync(handler, ChatA);

        var reply = await handler.HandleAsync(ChatA, "/logout", null, CancellationToken.None);

        Assert.Equal(BotCopy.LogoutSuccess, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(ChatRole.None, session.Role);
        Assert.Null(session.UserId);
        Assert.Null(session.GuestToken);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Logout_NotLoggedIn_ShowsIdleCopy()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, "/logout", null, CancellationToken.None);

        Assert.Equal(BotCopy.LogoutIdle, reply!.Text);
    }

    [Fact]
    public async Task Cancel_Pending_AbortsAndClearsPendingEmail()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "owner@example.com", null, CancellationToken.None);
        Assert.Equal("owner@example.com", handler.GetSession(ChatA)!.PendingEmail);

        var reply = await handler.HandleAsync(ChatA, "/cancel", null, CancellationToken.None);

        Assert.Equal(BotCopy.Cancelled, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Cancel_Idle_ShowsNothingToCancel()
    {
        var (handler, _, _, _) = Create();

        var reply = await handler.HandleAsync(ChatA, "/cancel", null, CancellationToken.None);

        Assert.Equal(BotCopy.NothingToCancel, reply!.Text);
    }

    [Fact]
    public async Task Pending_LoginCommand_AbortsCurrentFlow()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "someone@example.com", null, CancellationToken.None);
        Assert.Equal(SessionState.AwaitingRegisterPassword, handler.GetSession(ChatA)!.State);

        var reply = await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);

        Assert.Equal(BotCopy.PromptLoginEmail, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingLoginEmail, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Pending_RegisterCommand_AbortsCurrentFlow()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "someone@example.com", null, CancellationToken.None);
        Assert.Equal(SessionState.AwaitingLoginPassword, handler.GetSession(ChatA)!.State);

        var reply = await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);

        Assert.Equal(BotCopy.PromptRegisterEmail, reply!.Text);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.AwaitingRegisterEmail, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Fact]
    public async Task Start_WhilePending_AbortsFlowAndShowsMenu()
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);
        await handler.HandleAsync(ChatA, "someone@example.com", null, CancellationToken.None);
        Assert.Equal(SessionState.AwaitingRegisterPassword, handler.GetSession(ChatA)!.State);

        var reply = await handler.HandleAsync(ChatA, "/start", null, CancellationToken.None);

        Assert.Equal(BotCopy.MenuTitle, reply!.Text);
        Assert.NotNull(reply.Buttons);
        var session = handler.GetSession(ChatA)!;
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Null(session.PendingEmail);
    }

    [Theory]
    [InlineData("/share")]
    [InlineData("/revoke")]
    [InlineData("/logout")]
    public async Task Pending_ManagementCommands_ReplyStepInProgress_AndStayPending(string command)
    {
        var (handler, _, _, _) = Create();
        await handler.HandleAsync(ChatA, "/login", null, CancellationToken.None);
        Assert.Equal(SessionState.AwaitingLoginEmail, handler.GetSession(ChatA)!.State);

        var reply = await handler.HandleAsync(ChatA, command, null, CancellationToken.None);

        Assert.Equal(BotCopy.StepInProgress, reply!.Text);
        Assert.Equal(SessionState.AwaitingLoginEmail, handler.GetSession(ChatA)!.State);
    }

    [Fact]
    public async Task TwoChats_ConcurrentRegisterAndGuestLogin_StayIndependent()
    {
        var (handler, fake, _, _) = Create();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "GUEST-TOKEN" });

        var registerTask = Task.Run(async () =>
        {
            await handler.HandleAsync(ChatA, "/register", null, CancellationToken.None);
            await handler.HandleAsync(ChatA, "newuser@example.com", null, CancellationToken.None);
            await handler.HandleAsync(ChatA, "password123", null, CancellationToken.None);
        });

        var guestLoginTask = Task.Run(async () =>
        {
            await handler.HandleAsync(ChatB, "/login", null, CancellationToken.None);
            await handler.HandleAsync(ChatB, "GUEST-TOKEN", null, CancellationToken.None);
        });

        await Task.WhenAll(registerTask, guestLoginTask);

        var sessionA = handler.GetSession(ChatA)!;
        var sessionB = handler.GetSession(ChatB)!;
        Assert.Equal(SessionState.Idle, sessionA.State);
        Assert.Equal(ChatRole.Owner, sessionA.Role);
        Assert.Equal(SessionState.Idle, sessionB.State);
        Assert.Equal(ChatRole.Guest, sessionB.Role);
        Assert.Equal("GUEST-TOKEN", sessionB.GuestToken);
    }

    [Fact]
    public async Task TwoChats_ConcurrentOwnerLogins_LogoutInA_LeavesBLoggedIn()
    {
        var (handler, fake, auth, _) = Create();
        await auth.RegisterAsync("owner@example.com", "password123");

        var loginA = Task.Run(() => CompleteOwnerLoginAsync(handler, ChatA));
        var loginB = Task.Run(() => CompleteOwnerLoginAsync(handler, ChatB));
        await Task.WhenAll(loginA, loginB);

        Assert.Equal(ChatRole.Owner, handler.GetSession(ChatA)!.Role);
        Assert.Equal(ChatRole.Owner, handler.GetSession(ChatB)!.Role);

        var reply = await handler.HandleAsync(ChatA, "/logout", null, CancellationToken.None);

        Assert.Equal(BotCopy.LogoutSuccess, reply!.Text);
        Assert.Equal(ChatRole.None, handler.GetSession(ChatA)!.Role);
        Assert.Equal(ChatRole.Owner, handler.GetSession(ChatB)!.Role);
        Assert.Equal(SessionState.Idle, handler.GetSession(ChatB)!.State);
    }
}
