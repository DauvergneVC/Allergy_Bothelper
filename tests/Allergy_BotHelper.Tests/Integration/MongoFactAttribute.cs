using Xunit;

namespace Allergy_BotHelper.Tests.Integration;

/// <summary>
/// Fact that skips at discovery time (reported as Skipped, never run) unless the
/// RUN_MONGO_TESTS environment variable is set to "1". This is the xUnit v2 way to
/// gate tests on an external resource without failing when it is unavailable.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_MONGO_TESTS") != "1")
        {
            Skip = "RUN_MONGO_TESTS is not set to 1; skipping MongoDB integration tests.";
        }
    }
}
