using System.Text;
using MongoDB.Driver;

public class AuthService : IAuthService
{
    private const int WorkFactor = 12;

    // Valid work-factor-12 bcrypt hash of random data. Verifies false for every real
    // password; used to keep unknown-email and wrong-password timings comparable.
    private const string DummyHash = "$2a$12$yRa.f4SebU8lvFnVjSRXE.1PZ5JlxyQPTAtnfVQbfKGQuAkkGh0RO";

    private const string RequiredFieldsMessage = "email y contraseña son obligatorios";
    private const string PasswordTooLongMessage = "la contraseña es demasiado larga";

    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User> RegisterAsync(string email, string password)
    {
        email = NormalizeEmail(email);
        ValidateCredentials(email, password);

        // Optimization only: the unique email index is the actual duplicate guarantee.
        if (await _userRepository.GetUserByEmailAsync(email) is not null)
        {
            throw new AuthException(AuthErrorCode.DuplicateEmail);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        var user = new User(email, passwordHash);

        try
        {
            await _userRepository.CreateUserAsync(user);
        }
        // Backstop: a concurrent registration can slip past the pre-check above.
        // Match the write-error category, not the outer exception type, because the
        // duplicate may surface as either MongoWriteException or MongoBulkWriteException.
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new AuthException(AuthErrorCode.DuplicateEmail);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey))
        {
            throw new AuthException(AuthErrorCode.DuplicateEmail);
        }

        return user;
    }

    public async Task<User> LoginAsync(string email, string password)
    {
        throw new NotImplementedException();
    }

    public async Task<User> LoginByTokenAsync(string token)
    {
        throw new NotImplementedException();
    }

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new AuthException(AuthErrorCode.InvalidInput, RequiredFieldsMessage);
        }
        // BCrypt only reads the first 72 bytes; reject longer passwords up front,
        // before any hashing or lookup. The password itself is NEVER trimmed.
        if (Encoding.UTF8.GetByteCount(password) > 72)
        {
            throw new AuthException(AuthErrorCode.InvalidInput, PasswordTooLongMessage);
        }
    }

    private static void VerifyAgainstDummy(string password)
    {
        // Performed for unknown emails and legacy null hashes so the observable
        // timing stays comparable with a real password verification.
        BCrypt.Net.BCrypt.Verify(password, DummyHash);
    }
}
