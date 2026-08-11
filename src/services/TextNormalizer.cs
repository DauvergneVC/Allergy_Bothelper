using System.Globalization;
using System.Text;

public static class TextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var folded = FoldCaseAndAccents(text);
        var builder = new StringBuilder(folded.Length);

        foreach (var ch in folded)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> Tokenize(string? text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        return normalized
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    internal static string FoldCaseAndAccents(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (UnicodeCategory.NonSpacingMark != CharUnicodeInfo.GetUnicodeCategory(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
