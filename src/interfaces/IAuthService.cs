public interface IAuthService
{
    Task<User> RegisterAsync(string email, string password);
    Task<User> LoginAsync(string email, string password);
    Task<User> LoginByTokenAsync(string token);
}