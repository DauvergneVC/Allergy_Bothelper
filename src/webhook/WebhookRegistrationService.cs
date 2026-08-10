using Microsoft.Extensions.Hosting;

/// <summary>
/// Registers the webhook when the host starts. Registration is idempotent (re-setting
/// the same URL/secret is a no-op server-side) and non-fatal: a network failure at
/// startup logs and lets the host continue, matching Telegram's long-poll fallback
/// guarantees.
/// </summary>
public sealed class WebhookRegistrationService : IHostedService
{
    private readonly IWebhookRegistrar _registrar;
    private readonly string _webhookUrl;
    private readonly string _secretToken;

    public WebhookRegistrationService(IWebhookRegistrar registrar, string webhookUrl, string secretToken)
    {
        _registrar = registrar;
        _webhookUrl = webhookUrl;
        _secretToken = secretToken;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _registrar.SetWebhookAsync(_webhookUrl, _secretToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register Telegram webhook: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
