using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


// Load environment variables from .env file
DotNetEnv.Env.Load();
string telegramApiKey = Environment.GetEnvironmentVariable("TELEGRAM_API_KEY") ?? throw new InvalidOperationException("TELEGRAM_API_KEY is not set in the environment variables.");

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(telegramApiKey, cancellationToken: cts.Token);
var me = await bot.GetMe();
bot.OnError += OnError;
bot.OnMessage += OnMessage;
bot.OnUpdate += OnUpdate;

Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
Console.ReadLine();
cts.Cancel(); // stop the bot

// method to handle errors in polling or in your OnMessage/OnUpdate code
async Task OnError(Exception exception, HandleErrorSource source)
{
    Console.WriteLine(exception); // just dump the exception to the console
}

// method that handle messages received by the bot:
async Task OnMessage(Message msg, UpdateType type)
{
    if (msg.Text == "/start")
    {
        await bot.SendMessage(msg.Chat, "Welcome! Please sign in to your account or create a new one.",
        replyMarkup: new InlineKeyboardButton[] { "Sign In", "Create Account" });
    }
}

// method that handle other types of updates received by the bot:
async Task OnUpdate(Update update)
{
    if (update is { CallbackQuery: { } query }) // non-null CallbackQuery
    {
        await bot.AnswerCallbackQuery(query.Id, $"You picked {query.Data}"); // mesage that appears like a pop-up when the user clicks on a button
        if (query.Data == "Sign In")
        {
            await bot.SendMessage(query.Message!.Chat, "Please enter your email and password to sign in.");
        }
        else if (query.Data == "Create Account")
        {
            await bot.SendMessage(query.Message!.Chat, "Please enter your email, password, and allergies to create a new account.");
        }
        else
        {
            await bot.SendMessage(query.Message!.Chat, $"You clicked on {query.Data}");
        }
    }
}
