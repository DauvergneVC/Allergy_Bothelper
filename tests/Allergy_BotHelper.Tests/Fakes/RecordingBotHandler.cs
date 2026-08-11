using System.Threading.Tasks;

namespace Allergy_BotHelper.Tests.Fakes;

/// <summary>
/// Records handler invocations and returns a scripted reply (null by default).
/// </summary>
public sealed class RecordingBotHandler : IBotAuthHandler
{
    public List<(long ChatId, string? Text, string? Callback, byte[]? Photo)> Calls { get; } = new();
    public BotReply? Reply { get; set; }

    public Task<BotReply?> HandleAsync(long chatId, ChatSession session, string? text, string? callbackData, CancellationToken ct, byte[]? photoBytes = null)
    {
        Calls.Add((chatId, text, callbackData, photoBytes));
        return Task.FromResult(Reply);
    }
}
