using MongoDB.Bson;

public interface IAllergyService
{
    /// <summary>
    /// Persists one allergen for the user. The item is canonicalized through the
    /// vocabulary and stored at most once (idempotent). Returns true when a new
    /// canonical key was stored, false when it was already present.
    /// </summary>
    Task<bool> AddAsync(ObjectId userId, string canonical, string display);
    Task<IReadOnlyList<string>> GetAllergiesAsync(ObjectId userId);

    /// <summary>
    /// Returns the user's allergens as (canonical, display) pairs. Empty list if the user
    /// has no allergens or doesn't exist.
    /// </summary>
    Task<IReadOnlyList<(string Canonical, string Display)>> GetAllergiesWithDisplayAsync(ObjectId userId);

    /// <summary>
    /// Removes the specified canonical allergens from the user. Returns the count of allergens
    /// actually removed. Unknown canonical keys are silently ignored.
    /// </summary>
    Task<int> RemoveAsync(ObjectId userId, IEnumerable<string> canonicalKeys);
}
