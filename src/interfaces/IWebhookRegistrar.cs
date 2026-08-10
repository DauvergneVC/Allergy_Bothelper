/// <summary>
/// Registers the bot's webhook endpoint with Telegram. Implementations abstract the
/// Telegram API call so tests can record or fail the registration without a network.
/// </summary>
public interface IWebhookRegistrar
{
    Task SetWebhookAsync(string url, string secretToken, CancellationToken ct);
}
