namespace Allergy_BotHelper.Tests;

public class GuardBindingTests
{
    [Theory]
    [InlineData(AuthErrorCode.DuplicateEmail, "el email ya está registrado")]
    [InlineData(AuthErrorCode.UnknownEmail, "email no registrado")]
    [InlineData(AuthErrorCode.WrongPassword, "contraseña incorrecta")]
    [InlineData(AuthErrorCode.InvalidToken, "token inválido o no autorizado")]
    [InlineData(AuthErrorCode.InvalidInput, "email y contraseña son obligatorios")]
    public void AuthException_DefaultMessage_BindsToPinnedSpanishString(AuthErrorCode code, string expected)
    {
        var ex = new AuthException(code);

        Assert.Equal(expected, ex.Message);
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void AuthException_InvalidInput_OverLongPassword_HasPinnedMessage()
    {
        var ex = new AuthException(AuthErrorCode.InvalidInput, "la contraseña es demasiado larga");

        Assert.Equal("la contraseña es demasiado larga", ex.Message);
        Assert.Equal(AuthErrorCode.InvalidInput, ex.Code);
    }
}
