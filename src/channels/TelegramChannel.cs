using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramChannel
{
    private readonly TelegramBotClient _bot;
    private readonly IBotAuthHandler _handler;

    public TelegramChannel(string telegramApiKey, CancellationToken cancellationToken, IBotAuthHandler handler)
    {
        _handler = handler;
        _bot = new TelegramBotClient(telegramApiKey, cancellationToken: cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await _bot.GetMe();

        _bot.OnError += OnError;
        _bot.OnMessage += OnMessage;
        _bot.OnUpdate += OnUpdate;

        Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
        Console.ReadLine();
        cancellationToken.ThrowIfCancellationRequested(); // stop the bot
    }

    // method to handle errors in polling or in your OnMessage/OnUpdate code
    private async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception); // just dump the exception to the console
    }

    // method that handle messages received by the bot:
    private async Task OnMessage(Message msg, UpdateType type)
    {
        var reply = await _handler.HandleAsync(msg.Chat.Id, msg.Text, null, CancellationToken.None).ConfigureAwait(false);
        if (reply is not null)
        {
            await _bot.SendMessage(msg.Chat, reply.Text, replyMarkup: BuildMarkup(reply.Buttons)).ConfigureAwait(false);
        }
    }

    private async Task OnUpdate(Update update)
    {
        if (update is { CallbackQuery: { } query }) // non-null CallbackQuery
        {
            await _bot.AnswerCallbackQuery(query.Id).ConfigureAwait(false); // required by the Telegram API
            var reply = await _handler.HandleAsync(query.Message!.Chat.Id, null, query.Data, CancellationToken.None).ConfigureAwait(false);
            if (reply is not null)
            {
                await _bot.SendMessage(query.Message.Chat, reply.Text, replyMarkup: BuildMarkup(reply.Buttons)).ConfigureAwait(false);
            }
        }
    }

    private static InlineKeyboardMarkup? BuildMarkup(IReadOnlyList<BotButton>? buttons)
    {
        if (buttons is null || buttons.Count == 0)
        {
            return null;
        }

        var rows = buttons.Select(b => new[] { InlineKeyboardButton.WithCallbackData(b.Text, b.CallbackValue) });
        return new InlineKeyboardMarkup(rows);
    }
}
