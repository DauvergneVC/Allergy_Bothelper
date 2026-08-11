
using System.Collections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace Allergy_BotHelper.src
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load .env without clobbering: process environment wins, the file only fills gaps.
            DotNetEnv.Env.Load(".env", EnvConfig.NoClobberLoad);

            BotConfig config;
            try
            {
                config = EnvConfig.Resolve(ProcessEnv());
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            // MongoDB
            MongoDbContext mongoDbContext;
            try
            {
                mongoDbContext = new MongoDbContext(config.MongoUri, config.MongoDatabase);
                await mongoDbContext.PingAsync();
                // Ensure indexes are created
                await mongoDbContext.EnsureIndexesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to MongoDB: {ex.Message}");
                return; // Exit the application if the database connection fails
            }

            // Webhook host: one POST endpoint receives Telegram updates; the registrar
            // announces the URL to Telegram at startup. No long polling.
            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");

            builder.Services.AddSingleton(mongoDbContext);
            builder.Services.AddSingleton<IUserRepository, UserRepository>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<IAllergyService, AllergyService>();
            builder.Services.AddSingleton<IShareService, ShareService>();
            builder.Services.AddSingleton<ISessionStore, MongoSessionStore>();

            // OCR-4: OCR_MODE selects the IOcrService implementation. Google Vision is
            // lazy (no client at construction), so stub mode needs no GCP credentials.
            builder.Services.AddSingleton<IOcrService>(config.OcrMode == OcrMode.Google
                ? new GoogleVisionOcrService()
                : new StubOcrService());

            builder.Services.AddSingleton<BotAuthHandler>();
            builder.Services.AddSingleton<IBotAuthHandler>(sp =>
                new SessionAwareHandler(
                    sp.GetRequiredService<BotAuthHandler>(),
                    sp.GetRequiredService<ISessionStore>()));

            var client = new TelegramBotClient(config.TelegramApiKey);
            builder.Services.AddSingleton<ITelegramBotClient>(client);
            builder.Services.AddSingleton<IWebhookRegistrar>(new TelegramWebhookRegistrar(client));
            builder.Services.AddSingleton<WebhookDispatcher>();
            builder.Services.AddSingleton(sp => new WebhookRequestHandler(
                sp.GetRequiredService<WebhookDispatcher>(),
                config.WebhookSecretToken));
            builder.Services.AddSingleton(sp => new WebhookRegistrationService(
                sp.GetRequiredService<IWebhookRegistrar>(),
                config.WebhookUrl,
                config.WebhookSecretToken));
            builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookRegistrationService>());

            var app = builder.Build();

            app.MapPost("/webhook", async (HttpContext http, WebhookRequestHandler handler) =>
            {
                var secret = http.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
                http.Response.StatusCode = await handler.ProcessAsync(http.Request.Body, secret, http.RequestAborted);
            });

            app.MapGet("/healthz", () => Results.Ok("ok"));

            await app.RunAsync();
        }

        private static Dictionary<string, string?> ProcessEnv()
        {
            var env = new Dictionary<string, string?>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                env[(string)entry.Key] = entry.Value?.ToString();
            }

            return env;
        }
    }
}
