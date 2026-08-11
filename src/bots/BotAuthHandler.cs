using System.ComponentModel.DataAnnotations;

public interface IBotAuthHandler
{
    Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct);
}

/// <summary>
/// Pure chat-logic handler: mutates the <see cref="ChatSession"/> it is given and returns a
/// reply. It owns no session storage or locking — the caller (a
/// <c>SessionAwareHandler</c>) is responsible for loading, persisting and serializing.
/// </summary>
public sealed class BotAuthHandler : IBotAuthHandler
{
    private static readonly EmailAddressAttribute EmailAttribute = new();

    private readonly IAuthService _authService;
    private readonly IShareService _shareService;
    private readonly IAllergyService _allergyService;

    public BotAuthHandler(IAuthService authService, IShareService shareService, IAllergyService allergyService)
    {
        _authService = authService;
        _shareService = shareService;
        _allergyService = allergyService;
    }

    public async Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct)
    {
        if (callbackData is not null)
        {
            return HandleCallback(session, callbackData);
        }

        if (text is null)
        {
            return null;
        }

        return session.State == SessionState.Idle
            ? await HandleIdleCommandAsync(session, text).ConfigureAwait(false)
            : await HandlePendingInputAsync(session, text).ConfigureAwait(false);
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
        if (IsAddCommand(command))
        {
            return await HandleAddAsync(session, command).ConfigureAwait(false);
        }

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
                // CONSULT-1: non-command Idle text is an ingredient consultation, not
                // an unknown command. Anything still starting with '/' stays a command.
                return command.StartsWith('/')
                    ? new BotReply(BotCopy.UnknownCommand)
                    : await HandleConsultAsync(session, command).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// CONSULT-1..9: commandless ingredient consultation. Non-command Idle text is
    /// prefix-stripped, split, and matched against the consulted user's stored
    /// canonical allergies. Guest sessions carry the owner's UserId, so guests
    /// consult against the owner's allergies.
    /// </summary>
    private async Task<BotReply> HandleConsultAsync(ChatSession session, string text)
    {
        var language = ReplyLanguage.Detect(text);

        // CONSULT-2: logged out → log-in prompt. The guard runs before any
        // allergy-service (or, later, OCR) invocation.
        if (session.Role == ChatRole.None || session.UserId is not { } userId)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyLoginPromptEn, BotCopy.AllergyLoginPromptEs));
        }

        var consultedText = IngredientParser.StripPrefix(text);
        var tokens = IngredientParser.SplitItems(consultedText);
        var ownerKeys = await _allergyService.GetAllergiesAsync(userId).ConfigureAwait(false);

        // No stored allergies: prompt to add allergens first instead of a misleading
        // "no allergen detected" (CONSULT-7 applies when allergies exist but none matched).
        if (ownerKeys.Count == 0)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyUsageEn, BotCopy.AllergyUsageEs));
        }

        var result = IngredientMatcher.Match(tokens, ownerKeys);
        return result.Matches.Count == 0
            ? new BotReply(BotCopy.ForLanguage(language, BotCopy.IngredientSafeEn, BotCopy.IngredientSafeEs))
            : BuildConsultVerdict(language, result);
    }

    private static BotReply BuildConsultVerdict(ReplyLanguageValue language, MatchResult result)
    {
        var lines = result.Matches.Select(match => string.Format(
            BotCopy.ForLanguage(language, BotCopy.IngredientMatchEn, BotCopy.IngredientMatchEs),
            $"{match.CanonicalKey} ({string.Join(", ", match.OffendingTokens)})"));
        return new BotReply(string.Join("\n", lines));
    }

    /// <summary>
    /// ADD-1: the exact lowercase /add command (case-sensitive). Matches "/add" and
    /// "/add ..." only — "/Add", "/ADD" and "/address" are not the command.
    /// </summary>
    private static bool IsAddCommand(string command)
        => command.StartsWith("/add", StringComparison.Ordinal)
            && (command.Length == 4 || char.IsWhiteSpace(command[4]));

    private async Task<BotReply> HandleAddAsync(ChatSession session, string command)
    {
        var language = ReplyLanguage.Detect(command);

        var argument = command.Length == 4 ? string.Empty : command[4..].Trim();
        if (argument.Length == 0)
        {
            // ADD-7: bare /add (no content, no photo) → usage, nothing persisted.
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyUsageEn, BotCopy.AllergyUsageEs));
        }

        // ADD-8: three-way role gating. Nothing persists for None or Guest.
        if (session.Role == ChatRole.None)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyLoginPromptEn, BotCopy.AllergyLoginPromptEs));
        }

        if (session.Role == ChatRole.Guest)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyOwnerOnlyEn, BotCopy.AllergyOwnerOnlyEs));
        }

        if (session.UserId is not { } userId)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyLoginPromptEn, BotCopy.AllergyLoginPromptEs));
        }

        var items = IngredientParser.SplitItems(argument);
        if (items.Count == 0)
        {
            return new BotReply(BotCopy.ForLanguage(language, BotCopy.AllergyUsageEn, BotCopy.AllergyUsageEs));
        }

        var added = new List<string>();
        var duplicates = new List<string>();
        foreach (var item in items)
        {
            var canonical = Vocabulary.Canonicalize(item);
            var stored = await _allergyService.AddAsync(userId, canonical, item).ConfigureAwait(false);
            (stored ? added : duplicates).Add(item);
        }

        return BuildAddEcho(language, added, duplicates);
    }

    private static BotReply BuildAddEcho(ReplyLanguageValue language, IReadOnlyList<string> added, IReadOnlyList<string> duplicates)
    {
        var lines = new List<string>();
        if (added.Count > 0)
        {
            lines.Add(string.Format(
                BotCopy.ForLanguage(language, BotCopy.AllergyAddedEn, BotCopy.AllergyAddedEs),
                string.Join(", ", added)));
        }
        if (duplicates.Count > 0)
        {
            lines.Add(string.Format(
                BotCopy.ForLanguage(language, BotCopy.AllergyAlreadyStoredEn, BotCopy.AllergyAlreadyStoredEs),
                string.Join(", ", duplicates)));
        }
        return new BotReply(string.Join("\n", lines));
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
