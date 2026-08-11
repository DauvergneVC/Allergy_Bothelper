public enum OcrMode
{
    Stub,
    Google
}

public sealed record BotConfig(
    string TelegramApiKey,
    string MongoUri,
    string MongoDatabase,
    string WebhookUrl,
    string WebhookSecretToken,
    int Port,
    OcrMode OcrMode);

/// <summary>
/// Pure environment resolution. <see cref="Resolve"/> reads a key-value view of the
/// merged environment (process wins, <c>.env</c> fills gaps) and fails fast naming any
/// missing required variable. <see cref="Port"/> is optional and defaults to
/// <see cref="DefaultPort"/>; <c>OCR_MODE</c> is optional and defaults to stub
/// (CONFIG-7).
/// </summary>
public static class EnvConfig
{
    private static readonly string[] RequiredVariables =
    {
        "TELEGRAM_API_KEY",
        "MONGO_URI",
        "MONGO_INITDB_DATABASE",
        "WEBHOOK_URL",
        "WEBHOOK_SECRET_TOKEN"
    };

    public const int DefaultPort = 8080;

    /// <summary>
    /// Load options that only set variables absent from the process environment, so a
    /// deployment-provided value always beats the checked-in <c>.env</c>.
    /// </summary>
    public static readonly DotNetEnv.LoadOptions NoClobberLoad = new(
        setEnvVars: true,
        clobberExistingVars: false,
        onlyExactPath: true);

    /// <summary>
    /// CONFIG-7: <c>OCR_MODE</c> is optional and defaults to stub. Only the value
    /// <c>google</c> (case-insensitive) selects the Google implementation; any unknown
    /// or blank value falls back to the stub default.
    /// </summary>
    public static OcrMode OcrModeFrom(string? value)
        => string.Equals(value, "google", StringComparison.OrdinalIgnoreCase)
            ? OcrMode.Google
            : OcrMode.Stub;

    public static BotConfig Resolve(IReadOnlyDictionary<string, string?> env)
    {
        var missing = RequiredVariables
            .Where(v => string.IsNullOrWhiteSpace(env.GetValueOrDefault(v)))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing required environment variable(s): {string.Join(", ", missing)}");
        }

        var port = DefaultPort;
        var portValue = env.GetValueOrDefault("PORT");
        if (!string.IsNullOrWhiteSpace(portValue) && !int.TryParse(portValue, out port))
        {
            throw new InvalidOperationException($"PORT must be an integer, got '{portValue}'.");
        }

        return new BotConfig(
            env["TELEGRAM_API_KEY"]!,
            env["MONGO_URI"]!,
            env["MONGO_INITDB_DATABASE"]!,
            env["WEBHOOK_URL"]!,
            env["WEBHOOK_SECRET_TOKEN"]!,
            port,
            OcrModeFrom(env.GetValueOrDefault("OCR_MODE")));
    }
}
