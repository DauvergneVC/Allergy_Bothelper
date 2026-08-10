using Allergy_BotHelper.Tests.Fakes;

namespace Allergy_BotHelper.Tests;

public class RegisterTests
{
    private readonly FakeUserRepository _fake = new();
    private readonly AuthService _auth;

    public RegisterTests()
    {
        _auth = new AuthService(_fake);
    }

    [Fact]
    public async Task Register_StoresBcryptHash_NotPlaintext()
    {
        const string password = "password123";

        var user = await _auth.RegisterAsync("owner@example.com", password);

        Assert.StartsWith("$2a$12$", user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
    }

    [Fact]
    public async Task Register_NormalizesEmailBeforeStoring()
    {
        var user = await _auth.RegisterAsync("  User@Example.COM  ", "password123");

        Assert.Equal("user@example.com", user.Email);

        var stored = await _fake.GetUserByEmailAsync("user@example.com");
        Assert.Equal("user@example.com", stored!.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsDuplicateEmailWithPinnedMessage()
    {
        await _auth.RegisterAsync("dup@example.com", "password123");

        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.RegisterAsync("dup@example.com", "password123"));

        Assert.Equal(AuthErrorCode.DuplicateEmail, ex.Code);
        Assert.Equal("el email ya está registrado", ex.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_WhenPreCheckMisses_BackstopStillThrowsDuplicateEmail()
    {
        // Simulates the concurrent race: the pre-check sees no existing user, but the
        // insert hits the duplicate key (fake throws MongoWriteException with the
        // DuplicateKey category, mirroring the unique index backstop).
        await _auth.RegisterAsync("race@example.com", "password123");
        _fake.HideExistingUsersOnLookup = true;

        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.RegisterAsync("race@example.com", "password123"));

        Assert.Equal(AuthErrorCode.DuplicateEmail, ex.Code);
        Assert.Equal("el email ya está registrado", ex.Message);
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("   ", "password123")]
    [InlineData("owner@example.com", "")]
    [InlineData("owner@example.com", "   ")]
    public async Task Register_EmptyOrWhitespaceCredentials_ThrowsRequiredFields_WithZeroLookups(string email, string password)
    {
        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.RegisterAsync(email, password));

        Assert.Equal(AuthErrorCode.InvalidInput, ex.Code);
        Assert.Equal("email y contraseña son obligatorios", ex.Message);
        Assert.Equal(0, _fake.GetUserByEmailAsyncCalls);
    }

    [Fact]
    public async Task Register_OverLongPassword_ThrowsTooLong_WithZeroLookups()
    {
        var password = new string('a', 73); // 73 bytes

        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.RegisterAsync("owner@example.com", password));

        Assert.Equal(AuthErrorCode.InvalidInput, ex.Code);
        Assert.Equal("la contraseña es demasiado larga", ex.Message);
        Assert.Equal(0, _fake.GetUserByEmailAsyncCalls);
    }

    [Fact]
    public async Task Register_PasswordLength_IsEnforcedOnUtf8Bytes_NotCharacters()
    {
        // 36 x 'ñ' (2 UTF-8 bytes each) = exactly 72 bytes: allowed.
        var user = await _auth.RegisterAsync("boundary@example.com", new string('ñ', 36));
        Assert.NotNull(user.PasswordHash);

        // 37 x 'ñ' = 74 bytes: rejected.
        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.RegisterAsync("boundary@example.com", new string('ñ', 37)));
        Assert.Equal(AuthErrorCode.InvalidInput, ex.Code);
        Assert.Equal("la contraseña es demasiado larga", ex.Message);
    }

    [Fact]
    public async Task Register_SpacedPassword_IsStoredUntrimmed()
    {
        var user = await _auth.RegisterAsync("spaced@example.com", "  spaced  ");

        Assert.True(BCrypt.Net.BCrypt.Verify("  spaced  ", user.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("spaced", user.PasswordHash));
    }
}
