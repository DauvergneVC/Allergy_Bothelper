/// <summary>
/// OCR-1: text extraction from an ingredient photo. Returns the full extracted text
/// as a single string, or <c>null</c> when no text is detected. A normal "no text"
/// outcome must not throw; genuine failures surface as a typed
/// <see cref="OcrFailureException"/>.
/// </summary>
public interface IOcrService
{
    Task<string?> RecognizeAsync(byte[] image, CancellationToken ct);
}
