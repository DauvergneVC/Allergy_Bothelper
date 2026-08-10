public class AuthService : IAuthService
{
    private readonly UserRepository _userRepository;

    public AuthService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User> RegisterAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        // Implement registration logic here
        throw new NotImplementedException();
    }

    public async Task<User> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        // Implement login logic here
        throw new NotImplementedException();
    }

    public async Task<User> LoginByTokenAsync(string token)
    {
        // Implement login by token logic here
        throw new NotImplementedException();
    }
}
