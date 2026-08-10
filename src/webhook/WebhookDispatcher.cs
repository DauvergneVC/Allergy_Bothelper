using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Turns a Telegram <see cref="Update"/> into handler calls and outbound replies.
/// Message updates feed text into the handler; callback queries are acknowledged
/// first (required by the API) and then feed the callback data. A non-null reply
/// produces exactly one outbound message.
/// </summary>
public sealed class WebhookDispatcher
{
    private readonly ITelegramBotClient _client;
    private readonly IBotAuthHandler _handler;

    public WebhookDispatcher(ITelegramBotClient client, IBotAuthHandler handler)
    {
        _client = client;
        _handler = handler;
    }

    public async Task DispatchAsync(Update update, CancellationToken ct)
    {
        BotReply? reply = null;
        long? chatId = null;

        if (update.CallbackQuery is { } query)
        {
            chatId = query.Message!.Chat.Id;
            await _client.AnswerCallbackQuery(query.Id, cancellationToken: ct).ConfigureAwait(false);
            reply = await _handler.HandleAsync(chatId.Value, new ChatSession(), null, query.Data, ct).ConfigureAwait(false);
        }
        else if (update.Message is { } message)
        {
            chatId = message.Chat.Id;
            reply = await _handler.HandleAsync(chatId.Value, new ChatSession(), message.Text, null, ct).ConfigureAwait(false);
        }

        if (reply is not null && chatId is not null)
        {
            await _client.SendMessage(chatId.Value, reply.Text, replyMarkup: TelegramMarkup.Build(reply.Buttons), cancellationToken: ct).ConfigureAwait(false);
        }
    }
}
