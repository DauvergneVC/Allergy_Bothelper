using Xunit;

namespace Allergy_BotHelper.Tests.Integration;

/// <summary>
/// Fact that skips at discovery time unless the RUN_GCP_TESTS environment variable is set to "1".
/// Use this for tests that require real Google Cloud credentials (e.g., Google Vision OCR).
/// Most tests should use StubOcrService instead; this is for integration tests with real GCP.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GcpFactAttribute : FactAttribute
{
    public GcpFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_GCP_TESTS") != "1")
        {
            Skip = "RUN_GCP_TESTS is not set to 1; skipping GCP integration tests.";
        }
    }
}
