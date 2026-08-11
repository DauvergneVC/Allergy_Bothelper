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
}
