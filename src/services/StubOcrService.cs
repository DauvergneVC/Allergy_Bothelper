/// <summary>
/// OCR-2: deterministic, credential-free OCR stub. Returns a fixed canned text
/// (injectable through the constructor, defaulting to <see cref="DefaultCannedText"/>),
/// identical for identical input, with no network or credential use.
/// </summary>
public sealed class StubOcrService : IOcrService
{
    public const string DefaultCannedText = "maní, leche";

    private readonly string _cannedText;

    public StubOcrService(string? cannedText = null)
    {
        _cannedText = cannedText ?? DefaultCannedText;
    }

    public Task<string?> RecognizeAsync(byte[] image, CancellationToken ct)
        => Task.FromResult<string?>(_cannedText);
}
