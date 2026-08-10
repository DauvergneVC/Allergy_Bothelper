using Allergy_BotHelper.Tests.Fakes;
using Allergy_BotHelper.Tests.Fixtures;

namespace Allergy_BotHelper.Tests;

public class TokenTests
{
    private readonly FakeUserRepository _fake = new();
    private readonly AuthService _auth;

    public TokenTests()
    {
        _auth = new AuthService(_fake);
    }

    [Fact]
    public async Task LoginByToken_ValidToken_ReturnsOwner()
    {
        _fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "GUEST-TOKEN-1" });

        var user = await _auth.LoginByTokenAsync("GUEST-TOKEN-1");

        Assert.Equal("owner@example.com", user.Email);
        Assert.Equal(1, _fake.GetByUserShareTokenAsyncCalls);
    }

    [Fact]
    public async Task LoginByToken_OneToken_ServesDistinctGuests()
    {
        _fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "SHARED-TOKEN" });

        var firstGuest = await _auth.LoginByTokenAsync("SHARED-TOKEN");
        var secondGuest = await _auth.LoginByTokenAsync("SHARED-TOKEN");

        Assert.Equal(firstGuest.Id, secondGuest.Id);
        Assert.Equal("owner@example.com", firstGuest.Email);
    }

    [Fact]
    public async Task LoginByToken_UnknownToken_ThrowsInvalidToken()
    {
        _fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "KNOWN-TOKEN" });

        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.LoginByTokenAsync("unknown-token"));

        Assert.Equal(AuthErrorCode.InvalidToken, ex.Code);
        Assert.Equal("token inválido o no autorizado", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoginByToken_BlankToken_ThrowsInvalidToken_WithZeroLookups(string? token)
    {
        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.LoginByTokenAsync(token!));

        Assert.Equal(AuthErrorCode.InvalidToken, ex.Code);
        Assert.Equal("token inválido o no autorizado", ex.Message);
        Assert.Equal(0, _fake.GetByUserShareTokenAsyncCalls);
    }

    [Fact]
    public async Task LoginByToken_CaseVariant_Fails()
    {
        _fake.Seed(new User("owner@example.com", BcryptFixtures.Password123Hash) { ShareToken = "ABC-123" });

        var ex = await Assert.ThrowsAsync<AuthException>(() => _auth.LoginByTokenAsync("abc-123"));

        Assert.Equal(AuthErrorCode.InvalidToken, ex.Code);
    }
}
