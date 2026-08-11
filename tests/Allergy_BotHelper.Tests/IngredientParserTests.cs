namespace Allergy_BotHelper.Tests;

public class IngredientParserTests
{
    [Fact]
    public void StripPrefix_EsPrefixWithColon_StripsPrefixAndColon()
    {
        Assert.Equal("maní y leche", IngredientParser.StripPrefix("mira esto: maní y leche"));
    }

    [Fact]
    public void StripPrefix_EnPrefixWithNewline_StripsPrefixAndNewline()
    {
        Assert.Equal("leche", IngredientParser.StripPrefix("check this\nleche"));
    }

    [Fact]
    public void StripPrefix_CaseAndAccentInsensitive_StillStrips()
    {
        Assert.Equal("maní", IngredientParser.StripPrefix("MIRÁ ESTO maní"));
    }

    [Fact]
    public void StripPrefix_LongEsPrefix_Strips()
    {
        Assert.Equal("maní", IngredientParser.StripPrefix("examina estos ingredientes maní"));
    }

    [Theory]
    [InlineData("analiza esto: maní", "esto: maní")]
    [InlineData("revisa maní", "maní")]
    [InlineData("chequea maní", "maní")]
    [InlineData("look at this: leche", "leche")]
    [InlineData("examine these: leche", "leche")]
    [InlineData("review leche", "leche")]
    [InlineData("analyze leche", "leche")]
    public void StripPrefix_AllSupportedPrefixes(string input, string expected)
    {
        Assert.Equal(expected, IngredientParser.StripPrefix(input));
    }

    [Fact]
    public void StripPrefix_NoPrefix_ReturnsWholeText()
    {
        Assert.Equal("maní y leche", IngredientParser.StripPrefix("maní y leche"));
    }

    [Fact]
    public void StripPrefix_PrefixWordIsNotStrippedInsideLongerWord()
    {
        Assert.Equal("mirador", IngredientParser.StripPrefix("mirador"));
    }

    [Fact]
    public void StripPrefix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, IngredientParser.StripPrefix(null));
        Assert.Equal(string.Empty, IngredientParser.StripPrefix(""));
    }

    [Fact]
    public void SplitItems_CommasSemicolonsAndNewlines_ProduceFourItems()
    {
        var items = IngredientParser.SplitItems("maní, trigo; avena\nlácteos");

        Assert.Equal(new[] { "maní", "trigo", "avena", "lácteos" }, items);
    }

    [Fact]
    public void SplitItems_BulletsAndNumbering_ProduceThreeItems()
    {
        var items = IngredientParser.SplitItems("- maní\n• trigo\n1. avena");

        Assert.Equal(new[] { "maní", "trigo", "avena" }, items);
    }

    [Fact]
    public void SplitItems_NoSeparators_SingleItem()
    {
        Assert.Equal(new[] { "maní" }, IngredientParser.SplitItems("maní"));
    }

    [Fact]
    public void SplitItems_ConsultList_ProducesThreeItems()
    {
        var items = IngredientParser.SplitItems("maní, leche\ncontiene gluten");

        Assert.Equal(new[] { "maní", "leche", "contiene gluten" }, items);
    }

    [Fact]
    public void SplitItems_EmptyAndBlankSegments_AreDropped()
    {
        var items = IngredientParser.SplitItems("maní,, ;\n  \ntrigo");

        Assert.Equal(new[] { "maní", "trigo" }, items);
    }

    [Fact]
    public void SplitItems_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(IngredientParser.SplitItems(null));
        Assert.Empty(IngredientParser.SplitItems(""));
    }
}
