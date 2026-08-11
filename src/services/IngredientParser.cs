using System.Text.RegularExpressions;

public static partial class IngredientParser
{
    private static readonly string[] Prefixes =
    {
        "examina estos ingredientes",
        "examine these",
        "look at this",
        "mira esto",
        "check this",
        "chequea",
        "analiza",
        "analyze",
        "revisa",
        "review",
        "mira",
    };

    public static string StripPrefix(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var folded = TextNormalizer.FoldCaseAndAccents(text);

        foreach (var prefix in Prefixes)
        {
            if (!folded.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (folded.Length > prefix.Length && char.IsLetter(folded[prefix.Length]))
            {
                continue;
            }

            return text[prefix.Length..].TrimStart(' ', '\t', '\r', '\n', ':').TrimEnd();
        }

        return text.Trim();
    }

    public static IReadOnlyList<string> SplitItems(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();

        foreach (var rawLine in text.Replace('\r', '\n').Replace('•', '\n').Replace('*', '\n').Split('\n'))
        {
            var line = BulletMarkerRegex().Replace(rawLine.TrimStart(), string.Empty);

            foreach (var piece in line.Split(',', ';'))
            {
                var item = piece.Trim();
                if (item.Length > 0)
                {
                    items.Add(item);
                }
            }
        }

        return items;
    }

    [GeneratedRegex(@"^(?:[-]|\d+[.)])\s+")]
    private static partial Regex BulletMarkerRegex();
}
