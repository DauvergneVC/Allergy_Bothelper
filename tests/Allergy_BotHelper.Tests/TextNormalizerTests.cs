namespace Allergy_BotHelper.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Maní", "mani")]
    [InlineData("maní", "mani")]
    [InlineData("Cacahuete", "cacahuete")]
    [InlineData("LÁCTEOS", "lacteos")]
    [InlineData("trigo", "trigo")]
    [InlineData("MIRÁ ESTO", "mira esto")]
    public void Normalize_FoldsCaseAndAccents(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_StripsPunctuation()
    {
        Assert.Equal("mani  leche", TextNormalizer.Normalize("maní, leche."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrBlank_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespace_AfterFolding()
    {
        var tokens = TextNormalizer.Tokenize("Maní, Leche");

        Assert.Equal(new[] { "mani", "leche" }, tokens);
    }

    [Fact]
    public void Tokenize_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(TextNormalizer.Tokenize(""));
        Assert.Empty(TextNormalizer.Tokenize(null));
    }

    [Fact]
    public void Normalize_IsDeterministic()
    {
        var input = "Maní, Leche; Trigo.";
        Assert.Equal(TextNormalizer.Normalize(input), TextNormalizer.Normalize(input));
    }
}
