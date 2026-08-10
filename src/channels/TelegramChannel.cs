using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TelegramChannel
{
    private readonly string _telegramApiKey;
    private readonly TelegramBotClient _bot;

    public TelegramChannel(string telegramApiKey, CancellationToken cancellationToken)
    {
        _telegramApiKey = telegramApiKey;
        _bot = new TelegramBotClient(_telegramApiKey, cancellationToken: cancellationToken);
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
        if (msg.Text == "/start")
        {
            // Handle the /start command
            await _bot.SendMessage(msg.Chat, "Welcome! Please choose an option:",
            replyMarkup: new InlineKeyboardButton[] { "Sign In", "Create Account" });

        }
        else
        {
            // Handle other messages
            await _bot.SendMessage(msg.Chat, "I didn't understand that command.");
        }
    }

    async Task OnUpdate(Update update)
    {
        if (update is { CallbackQuery: { } query }) // non-null CallbackQuery
        {
            await _bot.AnswerCallbackQuery(query.Id, $"You picked {query.Data}"); // mesage that appears like a pop-up when the user clicks on a button
            if (query.Data == "Sign In")
            {
                await _bot.SendMessage(query.Message!.Chat, "Please enter your email and password to sign in.");
            }
            else if (query.Data == "Create Account")
            {
                await _bot.SendMessage(query.Message!.Chat, "Please enter your email, password, and allergies to create a new account.");
            }
            else
            {
                await _bot.SendMessage(query.Message!.Chat, $"You clicked on {query.Data}");
            }
        }
    }
}