using MongoDB.Bson;

public interface IAllergyService
{
    Task AddAsync(ObjectId userId, string allergy);
    Task RemoveAsync(ObjectId userId, string allergy);
    Task<IReadOnlyList<string>> GetAllergiesAsync(ObjectId userId);
}