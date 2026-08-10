using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
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
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var result = await _usersCollection.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(u => u.Id, user),
            Builders<User>.Update.Set(u => u.ShareToken, token));

        if (result is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        return token;
    }

    public async Task RevokeTokenAsync(ObjectId userId)
    {
        var result = await _usersCollection.FindOneAndUpdateAsync(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Update.Set(u => u.ShareToken, null));

        if (result is null)
        {
            throw new InvalidOperationException("User not found.");
        }
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