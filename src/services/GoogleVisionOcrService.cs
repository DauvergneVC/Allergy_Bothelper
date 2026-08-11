using Google.Api.Gax.Grpc;
using Google.Cloud.Vision.V1;

/// <summary>
/// OCR-3: Google Cloud Vision implementation of <see cref="IOcrService"/>, using
/// <c>DetectDocumentTextAsync</c> with Application Default Credentials (no API key).
///
/// The <see cref="ImageAnnotatorClient"/> is created lazily on first use (never at
/// construction) so stub-mode runs never need GCP credentials (OCR-4). Any Vision,
/// network or auth error is wrapped in a typed <see cref="OcrFailureException"/>
/// (OCR-5). A <c>null</c> document text is returned as a normal "no text" outcome.
/// </summary>
public sealed class GoogleVisionOcrService : IOcrService
{
    private readonly Func<CancellationToken, Task<ImageAnnotatorClient>> _clientFactory;
    private Task<ImageAnnotatorClient>? _clientTask;

    public GoogleVisionOcrService(Func<CancellationToken, Task<ImageAnnotatorClient>>? clientFactory = null)
    {
        _clientFactory = clientFactory ?? (ct => ImageAnnotatorClient.CreateAsync(ct));
    }

    public async Task<string?> RecognizeAsync(byte[] image, CancellationToken ct)
    {
        try
        {
            var client = await GetClientAsync(ct).ConfigureAwait(false);
            var annotation = await client.DetectDocumentTextAsync(
                Image.FromBytes(image),
                null,
                CallSettings.FromCancellationToken(ct)).ConfigureAwait(false);
            return annotation?.Text;
        }
        catch (OcrFailureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OcrFailureException("Google Vision OCR failed.", ex);
        }
    }

    private async Task<ImageAnnotatorClient> GetClientAsync(CancellationToken ct)
    {
        if (_clientTask is null)
        {
            _clientTask = _clientFactory(ct);
        }

        return await _clientTask.ConfigureAwait(false);
    }
}
