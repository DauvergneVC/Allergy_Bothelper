using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Driver;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _usersCollection;

    public UserRepository(MongoDbContext _context)
    {
        _usersCollection = _context.GetCollection<User>("Users");
    }


    public async Task<User?> GetUserByIdAsync(ObjectId userId)
    {
        return await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByUserShareTokenAsync(string token)
    {
        return await _usersCollection.Find(u => u.ShareToken == token).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _usersCollection.Find(u => u.Email == email).FirstOrDefaultAsync();
    }
    public async Task<string> GenerateTokenAsync(ObjectId user)
    {
        throw new NotImplementedException();
    }

    public async Task RevokeTokenAsync(ObjectId userId)
    {
        throw new NotImplementedException();
    }

    // With validations
    public async Task CreateUserAsync(User user)
    {
        List<ValidationResult> results = new List<ValidationResult>();
        Validator.TryValidateObject(user, new ValidationContext(user), results, validateAllProperties: true);
        if (results.Count > 0)
            throw new ValidationException(string.Join("; ", results.Select(r => r.ErrorMessage)));

        await _usersCollection.InsertOneAsync(user);
    }

    public async Task UpdateUserAsync(User user)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(user, new ValidationContext(user), results, validateAllProperties: true);
        if (results.Count > 0)
            throw new ValidationException(string.Join("; ", results.Select(r => r.ErrorMessage)));

        await _usersCollection.ReplaceOneAsync(u => u.Id == user.Id, user);
    }

    public async Task DeleteUserAsync(ObjectId userId)
    {
        await _usersCollection.DeleteOneAsync(u => u.Id == userId);
    }
}