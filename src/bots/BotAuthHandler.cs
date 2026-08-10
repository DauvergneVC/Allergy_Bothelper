using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Allergy_BotHelper.Tests")]

public interface IBotAuthHandler
{
    Task<BotReply?> HandleAsync(long chatId, string? text, string? callbackData, CancellationToken ct);
}

public sealed class BotAuthHandler : IBotAuthHandler
{
    private static readonly EmailAddressAttribute EmailAttribute = new();

    private readonly IAuthService _authService;
    private readonly IShareService _shareService;
    private readonly ConcurrentDictionary<long, ChatSession> _sessions = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _gates = new();

    public BotAuthHandler(IAuthService authService, IShareService shareService)
    {
        _authService = authService;
        _shareService = shareService;
    }

    public async Task<BotReply?> HandleAsync(long chatId, string? text, string? callbackData, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(chatId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var session = _sessions.GetOrAdd(chatId, _ => new ChatSession());
            return await HandleCoreAsync(session, text, callbackData).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    internal ChatSession? GetSession(long chatId)
        => _sessions.TryGetValue(chatId, out var session) ? session : null;

    private async Task<BotReply?> HandleCoreAsync(ChatSession session, string? text, string? callbackData)
    {
        if (callbackData is not null)
        {
            return HandleCallback(session, callbackData);
        }

        if (text is null)
        {
            return null;
        }

        if (session.State == SessionState.Idle)
        {
            return text.StartsWith('/')
                ? await HandleIdleCommandAsync(session, text).ConfigureAwait(false)
                : null;
        }

        return await HandlePendingInputAsync(session, text).ConfigureAwait(false);
    }

    private static BotReply? HandleCallback(ChatSession session, string callbackData)
    {
        if (session.State != SessionState.Idle)
        {
            return null;
        }

        switch (callbackData)
        {
            case BotCopy.CallbackLogin:
                StartLoginFlow(session);
                return new BotReply(BotCopy.PromptLoginEmail);
            case BotCopy.CallbackRegister:
                StartRegisterFlow(session);
                return new BotReply(BotCopy.PromptRegisterEmail);
            default:
                return null;
        }
    }

    private async Task<BotReply> HandleIdleCommandAsync(ChatSession session, string command)
    {
        switch (command)
        {
            case "/start":
                return MenuReply();
            case "/login":
                StartLoginFlow(session);
                return new BotReply(BotCopy.PromptLoginEmail);
            case "/register":
                StartRegisterFlow(session);
                return new BotReply(BotCopy.PromptRegisterEmail);
            case "/share":
                return await HandleShareAsync(session).ConfigureAwait(false);
            case "/revoke":
                return await HandleRevokeAsync(session).ConfigureAwait(false);
            case "/logout":
                return HandleLogout(session);
            case "/cancel":
                return new BotReply(BotCopy.NothingToCancel);
            case "/help":
                return HelpReply(session);
            default:
                return new BotReply(BotCopy.UnknownCommand);
        }
    }

    private async Task<BotReply> HandlePendingInputAsync(ChatSession session, string input)
    {
        if (input.StartsWith('/'))
        {
            return HandlePendingCommand(session, input);
        }

        switch (session.State)
        {
            case SessionState.AwaitingRegisterEmail:
                return HandleRegisterEmail(session, input);
            case SessionState.AwaitingRegisterPassword:
                return await HandleRegisterPasswordAsync(session, input).ConfigureAwait(false);
            case SessionState.AwaitingLoginEmail:
                return await HandleLoginEmailAsync(session, input).ConfigureAwait(false);
            case SessionState.AwaitingLoginPassword:
                return await HandleLoginPasswordAsync(session, input).ConfigureAwait(false);
            default:
                return new BotReply(BotCopy.UnknownCommand);
        }
    }

    private static BotReply HandlePendingCommand(ChatSession session, string command)
    {
        switch (command)
        {
            case "/login":
                StartLoginFlow(session);
                return new BotReply(BotCopy.PromptLoginEmail);
            case "/register":
                StartRegisterFlow(session);
                return new BotReply(BotCopy.PromptRegisterEmail);
            case "/start":
                session.State = SessionState.Idle;
                session.PendingEmail = null;
                return MenuReply();
            case "/cancel":
                session.State = SessionState.Idle;
                session.PendingEmail = null;
                return new BotReply(BotCopy.Cancelled);
            case "/share":
            case "/revoke":
            case "/logout":
                return new BotReply(BotCopy.StepInProgress);
            case "/help":
                return HelpReply(session);
            default:
                return new BotReply(BotCopy.UnknownCommand);
        }
    }

    private static BotReply HelpReply(ChatSession session)
        => new(session.Role == ChatRole.None ? BotCopy.HelpCommandsLoggedOut : BotCopy.HelpCommands);

    private static BotReply HandleRegisterEmail(ChatSession session, string input)
    {
        if (IsEmail(input))
        {
            session.PendingEmail = input;
            session.State = SessionState.AwaitingRegisterPassword;
            return new BotReply(BotCopy.PromptPassword);
        }

        return new BotReply(BotCopy.InvalidInput);
    }

    private async Task<BotReply> HandleRegisterPasswordAsync(ChatSession session, string password)
    {
        try
        {
            var user = await _authService.RegisterAsync(session.PendingEmail!, password).ConfigureAwait(false);
            session.State = SessionState.Idle;
            session.Role = ChatRole.Owner;
            session.UserId = user.Id;
            session.PendingEmail = null;
            return new BotReply(BotCopy.RegisterSuccess + "\n\n" + BotCopy.HelpCommands);
        }
        catch (AuthException ex)
        {
            switch (ex.Code)
            {
                case AuthErrorCode.DuplicateEmail:
                    session.State = SessionState.AwaitingRegisterEmail;
                    session.PendingEmail = null;
                    break;
                case AuthErrorCode.InvalidInput:
                    break;
                default:
                    session.State = SessionState.AwaitingRegisterEmail;
                    session.PendingEmail = null;
                    break;
            }

            return new BotReply(BotCopy.ForAuthError(ex.Code));
        }
    }

    private async Task<BotReply> HandleLoginEmailAsync(ChatSession session, string input)
    {
        if (IsEmail(input))
        {
            session.PendingEmail = input;
            session.State = SessionState.AwaitingLoginPassword;
            return new BotReply(BotCopy.PromptPassword);
        }

        try
        {
            var user = await _authService.LoginByTokenAsync(input).ConfigureAwait(false);
            session.State = SessionState.Idle;
            session.Role = ChatRole.Guest;
            session.UserId = user.Id;
            session.GuestToken = input;
            session.PendingEmail = null;
            return new BotReply(BotCopy.LoginGuestSuccess + "\n\n" + BotCopy.HelpCommands);
        }
        catch (AuthException ex)
        {
            return new BotReply(BotCopy.ForAuthError(ex.Code));
        }
    }

    private async Task<BotReply> HandleLoginPasswordAsync(ChatSession session, string password)
    {
        try
        {
            var user = await _authService.LoginAsync(session.PendingEmail!, password).ConfigureAwait(false);
            session.State = SessionState.Idle;
            session.Role = ChatRole.Owner;
            session.UserId = user.Id;
            session.GuestToken = null;
            session.PendingEmail = null;
            return new BotReply(BotCopy.LoginOwnerSuccess + "\n\n" + BotCopy.HelpCommands);
        }
        catch (AuthException ex)
        {
            switch (ex.Code)
            {
                case AuthErrorCode.UnknownEmail:
                case AuthErrorCode.WrongPassword:
                    session.State = SessionState.AwaitingLoginEmail;
                    session.PendingEmail = null;
                    break;
                case AuthErrorCode.InvalidInput:
                    break;
                default:
                    session.State = SessionState.AwaitingLoginEmail;
                    session.PendingEmail = null;
                    break;
            }

            return new BotReply(BotCopy.ForAuthError(ex.Code));
        }
    }

    private async Task<BotReply> HandleShareAsync(ChatSession session)
    {
        if (session.Role == ChatRole.None)
        {
            return new BotReply(BotCopy.ShareNotLoggedIn);
        }

        if (session.Role == ChatRole.Guest)
        {
            return new BotReply(BotCopy.ShareGuestDenied);
        }

        if (session.UserId is not { } userId)
        {
            return new BotReply(BotCopy.ShareNotLoggedIn);
        }

        var token = await _shareService.GenerateTokenAsync(userId).ConfigureAwait(false);
        return new BotReply(string.Format(BotCopy.ShareWarningTemplate, token));
    }

    private async Task<BotReply> HandleRevokeAsync(ChatSession session)
    {
        if (session.Role == ChatRole.None)
        {
            return new BotReply(BotCopy.ShareNotLoggedIn);
        }

        if (session.Role == ChatRole.Guest)
        {
            return new BotReply(BotCopy.ShareGuestDenied);
        }

        if (session.UserId is not { } userId)
        {
            return new BotReply(BotCopy.ShareNotLoggedIn);
        }

        await _shareService.RevokeTokenAsync(userId).ConfigureAwait(false);
        return new BotReply(BotCopy.RevokeSuccess);
    }

    private static BotReply HandleLogout(ChatSession session)
    {
        if (session.Role == ChatRole.None)
        {
            return new BotReply(BotCopy.LogoutIdle);
        }

        session.State = SessionState.Idle;
        session.Role = ChatRole.None;
        session.UserId = null;
        session.GuestToken = null;
        session.PendingEmail = null;
        return new BotReply(BotCopy.LogoutSuccess);
    }

    private static BotReply MenuReply() => new(
        BotCopy.MenuTitle,
        new[] { new BotButton(BotCopy.ButtonLogin, BotCopy.CallbackLogin), new BotButton(BotCopy.ButtonRegister, BotCopy.CallbackRegister) });

    private static void StartLoginFlow(ChatSession session)
    {
        session.State = SessionState.AwaitingLoginEmail;
        session.PendingEmail = null;
    }

    private static void StartRegisterFlow(ChatSession session)
    {
        session.State = SessionState.AwaitingRegisterEmail;
        session.PendingEmail = null;
    }

    private static bool IsEmail(string input) => EmailAttribute.IsValid(input.Trim());
}
