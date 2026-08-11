namespace Allergy_BotHelper.Tests;

public class VocabularyTests
{
    [Theory]
    [InlineData("maní", "peanut")]
    [InlineData("Maní", "peanut")]
    [InlineData("cacahuete", "peanut")]
    [InlineData("peanut", "peanut")]
    [InlineData("trigo", "gluten")]
    [InlineData("wheat", "gluten")]
    [InlineData("leche", "lactose")]
    [InlineData("milk", "lactose")]
    [InlineData("huevo", "egg")]
    [InlineData("soja", "soy")]
    [InlineData("pescado", "fish")]
    [InlineData("camarón", "shellfish")]
    [InlineData("mejillón", "molluscs")]
    [InlineData("sésamo", "sesame")]
    [InlineData("mostaza", "mustard")]
    [InlineData("apio", "celery")]
    [InlineData("altramuz", "lupin")]
    [InlineData("sulfitos", "sulphites")]
    [InlineData("almendra", "tree nut")]
    public void Canonicalize_MapsSynonymsToCanonicalKey(string term, string expected)
    {
        Assert.Equal(expected, Vocabulary.Canonicalize(term));
    }

    [Theory]
    [InlineData("xyzzy")]
    [InlineData("quinoa")]
    public void Canonicalize_UnmappedTerm_FallsBackToOwnNormalizedForm(string term)
    {
        Assert.Equal(TextNormalizer.Normalize(term), Vocabulary.Canonicalize(term));
    }

    [Fact]
    public void Canonicalize_UnmappedAccentedTerm_FallsBackNormalized()
    {
        Assert.Equal("canela", Vocabulary.Canonicalize("Canela"));
    }

    [Fact]
    public void TryGetCanonical_MappedTerm_ReturnsTrueAndKey()
    {
        Assert.True(Vocabulary.TryGetCanonical("cacahuete", out var key));
        Assert.Equal("peanut", key);
    }

    [Fact]
    public void TryGetCanonical_UnmappedTerm_ReturnsFalse()
    {
        Assert.False(Vocabulary.TryGetCanonical("xyzzy", out _));
    }

    [Fact]
    public void Entries_KeysAreAllLowercaseNormalized()
    {
        foreach (var key in Vocabulary.Entries.Keys)
        {
            Assert.Equal(TextNormalizer.Normalize(key), key);
        }
    }
}
