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
}
