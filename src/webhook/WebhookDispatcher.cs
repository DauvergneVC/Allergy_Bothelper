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
    /// <summary>OCR-6: maximum accepted photo size in bytes (20 MB).</summary>
    public const int MaxPhotoBytes = 20 * 1024 * 1024;

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
            if (query.Message is null)
            {
                // CallbackQuery without Message (e.g., inline keyboard from inline mode) — skip
                await _client.AnswerCallbackQuery(query.Id, cancellationToken: ct).ConfigureAwait(false);
                return;
            }

            chatId = query.Message.Chat.Id;
            await _client.AnswerCallbackQuery(query.Id, cancellationToken: ct).ConfigureAwait(false);
            reply = await _handler.HandleAsync(chatId.Value, new ChatSession(), null, query.Data, ct).ConfigureAwait(false);
        }
        else if (update.Message is { } message)
        {
            chatId = message.Chat.Id;
            var photoBytes = await DownloadPhotoBytesAsync(message, ct).ConfigureAwait(false);
            reply = await _handler.HandleAsync(chatId.Value, new ChatSession(), message.Text, null, ct, photoBytes).ConfigureAwait(false);
        }

        if (reply is not null && chatId is not null)
        {
            await _client.SendMessage(chatId.Value, reply.Text, replyMarkup: TelegramMarkup.Build(reply.Buttons), cancellationToken: ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// WEBHOOK-9 / OCR-6: downloads the largest PhotoSize at or under
    /// <see cref="MaxPhotoBytes"/> into memory. Tri-state result:
    /// <c>null</c> when there is no photo or the download failed (handler still runs),
    /// an empty array when the photo was present but rejected (oversize), and the
    /// non-empty bytes otherwise.
    /// </summary>
    private async Task<byte[]?> DownloadPhotoBytesAsync(Message message, CancellationToken ct)
    {
        if (message.Photo is not { Length: > 0 } photos)
        {
            return null;
        }

        var largestCompliant = photos
            .Where(p => p.FileSize is { } size && size <= MaxPhotoBytes)
            .OrderByDescending(p => p.FileSize)
            .FirstOrDefault();

        if (largestCompliant is null)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var file = await _client.GetFile(largestCompliant.FileId, ct).ConfigureAwait(false);
            using var stream = new MemoryStream();
            await _client.DownloadFile(file, stream, ct).ConfigureAwait(false);
            return stream.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
