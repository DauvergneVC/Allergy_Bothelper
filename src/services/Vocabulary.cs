public static class Vocabulary
{
    private static readonly Dictionary<string, IReadOnlyList<string>> CanonicalEntries = new()
    {
        ["peanut"] = new[] { "peanuts", "mani", "cacahuete", "cacahuate", "arachis", "manteca de mani" },
        ["gluten"] = new[] { "trigo", "wheat", "cebada", "barley", "centeno", "rye", "avena", "oats", "espelta", "semola", "harina de trigo" },
        ["lactose"] = new[] { "leche", "milk", "lactosa", "lacteos", "dairy", "caseina", "suero", "whey", "yogur", "yogurt", "queso", "cheese" },
        ["egg"] = new[] { "huevo", "huevos", "eggs", "albumina", "ovalbumina" },
        ["soy"] = new[] { "soja", "soya", "soybean", "edamame", "tofu" },
        ["fish"] = new[] { "pescado", "pez", "salmon", "merluza", "atun", "tuna", "anchoa", "anchovy", "bacalao", "cod" },
        ["shellfish"] = new[] { "marisco", "mariscos", "camaron", "shrimp", "gamba", "gambas", "langostino", "prawn", "cangrejo", "crab", "langosta", "lobster", "bogavante", "crayfish" },
        ["molluscs"] = new[] { "molusco", "moluscos", "mollusc", "mollusk", "mejillon", "mussel", "ostra", "oyster", "calamar", "squid", "pulpo", "octopus", "vieira", "scallop", "almeja", "clam", "caracol", "snail" },
        ["sesame"] = new[] { "sesamo", "ajonjoli", "tahini" },
        ["mustard"] = new[] { "mostaza" },
        ["celery"] = new[] { "apio", "celeriac" },
        ["lupin"] = new[] { "altramuz", "lupine", "lupino" },
        ["sulphites"] = new[] { "sulfito", "sulfitos", "sulfites", "sulfite", "dioxido de azufre", "sulfur dioxide", "metabisulfito" },
        ["tree nut"] = new[] { "tree nuts", "frutos secos", "frutos de cascara", "almendra", "almond", "nuez", "walnut", "avellana", "hazelnut", "anacardo", "cashew", "pistacho", "pistachio", "castana", "chestnut", "macadamia", "pecana", "pecan", "nuez de brasil", "brazil nut" },
    };

    private static readonly Dictionary<string, string> ReverseIndex = BuildReverseIndex();

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Entries => CanonicalEntries;

    public static string Canonicalize(string? term)
    {
        var normalized = TextNormalizer.Normalize(term);
        return ReverseIndex.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static bool TryGetCanonical(string? term, out string canonicalKey)
    {
        var normalized = TextNormalizer.Normalize(term);
        return ReverseIndex.TryGetValue(normalized, out canonicalKey!);
    }

    private static Dictionary<string, string> BuildReverseIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (canonical, synonyms) in CanonicalEntries)
        {
            index[canonical] = canonical;

            foreach (var synonym in synonyms)
            {
                index[TextNormalizer.Normalize(synonym)] = canonical;
            }
        }

        return index;
    }
}
