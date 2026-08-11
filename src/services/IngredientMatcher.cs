public record AllergenMatch(string CanonicalKey, IReadOnlyList<string> OffendingTokens);

public record MatchResult(IReadOnlyList<AllergenMatch> Matches);

public static class IngredientMatcher
{
    public static MatchResult Match(IEnumerable<string> tokens, IEnumerable<string> ownerCanonicalKeys)
    {
        var ownerKeys = new HashSet<string>(
            ownerCanonicalKeys.Select(TextNormalizer.Normalize).Where(k => k.Length > 0),
            StringComparer.Ordinal);

        var matches = new Dictionary<string, (List<string> Raw, HashSet<string> Seen)>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            foreach (var key in MatchedKeys(token, ownerKeys))
            {
                if (!matches.TryGetValue(key, out var entry))
                {
                    entry = (new List<string>(), new HashSet<string>(StringComparer.Ordinal));
                    matches[key] = entry;
                }

                if (entry.Seen.Add(TextNormalizer.Normalize(token)))
                {
                    entry.Raw.Add(token);
                }
            }
        }

        return new MatchResult(matches
            .Select(pair => new AllergenMatch(pair.Key, pair.Value.Raw))
            .ToList());
    }

    private static IEnumerable<string> MatchedKeys(string token, HashSet<string> ownerKeys)
    {
        var normalized = TextNormalizer.Normalize(token);

        foreach (var subToken in TextNormalizer.Tokenize(token))
        {
            var canonical = Vocabulary.Canonicalize(subToken);
            if (ownerKeys.Contains(canonical))
            {
                yield return canonical;
            }
        }

        foreach (var key in ownerKeys)
        {
            if (ContainsWord(normalized, key))
            {
                yield return key;
            }
        }
    }

    private static bool ContainsWord(string normalizedToken, string canonicalKey)
    {
        var index = normalizedToken.IndexOf(canonicalKey, StringComparison.Ordinal);

        while (index >= 0)
        {
            var before = index == 0 ? ' ' : normalizedToken[index - 1];
            var after = index + canonicalKey.Length >= normalizedToken.Length
                ? ' '
                : normalizedToken[index + canonicalKey.Length];

            if (!char.IsLetter(before) && !char.IsLetter(after))
            {
                return true;
            }

            index = normalizedToken.IndexOf(canonicalKey, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
