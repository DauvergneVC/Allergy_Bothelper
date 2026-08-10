using Allergy_BotHelper.Tests.Fakes;

namespace Allergy_BotHelper.Tests;

public class WebhookRegistrationServiceTests
{
    [Fact]
    public async Task StartAsync_CallsSetWebhookWithUrlAndSecret()
    {
        var registrar = new RecordingWebhookRegistrar();
        var service = new WebhookRegistrationService(registrar, "https://example.com/webhook", "secret-1");

        await service.StartAsync(CancellationToken.None);

        var call = Assert.Single(registrar.Calls);
        Assert.Equal("https://example.com/webhook", call.Url);
        Assert.Equal("secret-1", call.Secret);
    }

    [Fact]
    public async Task StartAsync_RegistrarFailure_IsNonFatal()
    {
        var service = new WebhookRegistrationService(
            new ThrowingWebhookRegistrar(),
            "https://example.com/webhook",
            "secret-1");

        await service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TelegramWebhookRegistrar_RoutesThroughClient_SetWebhook()
    {
        var client = new FakeTelegramBotClient();
        var registrar = new TelegramWebhookRegistrar(client);

        await registrar.SetWebhookAsync("https://example.com/webhook", "secret-1", CancellationToken.None);

        var request = Assert.Single(client.WebhookRegistrations);
        Assert.Equal("https://example.com/webhook", request.Url);
        Assert.Equal("secret-1", request.SecretToken);
    }

    private sealed class RecordingWebhookRegistrar : IWebhookRegistrar
    {
        public List<(string Url, string Secret)> Calls { get; } = new();

        public Task SetWebhookAsync(string url, string secretToken, CancellationToken ct)
        {
            Calls.Add((url, secretToken));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingWebhookRegistrar : IWebhookRegistrar
    {
        public Task SetWebhookAsync(string url, string secretToken, CancellationToken ct)
            => throw new InvalidOperationException("network down");
    }
}
