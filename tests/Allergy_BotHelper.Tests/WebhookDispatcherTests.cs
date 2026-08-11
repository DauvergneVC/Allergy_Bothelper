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

    private static PhotoSize Photo(string fileId, long fileSize) => new() { FileId = fileId, FileSize = fileSize };

    private static Update PhotoUpdate(PhotoSize[] photos, string? caption = null) => new()
    {
        Id = 3,
        Message = new Message { Id = 3, Chat = new Chat { Id = ChatId }, Text = caption, Photo = photos }
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
        Assert.Null(call.Photo);
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
        Assert.Null(call.Photo);
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

    [Fact]
    public async Task Photo_LargestCompliantSize_IsDownloadedAndPassedToHandler()
    {
        var dispatcher = Create(out var client, out var handler, new BotReply("ok"));
        client.DownloadBytes = new byte[] { 1, 2, 3 };

        await dispatcher.DispatchAsync(
            PhotoUpdate(new[] { Photo("f1", 1000), Photo("f2", 2000) }),
            CancellationToken.None);

        Assert.Equal("f2", Assert.Single(client.RequestedFileIds));
        var call = Assert.Single(handler.Calls);
        Assert.Equal(new byte[] { 1, 2, 3 }, call.Photo);
        Assert.Equal("ok", Assert.Single(client.SentMessages).Text);
    }

    [Fact]
    public async Task Photo_MixedSizes_PicksLargestCompliantOverOversize()
    {
        var dispatcher = Create(out var client, out var handler);
        client.DownloadBytes = new byte[] { 9 };

        await dispatcher.DispatchAsync(
            PhotoUpdate(new[]
            {
                Photo("small", 1000),
                Photo("oversize", WebhookDispatcher.MaxPhotoBytes + 1),
                Photo("medium", 2000)
            }),
            CancellationToken.None);

        Assert.Equal("medium", Assert.Single(client.RequestedFileIds));
        Assert.Equal(new byte[] { 9 }, Assert.Single(handler.Calls).Photo);
    }

    [Fact]
    public async Task Photo_AllSizesOversize_EmptyBytesPassed_NoDownloadAttempted()
    {
        var dispatcher = Create(out var client, out var handler);

        await dispatcher.DispatchAsync(
            PhotoUpdate(new[] { Photo("f1", WebhookDispatcher.MaxPhotoBytes + 1) }),
            CancellationToken.None);

        Assert.Empty(client.RequestedFileIds);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(Array.Empty<byte>(), call.Photo);
    }

    [Fact]
    public async Task Photo_DownloadFailure_NullBytesPassed_HandlerStillInvoked()
    {
        var dispatcher = Create(out var client, out var handler);
        client.FailDownload = true;

        await dispatcher.DispatchAsync(PhotoUpdate(new[] { Photo("f1", 1000) }), CancellationToken.None);

        var call = Assert.Single(handler.Calls);
        Assert.Null(call.Photo);
        Assert.Empty(client.SentMessages);
    }
}
