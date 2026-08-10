using MongoDB.Bson;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(ObjectId userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetByUserShareTokenAsync(string token);
    Task CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(ObjectId userId);

    // Share
    Task<string> GenerateTokenAsync(ObjectId user);
    Task RevokeTokenAsync(ObjectId userId);

}