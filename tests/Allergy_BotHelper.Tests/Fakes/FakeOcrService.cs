using System.Threading.Tasks;

namespace Allergy_BotHelper.Tests.Fakes;

/// <summary>
/// Scripted <see cref="IOcrService"/> for handler tests: returns a configured text,
/// records the number of calls and the last image passed in, and can throw a typed
/// failure on demand.
/// </summary>
public sealed class FakeOcrService : IOcrService
{
    public string? Text { get; set; }
    public bool ThrowOcrFailure { get; set; }
    public int Calls { get; private set; }
    public byte[]? LastImage { get; private set; }

    public Task<string?> RecognizeAsync(byte[] image, CancellationToken ct)
    {
        Calls++;
        LastImage = image;

        if (ThrowOcrFailure)
        {
            throw new OcrFailureException("canned failure");
        }

        return Task.FromResult(Text);
    }
}
