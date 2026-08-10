using MongoDB.Bson;

public class AllergyService : IAllergyService
{
    private readonly UserRepository _userRepository;

    public AllergyService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task AddAsync(ObjectId userId, string allergy)
    {
        throw new NotImplementedException();

    }

    public async Task RemoveAsync(ObjectId userId, string allergy)
    {
        throw new NotImplementedException();

    }

    public async Task<IReadOnlyList<string>> GetAllergiesAsync(ObjectId userId)
    {
        throw new NotImplementedException();
    }
}
