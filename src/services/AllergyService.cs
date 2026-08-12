using MongoDB.Bson;

public class AllergyService : IAllergyService
{
    private readonly IUserRepository _userRepository;

    public AllergyService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> AddAsync(ObjectId userId, string canonical, string display)
    {
        var canonicalKey = Vocabulary.Canonicalize(canonical);

        var user = await _userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        user.Allergies ??= new List<string>();
        user.AllergyDisplay ??= new List<string>();

        // ADD-9: the canonical key is stored at most once. A repeated add (exact or
        // synonym) is idempotent and never duplicates the display label.
        if (user.Allergies.Contains(canonicalKey))
        {
            return false;
        }

        user.Allergies.Add(canonicalKey);
        user.AllergyDisplay.Add(display);
        await _userRepository.UpdateUserAsync(user).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<string>> GetAllergiesAsync(ObjectId userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
        return user?.Allergies ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public async Task<IReadOnlyList<(string Canonical, string Display)>> GetAllergiesWithDisplayAsync(ObjectId userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
        if (user?.Allergies is null || user.Allergies.Count == 0)
        {
            return Array.Empty<(string, string)>();
        }

        var display = user.AllergyDisplay ?? new List<string>();
        var result = new List<(string, string)>(user.Allergies.Count);

        for (var i = 0; i < user.Allergies.Count; i++)
        {
            var canonical = user.Allergies[i];
            var displayLabel = i < display.Count ? display[i] : canonical;
            result.Add((canonical, displayLabel));
        }

        return result;
    }

    public async Task<int> RemoveAsync(ObjectId userId, IEnumerable<string> canonicalKeys)
    {
        var user = await _userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
        if (user?.Allergies is null || user.Allergies.Count == 0)
        {
            return 0;
        }

        var keysToRemove = canonicalKeys.ToHashSet();
        var removed = 0;

        // Remove from both Allergies and AllergyDisplay in sync
        var display = user.AllergyDisplay ?? new List<string>();
        var newAllergies = new List<string>();
        var newDisplay = new List<string>();

        for (var i = 0; i < user.Allergies.Count; i++)
        {
            var canonical = user.Allergies[i];
            if (keysToRemove.Contains(canonical))
            {
                removed++;
            }
            else
            {
                newAllergies.Add(canonical);
                if (i < display.Count)
                {
                    newDisplay.Add(display[i]);
                }
            }
        }

        if (removed > 0)
        {
            user.Allergies = newAllergies;
            user.AllergyDisplay = newDisplay;
            await _userRepository.UpdateUserAsync(user).ConfigureAwait(false);
        }

        return removed;
    }
}
