using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace Allergy_BotHelper.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ITelegramBotClient"/> that records the typed requests flowing
/// through <see cref="SendRequest{TResponse}"/> — the single channel every typed
/// extension (SendMessage, AnswerCallbackQuery, SetWebhook, ...) uses.
/// </summary>
public sealed class FakeTelegramBotClient : ITelegramBotClient
{
    public List<object> Requests { get; } = new();
    public List<SendMessageRequest> SentMessages { get; } = new();
    public List<AnswerCallbackQueryRequest> AnsweredCallbacks { get; } = new();
    public List<SetWebhookRequest> WebhookRegistrations { get; } = new();

    public bool LocalBotServer => false;
    public long BotId => 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest { add { } remove { } }
    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived { add { } remove { } }

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        switch (request)
        {
            case SendMessageRequest messageRequest: SentMessages.Add(messageRequest); break;
            case AnswerCallbackQueryRequest callbackRequest: AnsweredCallbacks.Add(callbackRequest); break;
            case SetWebhookRequest webhookRequest: WebhookRegistrations.Add(webhookRequest); break;
        }

        Requests.Add(request);
        return Task.FromResult<TResponse>(default!);
    }

    public Task<bool> TestApi(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
