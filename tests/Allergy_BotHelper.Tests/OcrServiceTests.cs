using Google.Cloud.Vision.V1;

namespace Allergy_BotHelper.Tests;

public class OcrServiceTests
{
    [Fact]
    public void Stub_ReturnsConfiguredCannedText()
    {
        var service = new StubOcrService("maní, leche");

        var result = service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None).Result;

        Assert.Equal("maní, leche", result);
    }

    [Fact]
    public void Stub_DefaultCannedText_IsFixedDemoText()
    {
        var service = new StubOcrService();

        var result = service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None).Result;

        Assert.Equal(StubOcrService.DefaultCannedText, result);
    }

    [Fact]
    public void Stub_ConfiguredEmpty_ReturnsEmptyText()
    {
        var service = new StubOcrService(string.Empty);

        var result = service.RecognizeAsync(new byte[] { 1 }, CancellationToken.None).Result;

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Stub_IsDeterministicAndCredentialFree()
    {
        var service = new StubOcrService("peanut");
        var bytes = new byte[] { 1, 2, 3 };

        var first = service.RecognizeAsync(bytes, CancellationToken.None).Result;
        var second = service.RecognizeAsync(bytes, CancellationToken.None).Result;

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
