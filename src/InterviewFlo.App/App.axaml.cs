using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterviewFlo.App.Services;
using InterviewFlo.App.Services.Media;
using InterviewFlo.App.Services.Voice;
using InterviewFlo.App.ViewModels;
using InterviewFlo.App.ViewModels.Admin;
using InterviewFlo.App.ViewModels.Auth;
using InterviewFlo.App.ViewModels.Candidate;
using InterviewFlo.App.Views;
using InterviewFlo.Infrastructure;
using InterviewFlo.Infrastructure.Services;
using System.Runtime.Versioning;

namespace InterviewFlo.App;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowDecorations = WindowDecorations.Full,
                ExtendClientAreaToDecorationsHint = false,
                CanResize = true,
                CanMinimize = true,
                CanMaximize = true,
                ShowInTaskbar = true,
                WindowState = WindowState.Maximized,
            };

            mainWindow.Opened += (_, _) =>
            {
                if (mainWindow.WindowState != WindowState.Maximized)
                {
                    mainWindow.WindowState = WindowState.Maximized;
                }
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            _ = InitializeShellAsync(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeShellAsync(MainWindow mainWindow)
    {
        try
        {
            Services = BuildServices();
            var telemetry = Services.GetRequiredService<IAppTelemetryService>();
            ConfigureGlobalExceptionHandlers(telemetry);

            var seeder = Services.GetRequiredService<IStartupSeeder>();
            try
            {
                using var seedCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await seeder.SeedAsync(seedCts.Token);
            }
            catch (Exception ex)
            {
                telemetry.Error("Startup seed failed. Continuing app launch with available storage mode.", ex);
            }

            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();

            var navigator = Services.GetRequiredService<IAppNavigator>();
            await navigator.NavigateToLoginAsync();
            telemetry.Info("InterviewFlo app initialized successfully.");
        }
        catch (Exception ex)
        {
            mainWindow.Title = "InterviewFlo - Startup Error";
            mainWindow.Content = new TextBlock
            {
                Text = $"Startup failed: {ex.Message}",
                Margin = new Thickness(24),
            };
        }
    }

    private static IServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
        });

        services.AddInterviewFloInfrastructure(configuration);

        services.AddSingleton<IAppTelemetryService, AppTelemetryService>();
        services.AddSingleton<SessionContext>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IAppNavigator, AppNavigator>();

        services.AddSingleton<IMicrophoneRecorderService, OpenAlMicrophoneRecorderService>();
        services.AddSingleton<ICameraPreviewService, OpenCvCameraPreviewService>();
        services.AddSingleton<IMediaPermissionService, MediaPermissionService>();
        services.AddOptions<ElevenLabsVoiceSettings>().Bind(configuration.GetSection("ElevenLabs"));
        services.AddSingleton<IInterviewVoiceService, WindowsInterviewVoiceService>();

        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<PasswordResetViewModel>();
        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<CandidatePermissionViewModel>();
        services.AddTransient<CandidateRulesViewModel>();
        services.AddTransient<CandidateLobbyViewModel>();
        services.AddTransient<CandidateInterviewViewModel>();
        services.AddTransient<CandidateFeedbackViewModel>();
        services.AddTransient<CandidateResultViewModel>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureGlobalExceptionHandlers(IAppTelemetryService telemetry)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                telemetry.Error("Unhandled exception in AppDomain.", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            telemetry.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
