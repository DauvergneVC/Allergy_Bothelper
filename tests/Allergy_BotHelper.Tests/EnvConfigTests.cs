namespace Allergy_BotHelper.Tests;

public class EnvConfigTests
{
    private static Dictionary<string, string?> ValidEnv()
    {
        return new Dictionary<string, string?>
        {
            ["TELEGRAM_API_KEY"] = "telegram-key",
            ["MONGO_URI"] = "mongodb://localhost:27017/test",
            ["MONGO_INITDB_DATABASE"] = "test-db",
            ["WEBHOOK_URL"] = "https://example.com/webhook",
            ["WEBHOOK_SECRET_TOKEN"] = "secret"
        };
    }

    [Fact]
    public void Resolve_AllRequiredPresent_ReturnsConfig()
    {
        var config = EnvConfig.Resolve(ValidEnv());

        Assert.Equal("telegram-key", config.TelegramApiKey);
        Assert.Equal("mongodb://localhost:27017/test", config.MongoUri);
        Assert.Equal("test-db", config.MongoDatabase);
        Assert.Equal("https://example.com/webhook", config.WebhookUrl);
        Assert.Equal("secret", config.WebhookSecretToken);
        Assert.Equal(EnvConfig.DefaultPort, config.Port);
    }

    [Fact]
    public void Resolve_MissingVariable_ThrowsNamingIt()
    {
        var env = ValidEnv();
        env.Remove("WEBHOOK_URL");

        var ex = Assert.Throws<InvalidOperationException>(() => EnvConfig.Resolve(env));

        Assert.Contains("WEBHOOK_URL", ex.Message);
    }

    [Fact]
    public void Resolve_MissingMultipleVariables_NamesThemAll()
    {
        var env = ValidEnv();
        env.Remove("MONGO_URI");
        env.Remove("WEBHOOK_SECRET_TOKEN");

        var ex = Assert.Throws<InvalidOperationException>(() => EnvConfig.Resolve(env));

        Assert.Contains("MONGO_URI", ex.Message);
        Assert.Contains("WEBHOOK_SECRET_TOKEN", ex.Message);
    }

    [Fact]
    public void Resolve_PortProvided_IsUsed()
    {
        var env = ValidEnv();
        env["PORT"] = "9090";

        Assert.Equal(9090, EnvConfig.Resolve(env).Port);
    }

    [Fact]
    public void Resolve_PortBlank_FallsBackToDefault()
    {
        var env = ValidEnv();
        env["PORT"] = "";

        Assert.Equal(EnvConfig.DefaultPort, EnvConfig.Resolve(env).Port);
    }

    [Fact]
    public void Resolve_PortNotAnInteger_Throws()
    {
        var env = ValidEnv();
        env["PORT"] = "not-a-port";

        var ex = Assert.Throws<InvalidOperationException>(() => EnvConfig.Resolve(env));

        Assert.Contains("PORT", ex.Message);
    }

    [Fact]
    public void ProcessEnv_WinsOverDotEnvFile_AndFileFillsGaps()
    {
        var processWins = $"PROC_ENV_TEST_{Guid.NewGuid():N}";
        var fileOnly = $"FILE_ONLY_{Guid.NewGuid():N}";
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var envPath = Path.Combine(tempDir, ".env");
            File.WriteAllLines(envPath, new[]
            {
                $"{processWins}=from-file",
                $"{fileOnly}=from-file"
            });

            Environment.SetEnvironmentVariable(processWins, "from-process");

            DotNetEnv.Env.Load(envPath, EnvConfig.NoClobberLoad);

            Assert.Equal("from-process", Environment.GetEnvironmentVariable(processWins));
            Assert.Equal("from-file", Environment.GetEnvironmentVariable(fileOnly));
        }
        finally
        {
            Environment.SetEnvironmentVariable(processWins, null);
            Environment.SetEnvironmentVariable(fileOnly, null);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
