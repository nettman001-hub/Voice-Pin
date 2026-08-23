using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VoicePin.App.Services;
using VoicePin.Core.Listening;
using VoicePin.Core.Services;
using VoicePin.Infrastructure.Audio;
using VoicePin.Infrastructure.Capture;
using VoicePin.Infrastructure.Data;
using VoicePin.Infrastructure.Security;
using VoicePin.Infrastructure.Settings;
using VoicePin.Infrastructure.Stt;

namespace VoicePin.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Write("UI", e.Exception);
        MessageBox.Show(
            "예기치 않은 오류가 발생했습니다.\n" + e.Exception.Message +
            "\n\n로그: " + Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoicePin", "logs", "app.log"),
            "다들려", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLogger.Write("AppDomain", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Write("Task", e.Exception);
        e.SetObserved();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Db>();
        services.AddSingleton<ISalesRepository, SalesRepository>();
        services.AddSingleton<IBroadcastSessionRepository, BroadcastSessionRepository>();
        services.AddSingleton<IRecognitionRuleRepository, RecognitionRuleRepository>();
        services.AddSingleton<ICaptureRepository, CaptureRepository>();
        services.AddSingleton<ITrainingRepository, TrainingRepository>();

        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();
        services.AddSingleton<IAudioLoopbackSource, WasapiLoopbackSource>();
        services.AddSingleton<IPronunciationScorer, DeepgramPronunciationScorer>();

        services.AddTransient<IMicrophoneRecorder, MicrophoneRecorder>();

        services.AddSingleton<Func<ISttStreamer>>(sp => () =>
        {
            var settingsStore = sp.GetRequiredService<ISettingsStore>();
            var protector = sp.GetRequiredService<ISecretProtector>();
            var settings = settingsStore.Load();
            var apiKey = settings.HasDeepgramKey ? protector.Unprotect(settings.DeepgramApiKeyProtected) : string.Empty;
            return new DeepgramStreamingClient(apiKey, settings.DeepgramModel, settings.DeepgramLanguage);
        });

        services.AddSingleton<ListeningEngine>();

        services.AddSingleton<NavigationService>();
    }
}
