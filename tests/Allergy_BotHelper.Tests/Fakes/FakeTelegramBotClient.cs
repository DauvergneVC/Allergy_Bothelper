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

    /// <summary>File ids for which <see cref="GetFileRequest"/> was issued, in order.</summary>
    public List<string> RequestedFileIds { get; } = new();

    /// <summary>Bytes written by <see cref="DownloadFile(TGFile, Stream, CancellationToken)"/>.</summary>
    public byte[] DownloadBytes { get; set; } = Array.Empty<byte>();

    /// <summary>When true, both <c>DownloadFile</c> overloads fail (simulated network error).</summary>
    public bool FailDownload { get; set; }

    /// <summary>Optional canned file info returned for a <see cref="GetFileRequest"/>.</summary>
    public TGFile? GetFileResult { get; set; }

    public bool LocalBotServer => false;
    public long BotId => 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest { add { } remove { } }
    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived { add { } remove { } }

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is GetFileRequest fileRequest)
        {
            RequestedFileIds.Add(fileRequest.FileId);
            Requests.Add(request);
            var file = GetFileResult ?? new TGFile { FileId = fileRequest.FileId, FilePath = "photo.jpg" };
            return Task.FromResult<TResponse>((TResponse)(object)file);
        }

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

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
        => WriteCannedBytes(destination);

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
        => WriteCannedBytes(destination);

    private Task WriteCannedBytes(Stream destination)
    {
        if (FailDownload)
        {
            return Task.FromException(new InvalidOperationException("download failed"));
        }

        destination.Write(DownloadBytes);
        return Task.CompletedTask;
    }
}
