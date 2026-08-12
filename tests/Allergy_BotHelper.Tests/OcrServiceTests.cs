using Google.Cloud.Vision.V1;

namespace Allergy_BotHelper.Tests;

/// <summary>
/// Tests for OCR services. Note: scenarios OCR-3 (document detection, ADC auth) and CONFIG-8
/// (SA key local path) are static-only by design — they require real GCP credentials and cannot
/// be tested in CI. Use GcpFactAttribute with RUN_GCP_TESTS=1 for integration tests with real GCP.
/// </summary>
public class OcrServiceTests
{
    [Fact]
    public async Task Stub_ReturnsConfiguredCannedText()
    {
        var service = new StubOcrService("maní, leche");

        var result = await service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal("maní, leche", result);
    }

    [Fact]
    public async Task Stub_DefaultCannedText_IsFixedDemoText()
    {
        var service = new StubOcrService();

        var result = await service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal(StubOcrService.DefaultCannedText, result);
    }

    [Fact]
    public async Task Stub_ConfiguredEmpty_ReturnsEmptyText()
    {
        var service = new StubOcrService(string.Empty);

        var result = await service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task Stub_IsDeterministicAndCredentialFree()
    {
        var service = new StubOcrService("peanut");
        var bytes = new byte[] { 1, 2, 3 };

        var first = await service.RecognizeAsync(bytes, CancellationToken.None);
        var second = await service.RecognizeAsync(bytes, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal("peanut", first);
    }

    [Fact]
    public void Google_Constructor_DoesNotCreateClient()
    {
        var factoryCalls = 0;
        var service = new GoogleVisionOcrService(_ =>
        {
            factoryCalls++;
            return Task.FromException<ImageAnnotatorClient>(new InvalidOperationException("no credentials"));
        });

        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task Google_FactoryFailure_WrapsAsOcrFailureException()
    {
        var service = new GoogleVisionOcrService(_ =>
            Task.FromException<ImageAnnotatorClient>(new InvalidOperationException("no credentials")));

        var ex = await Assert.ThrowsAsync<OcrFailureException>(
            () => service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task Google_ClientFactoryCalledOnce_AcrossRecognitions()
    {
        var factoryCalls = 0;
        var service = new GoogleVisionOcrService(ct =>
        {
            factoryCalls++;
            return Task.FromException<ImageAnnotatorClient>(new InvalidOperationException("no credentials"));
        });

        await Assert.ThrowsAsync<OcrFailureException>(
            () => service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None));
        await Assert.ThrowsAsync<OcrFailureException>(
            () => service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None));

        Assert.Equal(1, factoryCalls);
    }
}
