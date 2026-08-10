using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Processes an HTTP webhook request: constant-time secret-token verification, body
/// deserialization and dispatch. Returns the HTTP status to use:
/// 200 on success, 401 on a missing/wrong secret, 400 on malformed or null JSON.
/// </summary>
public sealed class WebhookRequestHandler
{
    private readonly WebhookDispatcher _dispatcher;
    private readonly byte[] _secret;

    public WebhookRequestHandler(WebhookDispatcher dispatcher, string secretToken)
    {
        _dispatcher = dispatcher;
        _secret = Encoding.UTF8.GetBytes(secretToken);
    }

    public async Task<int> ProcessAsync(Stream body, string? providedSecret, CancellationToken ct)
    {
        if (!FixedTimeEqualsUtf8(providedSecret, _secret))
        {
            return 401;
        }

        Update? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(body, JsonBotAPI.Options, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return 400;
        }

        if (update is null)
        {
            return 400;
        }

        await _dispatcher.DispatchAsync(update, ct).ConfigureAwait(false);
        return 200;
    }

    private static bool FixedTimeEqualsUtf8(string? provided, byte[] expected)
    {
        if (provided is null)
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return providedBytes.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expected);
    }
}
