using Allergy_BotHelper.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Allergy_BotHelper.Tests;

public class WebhookDispatcherTests
{
    private const long ChatId = 42;

    private static WebhookDispatcher Create(out FakeTelegramBotClient client, out RecordingBotHandler handler, BotReply? reply = null)
    {
        client = new FakeTelegramBotClient();
        handler = new RecordingBotHandler { Reply = reply };
        return new WebhookDispatcher(client, handler);
    }

    private static Update MessageUpdate(string? text) => new()
    {
        Id = 1,
        Message = new Message { Id = 1, Chat = new Chat { Id = ChatId }, Text = text }
    };

    private static Update CallbackUpdate(string data) => new()
    {
        Id = 2,
        CallbackQuery = new CallbackQuery
        {
            Id = "cb-1",
            Data = data,
            Message = new Message { Id = 1, Chat = new Chat { Id = ChatId } }
        }
    };

    [Fact]
    public async Task Message_ForwardsTextToHandler()
    {
        var dispatcher = Create(out _, out var handler);

        await dispatcher.DispatchAsync(MessageUpdate("/start"), CancellationToken.None);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(ChatId, call.ChatId);
        Assert.Equal("/start", call.Text);
        Assert.Null(call.Callback);
    }

    [Fact]
    public async Task Callback_AnswersCallback_ThenForwardsData()
    {
        var dispatcher = Create(out var client, out var handler);

        await dispatcher.DispatchAsync(CallbackUpdate("login"), CancellationToken.None);

        Assert.Equal("cb-1", Assert.Single(client.AnsweredCallbacks).CallbackQueryId);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("login", call.Callback);
        Assert.Null(call.Text);
    }

    [Fact]
    public async Task NullReply_SendsNothing()
    {
        var dispatcher = Create(out var client, out _);

        await dispatcher.DispatchAsync(MessageUpdate("/bogus"), CancellationToken.None);

        Assert.Empty(client.SentMessages);
        Assert.Empty(client.AnsweredCallbacks);
    }

    [Fact]
    public async Task Reply_SendsOneMessageWithText()
    {
        var dispatcher = Create(out var client, out _, new BotReply("hola"));

        await dispatcher.DispatchAsync(MessageUpdate("/start"), CancellationToken.None);

        var message = Assert.Single(client.SentMessages);
        Assert.Equal(ChatId, message.ChatId.Identifier);
        Assert.Equal("hola", message.Text);
        Assert.Null(message.ReplyMarkup);
    }

    [Fact]
    public async Task ReplyWithButtons_BuildsInlineKeyboardMarkup()
    {
        var reply = new BotReply("menu", new List<BotButton>
        {
            new BotButton("Login", "login"),
            new BotButton("Register", "register")
        });
        var dispatcher = Create(out var client, out _, reply);

        await dispatcher.DispatchAsync(MessageUpdate("/start"), CancellationToken.None);

        var message = Assert.Single(client.SentMessages);
        var markup = Assert.IsType<InlineKeyboardMarkup>(message.ReplyMarkup);
        var rows = markup.InlineKeyboard.ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("login", rows[0].Single().CallbackData);
        Assert.Equal("Register", rows[1].Single().Text);
    }

    [Fact]
    public async Task UpdateWithoutMessageOrCallback_IsNoOp()
    {
        var dispatcher = Create(out var client, out var handler);

        await dispatcher.DispatchAsync(new Update { Id = 3 }, CancellationToken.None);

        Assert.Empty(handler.Calls);
        Assert.Empty(client.SentMessages);
        Assert.Empty(client.AnsweredCallbacks);
    }
}
