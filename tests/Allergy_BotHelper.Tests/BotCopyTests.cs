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
        var handler = new BotAuthHandler(new AuthService(fake), new ShareService(fake));
        const long chatId = 1;

        await handler.HandleAsync(chatId, "/login", null, CancellationToken.None);
        await handler.HandleAsync(chatId, "owner@example.com", null, CancellationToken.None);
        var reply = await handler.HandleAsync(chatId, "wrong-password", null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(BotCopy.WrongPassword, reply!.Text);

        string[] spanishFragments = { "registrado", "contraseña", "token inválido", "email", "no autorizado" };
        foreach (var fragment in spanishFragments)
        {
            Assert.DoesNotContain(fragment, reply.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
