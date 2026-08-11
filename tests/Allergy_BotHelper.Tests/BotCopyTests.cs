using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class BotCopyTests
{
    [Theory]
    [InlineData(AuthErrorCode.InvalidInput, "Please provide a valid email and password.")]
    [InlineData(AuthErrorCode.DuplicateEmail, "That email is already registered.")]
    [InlineData(AuthErrorCode.UnknownEmail, "No account found with that email.")]
    [InlineData(AuthErrorCode.WrongPassword, "Incorrect password.")]
    [InlineData(AuthErrorCode.InvalidToken, "That token is invalid or expired.")]
    public void AuthError_Code_MapsToExactEnglishCopy(AuthErrorCode code, string expected)
    {
        Assert.Equal(expected, BotCopy.ForAuthError(code));
    }

    [Fact]
    public void ShareWarningTemplate_FormatsTokenIntoMessage()
    {
        var message = string.Format(BotCopy.ShareWarningTemplate, "ABC-123");

        Assert.Equal(
            "Anyone with this token can view your allergies: ABC-123 The previous token no longer works.",
            message);
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/share")]
    [InlineData("/revoke")]
    [InlineData("/logout")]
    [InlineData("/cancel")]
    [InlineData("/help")]
    public void HelpCommands_ListsEveryCommand(string command)
    {
        Assert.Contains(command, BotCopy.HelpCommands);
    }

    [Fact]
    public void HelpCommandsLoggedOut_ListsOnlyLoginAndRegister()
    {
        Assert.Contains("/login", BotCopy.HelpCommandsLoggedOut);
        Assert.Contains("/register", BotCopy.HelpCommandsLoggedOut);
        Assert.DoesNotContain("/share", BotCopy.HelpCommandsLoggedOut);
        Assert.DoesNotContain("/revoke", BotCopy.HelpCommandsLoggedOut);
        Assert.DoesNotContain("/logout", BotCopy.HelpCommandsLoggedOut);
        Assert.DoesNotContain("/cancel", BotCopy.HelpCommandsLoggedOut);
        Assert.DoesNotContain("/help", BotCopy.HelpCommandsLoggedOut);
    }

    [Fact]
    public async Task AuthFailureThroughHandler_RepliesPinnedEnglish_WithNoSpanishLeak()
    {
        var fake = new FakeUserRepository();
        fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash));
        var handler = new BotAuthHandler(new AuthService(fake), new ShareService(fake), new AllergyService(fake), new StubOcrService());
        const long chatId = 1;
        var session = new ChatSession { ChatId = chatId };

        await handler.HandleAsync(chatId, session, "/login", null, CancellationToken.None);
        await handler.HandleAsync(chatId, session, "owner@example.com", null, CancellationToken.None);
        var reply = await handler.HandleAsync(chatId, session, "wrong-password", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.WrongPassword, reply!.Text);

        string[] spanishFragments = { "registrado", "contraseña", "token inválido", "email", "no autorizado" };
        foreach (var fragment in spanishFragments)
        {
            Assert.DoesNotContain(fragment, reply.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(ReplyLanguageValue.En)]
    [InlineData(ReplyLanguageValue.Es)]
    public void ForLanguage_SelectsTheMatchingLanguageString(ReplyLanguageValue language)
    {
        const string en = "english";
        const string es = "español";

        var selected = BotCopy.ForLanguage(language, en, es);

        Assert.Equal(language == ReplyLanguageValue.En ? en : es, selected);
    }

    [Theory]
    [InlineData(BotCopy.AllergyAddedEn, "Added allergies: {0}")]
    [InlineData(BotCopy.AllergyAddedEs, "Alergias agregadas: {0}")]
    [InlineData(BotCopy.AllergyAlreadyStoredEn, "Already on your list: {0}")]
    [InlineData(BotCopy.AllergyAlreadyStoredEs, "Ya estaban en tu lista: {0}")]
    [InlineData(BotCopy.AllergyUsageEn, "Use /add followed by the allergen or a list, for example:\n/add maní\n/add maní, trigo; avena")]
    [InlineData(BotCopy.AllergyUsageEs, "Usa /add seguido del alérgeno o una lista, por ejemplo:\n/add maní\n/add maní, trigo; avena")]
    [InlineData(BotCopy.AllergyOwnerOnlyEn, "Only the owner can add allergies.")]
    [InlineData(BotCopy.AllergyOwnerOnlyEs, "Solo el dueño puede agregar alergias.")]
    [InlineData(BotCopy.AllergyLoginPromptEn, "Please log in to add allergies.")]
    [InlineData(BotCopy.AllergyLoginPromptEs, "Inicia sesión para agregar alergias.")]
    public void AllergyAddCopy_IsBilingualAndPinned(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(BotCopy.IngredientMatchEn, "Allergen detected: {0}")]
    [InlineData(BotCopy.IngredientMatchEs, "Alérgeno detectado: {0}")]
    [InlineData(BotCopy.IngredientSafeEn, "No allergen detected.")]
    [InlineData(BotCopy.IngredientSafeEs, "No se detectaron alérgenos.")]
    [InlineData(BotCopy.OcrFailureEn, "I couldn't read the photo. Try sending the ingredients as text.")]
    [InlineData(BotCopy.OcrFailureEs, "No pude leer la foto. Prueba enviar los ingredientes como texto.")]
    public void IngredientVerdictAndOcrCopy_IsBilingualAndPinned(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
