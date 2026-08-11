namespace Allergy_BotHelper.Tests;

public class ReplyLanguageTests
{
    [Theory]
    [InlineData("maní")]
    [InlineData("¿tengo lácteos?")]
    [InlineData("añadir")]
    [InlineData("pingüino")]
    public void Detect_EsDiacritic_ReturnsEs(string text)
    {
        Assert.Equal(ReplyLanguageValue.Es, ReplyLanguage.Detect(text));
    }

    [Theory]
    [InlineData("mira esto: peanut")]
    [InlineData("analiza peanut")]
    [InlineData("revisa peanut")]
    [InlineData("chequea peanut")]
    [InlineData("MIRÁ ESTO peanut")]
    public void Detect_EsPrefix_ReturnsEs(string text)
    {
        Assert.Equal(ReplyLanguageValue.Es, ReplyLanguage.Detect(text));
    }

    [Theory]
    [InlineData("check this: peanut")]
    [InlineData("peanut, milk")]
    [InlineData("")]
    public void Detect_NoEsSignal_ReturnsEn(string text)
    {
        Assert.Equal(ReplyLanguageValue.En, ReplyLanguage.Detect(text));
    }

    [Fact]
    public void Detect_Null_ReturnsEn()
    {
        Assert.Equal(ReplyLanguageValue.En, ReplyLanguage.Detect(null));
    }

    [Fact]
    public void Detect_IsDeterministic_ForIdenticalInput()
    {
        const string input = "mira esto: maní y leche";

        Assert.Equal(ReplyLanguage.Detect(input), ReplyLanguage.Detect(input));
    }
}
