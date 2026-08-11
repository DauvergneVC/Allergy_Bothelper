namespace Allergy_BotHelper.Tests;

public class IngredientMatcherTests
{
    [Fact]
    public void Match_SynonymDirectMatch_ReportsCanonicalAndOffendingToken()
    {
        var result = IngredientMatcher.Match(new[] { "maní" }, new[] { "peanut" });

        var match = Assert.Single(result.Matches);
        Assert.Equal("peanut", match.CanonicalKey);
        Assert.Equal(new[] { "maní" }, match.OffendingTokens);
    }

    [Fact]
    public void Match_ContainmentMatch_ReportsMatch()
    {
        var result = IngredientMatcher.Match(new[] { "contiene gluten" }, new[] { "gluten" });

        var match = Assert.Single(result.Matches);
        Assert.Equal("gluten", match.CanonicalKey);
        Assert.Equal(new[] { "contiene gluten" }, match.OffendingTokens);
    }

    [Fact]
    public void Match_NoMatch_ReturnsEmptyResult()
    {
        var result = IngredientMatcher.Match(new[] { "agua", "sal" }, new[] { "peanut" });

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Match_MultipleAllergens_ReportsEachWithTokens()
    {
        var result = IngredientMatcher.Match(
            new[] { "maní", "leche" },
            new[] { "peanut", "lactose" });

        Assert.Equal(2, result.Matches.Count);
        Assert.Contains(result.Matches, m => m.CanonicalKey == "peanut" && m.OffendingTokens.Contains("maní"));
        Assert.Contains(result.Matches, m => m.CanonicalKey == "lactose" && m.OffendingTokens.Contains("leche"));
    }

    [Fact]
    public void Match_UnknownTokenAlongsideKnown_KnownStillMatches()
    {
        var result = IngredientMatcher.Match(new[] { "xyzzy", "leche" }, new[] { "lactose" });

        var match = Assert.Single(result.Matches);
        Assert.Equal("lactose", match.CanonicalKey);
        Assert.Equal(new[] { "leche" }, match.OffendingTokens);
    }

    [Fact]
    public void Match_AllTokensUnknown_ReturnsEmptyWithoutError()
    {
        var result = IngredientMatcher.Match(new[] { "xyzzy", "plumbus" }, new[] { "peanut" });

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Match_EmptyOwnerKeys_NeverMatches()
    {
        var result = IngredientMatcher.Match(new[] { "maní", "leche" }, Array.Empty<string>());

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Match_RepeatedOffendingToken_IsDeduplicated()
    {
        var result = IngredientMatcher.Match(new[] { "maní", "Maní" }, new[] { "peanut" });

        var match = Assert.Single(result.Matches);
        Assert.Equal("peanut", match.CanonicalKey);
        Assert.Single(match.OffendingTokens);
    }

    [Fact]
    public void Match_IsDeterministic()
    {
        var tokens = new[] { "maní", "contiene gluten", "xyzzy" };
        var keys = new[] { "peanut", "gluten" };

        var first = IngredientMatcher.Match(tokens, keys);
        var second = IngredientMatcher.Match(tokens, keys);

        Assert.Equal(
            first.Matches.Select(m => (m.CanonicalKey, Tokens: string.Join("|", m.OffendingTokens))),
            second.Matches.Select(m => (m.CanonicalKey, Tokens: string.Join("|", m.OffendingTokens))));
    }
}
