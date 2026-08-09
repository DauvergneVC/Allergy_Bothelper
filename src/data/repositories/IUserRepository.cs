using MongoDB.Bson;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(ObjectId userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task DeleteUserAsync(ObjectId userId);
}