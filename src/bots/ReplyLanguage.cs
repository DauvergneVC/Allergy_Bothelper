public enum ReplyLanguageValue
{
    En,
    Es,
}

public static class ReplyLanguage
{
    private const string EsDiacritics = "ñáéíóúüÑÁÉÍÓÚÜ";

    private static readonly string[] EsPrefixes =
    {
        "examina estos ingredientes",
        "mira esto",
        "chequea",
        "analiza",
        "revisa",
        "mira",
    };

    public static ReplyLanguageValue Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ReplyLanguageValue.En;
        }

        if (text.AsSpan().IndexOfAny(EsDiacritics) >= 0)
        {
            return ReplyLanguageValue.Es;
        }

        var folded = TextNormalizer.FoldCaseAndAccents(text);

        foreach (var prefix in EsPrefixes)
        {
            if (!folded.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (folded.Length == prefix.Length || !char.IsLetter(folded[prefix.Length]))
            {
                return ReplyLanguageValue.Es;
            }
        }

        return ReplyLanguageValue.En;
    }
}
