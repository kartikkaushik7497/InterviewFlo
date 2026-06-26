using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Configuration;
using MockUpAi.Infrastructure.Persistence;
using MockUpAi.Infrastructure.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MockUpAi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMockUpAiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbSettings>().Bind(configuration.GetSection("MongoDb"));
        services.AddOptions<OpenAiSettings>().Bind(configuration.GetSection("OpenAi"));
        services.AddOptions<GeminiSettings>().Bind(configuration.GetSection("Gemini"));
        services.AddOptions<AzureSpeechSettings>().Bind(configuration.GetSection("AzureSpeech"));

        services.AddHttpClient();

        var mongoSettings = configuration.GetSection("MongoDb").Get<MongoDbSettings>() ?? new MongoDbSettings();
        var hasMongoConfig = !string.IsNullOrWhiteSpace(mongoSettings.ConnectionString) &&
                             !string.IsNullOrWhiteSpace(mongoSettings.DatabaseName);

        if (hasMongoConfig)
        {
            try
            {
                var clientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);
                clientSettings.RetryReads = true;
                clientSettings.RetryWrites = true;
                clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
                clientSettings.ConnectTimeout = TimeSpan.FromSeconds(2);

                var client = new MongoClient(clientSettings);
                var database = client.GetDatabase(mongoSettings.DatabaseName);

                using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                database.RunCommand<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: pingCts.Token);

                services.AddSingleton(database);
                services.AddSingleton(mongoSettings);
                services.AddSingleton<IUserRepository, MongoUserRepository>();
                services.AddSingleton<IInterviewRepository, MongoInterviewRepository>();
                services.AddSingleton<IStorageStatusService>(_ => new StorageStatusService(
                    "MongoDB",
                    isPersistent: true,
                    "Connected to MongoDB. Candidate and interview data will persist."));
            }
            catch
            {
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();
                services.AddSingleton<IInterviewRepository, InMemoryInterviewRepository>();
                services.AddSingleton<IStorageStatusService>(_ => new StorageStatusService(
                    "Temporary",
                    isPersistent: false,
                    "MongoDB is unreachable. Running with temporary in-memory storage; data will be lost when the app closes."));
            }
        }
        else
        {
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IInterviewRepository, InMemoryInterviewRepository>();
            services.AddSingleton<IStorageStatusService>(_ => new StorageStatusService(
                "Temporary",
                isPersistent: false,
                "No MongoDB connection string configured. Running with temporary in-memory storage."));
        }

        services.AddSingleton<IPasswordPolicyService, PasswordPolicyService>();
        services.AddSingleton<ISecretVaultService, SecretVaultService>();
        services.AddSingleton<IReportExportService, ReportExportService>();
        var openAiSettings = configuration.GetSection("OpenAi").Get<OpenAiSettings>() ?? new OpenAiSettings();
        var azureSpeechSettings = configuration.GetSection("AzureSpeech").Get<AzureSpeechSettings>() ?? new AzureSpeechSettings();
        var openAiEnabled = openAiSettings.Enabled && !string.IsNullOrWhiteSpace(openAiSettings.ApiKey);
        var azureSpeechEnabled = azureSpeechSettings.Enabled &&
                                 !string.IsNullOrWhiteSpace(azureSpeechSettings.Region) &&
                                 (!string.IsNullOrWhiteSpace(azureSpeechSettings.ApiKey) ||
                                  !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")));

        services.AddSingleton<HeuristicInterviewAiService>();
        services.AddSingleton<OpenAiInterviewAiService>();
        services.AddSingleton<GeminiInterviewAiService>();
        services.AddSingleton<IInterviewAiService, InterviewAiRouterService>();
        services.AddSingleton<HeuristicInterviewTurnOrchestrator>();
        services.AddSingleton<OpenAiInterviewTurnOrchestrator>();
        services.AddSingleton<GeminiInterviewTurnOrchestrator>();
        services.AddSingleton<IInterviewTurnOrchestrator, InterviewTurnOrchestratorRouter>();

        if (azureSpeechEnabled)
        {
            services.AddSingleton<ITranscriptionService, AzureSpeechTranscriptionService>();
        }
        else if (openAiEnabled)
        {
            services.AddSingleton<ITranscriptionService, OpenAiTranscriptionService>();
        }
        else
        {
            services.AddSingleton<ITranscriptionService, NoOpTranscriptionService>();
        }

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IAdminService, AdminService>();
        services.AddSingleton<IInterviewWorkflowService, InterviewWorkflowService>();

        services.AddSingleton<IStartupSeeder, StartupSeeder>();

        return services;
    }
}
