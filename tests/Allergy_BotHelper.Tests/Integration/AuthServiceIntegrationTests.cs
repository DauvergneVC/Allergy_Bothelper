namespace Allergy_BotHelper.Tests.Integration;

public class AuthServiceIntegrationTests : IntegrationTestBase
{
    [MongoFact]
    public async Task Parallel_Register_SameEmail_YieldsExactlyOneSuccess()
    {
        const string email = "race@example.com";
        const string password = "race-password";
        const int attempts = 8;

        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(async _ =>
            {
                try
                {
                    await Auth.RegisterAsync(email, password);
                    return (Success: true, Error: null as AuthException);
                }
                catch (AuthException ex)
                {
                    return (Success: false, Error: ex);
                }
            }));

        var successes = outcomes.Count(o => o.Success);
        var failures = outcomes.Where(o => !o.Success).Select(o => o.Error!).ToList();

        Assert.Equal(1, successes);
        Assert.Equal(attempts - 1, failures.Count);
        Assert.All(failures, f => Assert.Equal(AuthErrorCode.DuplicateEmail, f.Code));
        Assert.All(failures, f => Assert.Equal("el email ya está registrado", f.Message));
    }
}
