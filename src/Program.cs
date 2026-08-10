
namespace Allergy_BotHelper.src
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load environment variables from .env file and retrieve API keys
            DotNetEnv.Env.Load();
            string? telegramApiKey = Environment.GetEnvironmentVariable("TELEGRAM_API_KEY");
            string? whatsappApiKey = Environment.GetEnvironmentVariable("WHATSAPP_API_KEY");

            // MongoDB
            MongoDbContext mongoDbContext;
            try
            {
                string mongoUri = Environment.GetEnvironmentVariable("MONGO_URI") ??
                    throw new InvalidOperationException("MONGO_URI is not set in the environment variables.");
                string mongoDatabaseName = Environment.GetEnvironmentVariable("MONGO_INITDB_DATABASE") ??
                    throw new InvalidOperationException("MONGO_INITDB_DATABASE is not set in the environment variables.");

                mongoDbContext = new MongoDbContext(mongoUri, mongoDatabaseName);
                await mongoDbContext.PingAsync();
                // Ensure indexes are created
                await mongoDbContext.EnsureIndexesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to MongoDB: {ex.Message}");
                return; // Exit the application if the database connection fails
            }

            // Initialize services with DB context loaded
            UserRepository userRepository = new(mongoDbContext);
            AuthService authService = new(userRepository);
            AllergyService allergyService = new(userRepository);
            ShareService shareService = new(userRepository);
            var botHandler = new BotAuthHandler(authService, shareService);

            // Initialize Bot.
            if (string.IsNullOrEmpty(telegramApiKey) && string.IsNullOrEmpty(whatsappApiKey))
            {
                Console.WriteLine("No API keys has been set. One or more API keys are not set in the environment variables.");
                return;
            }

            if (!string.IsNullOrEmpty(whatsappApiKey))
            {
                Console.WriteLine("WhatsApp API key is set. WhatsApp bot functionality will be enabled.");
                Console.WriteLine("NOT IMPLEMENTED YET");
            }
            if (!string.IsNullOrEmpty(telegramApiKey))
            {
                Console.WriteLine("Telegram API key is set. Telegram bot functionality will be enabled.");
                await new TelegramChannel(telegramApiKey!, CancellationToken.None, botHandler).StartAsync(CancellationToken.None);

            }


        }
    }
}
