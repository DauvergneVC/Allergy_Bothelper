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

    // Bilingual ES/EN copy for the /add and ingredient-consult flows (additive; the
    // legacy strings above are untouched). Language is picked per reply via ForLanguage.
    public const string AllergyAddedEn = "Added allergies: {0}";
    public const string AllergyAddedEs = "Alergias agregadas: {0}";
    public const string AllergyAlreadyStoredEn = "Already on your list: {0}";
    public const string AllergyAlreadyStoredEs = "Ya estaban en tu lista: {0}";
    public const string AllergyUsageEn = "Use /add followed by the allergen or a list, for example:\n/add maní\n/add maní, trigo; avena";
    public const string AllergyUsageEs = "Usa /add seguido del alérgeno o una lista, por ejemplo:\n/add maní\n/add maní, trigo; avena";
    public const string AllergyOwnerOnlyEn = "Only the owner can add allergies.";
    public const string AllergyOwnerOnlyEs = "Solo el dueño puede agregar alergias.";
    public const string AllergyLoginPromptEn = "Please log in to add allergies.";
    public const string AllergyLoginPromptEs = "Inicia sesión para agregar alergias.";
    public const string IngredientMatchEn = "Allergen detected: {0}";
    public const string IngredientMatchEs = "Alérgeno detectado: {0}";
    public const string IngredientSafeEn = "No allergen detected.";
    public const string IngredientSafeEs = "No se detectaron alérgenos.";
    public const string OcrFailureEn = "I couldn't read the photo. Try sending the ingredients as text.";
    public const string OcrFailureEs = "No pude leer la foto. Prueba enviar los ingredientes como texto.";

    public static string ForLanguage(ReplyLanguageValue language, string en, string es)
        => language == ReplyLanguageValue.En ? en : es;

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
