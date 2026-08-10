public static class BotCopy
{
    public const string MenuTitle = "Welcome! Choose an option:";
    public const string ButtonLogin = "Login";
    public const string ButtonRegister = "Register";
    public const string CallbackLogin = "login";
    public const string CallbackRegister = "register";
    public const string PromptLoginEmail = "Enter your email to log in, or a share token to continue as guest.";
    public const string PromptRegisterEmail = "Enter your email to create an account.";
    public const string PromptPassword = "Enter a password.";
    public const string RegisterSuccess = "Account created. You are logged in as the owner.";
    public const string LoginOwnerSuccess = "Logged in as the owner.";
    public const string LoginGuestSuccess = "Logged in as guest. You can view the owner's allergies.";
    public const string LogoutSuccess = "Logged out. This chat's session was cleared; any shared token remains active.";
    public const string LogoutIdle = "You are not logged in in this chat.";
    public const string Cancelled = "Cancelled.";
    public const string NothingToCancel = "Nothing to cancel.";
    public const string StepInProgress = "Finish or cancel the current step first.";
    public const string UnknownCommand = "I didn't understand that command.";
    public const string ShareNotLoggedIn = "Please log in first to share a token.";
    public const string ShareGuestDenied = "Only the owner can manage tokens.";
    public const string RevokeSuccess = "Access token revoked. Guests can no longer log in with it.";
    public const string ShareWarningTemplate = "Anyone with this token can view your allergies: {0} The previous token no longer works.";
    public const string DuplicateEmail = "That email is already registered.";
    public const string UnknownEmail = "No account found with that email.";
    public const string WrongPassword = "Incorrect password.";
    public const string InvalidToken = "That token is invalid or expired.";
    public const string InvalidInput = "Please provide a valid email and password.";
    public const string HelpCommands = "Available commands:\n/login — log in as the owner\n/register — create an owner account\n/share — generate a new guest token (owner only)\n/revoke — delete the guest token (owner only)\n/logout — clear this chat's session\n/cancel — cancel the current step\n/help — show this list";
    public const string HelpCommandsLoggedOut = "Available commands:\n/login — log in as the owner\n/register — create an owner account";

    public static string ForAuthError(AuthErrorCode code) => code switch
    {
        AuthErrorCode.InvalidInput => InvalidInput,
        AuthErrorCode.DuplicateEmail => DuplicateEmail,
        AuthErrorCode.UnknownEmail => UnknownEmail,
        AuthErrorCode.WrongPassword => WrongPassword,
        AuthErrorCode.InvalidToken => InvalidToken,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
    };
}
