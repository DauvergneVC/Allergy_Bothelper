namespace Allergy_BotHelper.Tests.Integration;

public class ShareIntegrationTests : IntegrationTestBase
{
    [MongoFact]
    public async Task ShareToken_FullRoundTrip_GenerateLoginRegenerateRevoke()
    {
        var owner = await Auth.RegisterAsync("share@example.com", "password123");
        var share = new ShareService(Repository);

        var token = await share.GenerateTokenAsync(owner.Id);

        var guest = await Auth.LoginByTokenAsync(token);
        Assert.Equal(owner.Id, guest.Id);

        var regenerated = await share.GenerateTokenAsync(owner.Id);
        Assert.NotEqual(regenerated, token);

        var stale = await Assert.ThrowsAsync<AuthException>(() => Auth.LoginByTokenAsync(token));
        Assert.Equal(AuthErrorCode.InvalidToken, stale.Code);

        await share.RevokeTokenAsync(owner.Id);

        var revoked = await Assert.ThrowsAsync<AuthException>(() => Auth.LoginByTokenAsync(regenerated));
        Assert.Equal(AuthErrorCode.InvalidToken, revoked.Code);

        var stillStale = await Assert.ThrowsAsync<AuthException>(() => Auth.LoginByTokenAsync(token));
        Assert.Equal(AuthErrorCode.InvalidToken, stillStale.Code);
    }
}
