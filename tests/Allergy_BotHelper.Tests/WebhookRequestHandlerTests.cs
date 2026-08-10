using System.Text;
using System.Text.Json;
using Allergy_BotHelper.Tests.Fakes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Allergy_BotHelper.Tests;

public class WebhookRequestHandlerTests
{
    private const long ChatId = 42;
    private const string Secret = "top-secret";

    private static WebhookRequestHandler Create(out FakeTelegramBotClient client, out RecordingBotHandler handler)
    {
        client = new FakeTelegramBotClient();
        handler = new RecordingBotHandler();
        return new WebhookRequestHandler(new WebhookDispatcher(client, handler), Secret);
    }

    private static Stream Body(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static string Serialize(Update update) => JsonSerializer.Serialize(update, JsonBotAPI.Options);

    private static Update MessageUpdate(string? text) => new()
    {
        Id = 1,
        Message = new Message { Id = 1, Chat = new Chat { Id = ChatId, Type = ChatType.Private }, Text = text }
    };

    [Fact]
    public async Task WrongSecret_Returns401_AndDoesNotDispatch()
    {
        var requestHandler = Create(out var client, out var handler);

        var status = await requestHandler.ProcessAsync(Body(Serialize(MessageUpdate("/start"))), "wrong-secret", CancellationToken.None);

        Assert.Equal(401, status);
        Assert.Empty(handler.Calls);
        Assert.Empty(client.SentMessages);
    }

    [Fact]
    public async Task MissingSecret_Returns401()
    {
        var requestHandler = Create(out _, out _);

        var status = await requestHandler.ProcessAsync(Body("{}"), null, CancellationToken.None);

        Assert.Equal(401, status);
    }

    [Fact]
    public async Task MalformedBody_Returns400()
    {
        var requestHandler = Create(out _, out _);

        var status = await requestHandler.ProcessAsync(Body("{ not json"), Secret, CancellationToken.None);

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task NullJson_Returns400()
    {
        var requestHandler = Create(out _, out _);

        var status = await requestHandler.ProcessAsync(Body("null"), Secret, CancellationToken.None);

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task ValidMessage_Returns200_AndSendsReply()
    {
        var requestHandler = Create(out var client, out var handler);
        handler.Reply = new BotReply("hola");

        var status = await requestHandler.ProcessAsync(Body(Serialize(MessageUpdate("/start"))), Secret, CancellationToken.None);

        Assert.Equal(200, status);
        Assert.Equal(ChatId, Assert.Single(handler.Calls).ChatId);
        Assert.Equal("hola", Assert.Single(client.SentMessages).Text);
    }

    [Fact]
    public async Task ValidCallback_Returns200_AnswersAndSendsReply()
    {
        var requestHandler = Create(out var client, out var handler);
        handler.Reply = new BotReply("menu");
        var update = new Update
        {
            Id = 2,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb-9",
                Data = "login",
                Message = new Message { Id = 1, Chat = new Chat { Id = ChatId, Type = ChatType.Private } }
            }
        };

        var status = await requestHandler.ProcessAsync(Body(Serialize(update)), Secret, CancellationToken.None);

        Assert.Equal(200, status);
        Assert.Equal("cb-9", Assert.Single(client.AnsweredCallbacks).CallbackQueryId);
        Assert.Equal("menu", Assert.Single(client.SentMessages).Text);
    }
}
