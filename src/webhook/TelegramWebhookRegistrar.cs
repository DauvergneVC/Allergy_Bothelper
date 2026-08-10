using Telegram.Bot;
using Telegram.Bot.Types.Enums;

/// <summary>
/// <see cref="IWebhookRegistrar"/> backed by the Telegram Bot API. Routes registration
/// through the typed <c>SetWebhook</c> extension so the fake client can observe it.
/// </summary>
public sealed class TelegramWebhookRegistrar : IWebhookRegistrar
{
    private readonly ITelegramBotClient _client;

    public TelegramWebhookRegistrar(ITelegramBotClient client)
    {
        _client = client;
    }

    public Task SetWebhookAsync(string url, string secretToken, CancellationToken ct)
    {
        return _client.SetWebhook(
            url,
            secretToken: secretToken,
            allowedUpdates: new[] { UpdateType.Message, UpdateType.CallbackQuery },
            cancellationToken: ct);
    }
}
