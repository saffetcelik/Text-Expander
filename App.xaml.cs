using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtomatikMetinGenisletici.Services;
using OtomatikMetinGenisletici.ViewModels;
using OtomatikMetinGenisletici.Views;
using ModernWpf;

namespace OtomatikMetinGenisletici;

public static class ServiceProviderExtensions
{
    public static IServiceProvider Services => ((App)Application.Current)._host!.Services;
}

/// <summary>
/// Modern WPF Application with Dependency Injection and Hosting
/// </summary>
public partial class App : Application
{
    public IHost? _host;

    public App()
    {
        Console.WriteLine("[DEBUG] App constructor çağrıldı!");

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.WriteLine($"[FATAL] Unhandled exception: {e.ExceptionObject}");
            MessageBox.Show($"Kritik hata:\n{e.ExceptionObject}", "Program Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (sender, e) =>
        {
            Console.WriteLine($"[FATAL] Dispatcher exception: {e.Exception}");
            MessageBox.Show($"UI Hatası:\n{e.Exception.Message}\n\nDetaylar:\n{e.Exception.StackTrace}", "UI Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            Console.WriteLine("[DEBUG] OnStartup başlıyor...");
            Console.WriteLine($"[DEBUG] Args: {string.Join(", ", e.Args)}");

            // Modern WPF Theme - Sadece Light tema
            Console.WriteLine("[DEBUG] Modern WPF Theme ayarlanıyor...");
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            ThemeManager.Current.AccentColor = System.Windows.Media.Colors.Blue;
            Console.WriteLine("[DEBUG] Modern WPF Theme ayarlandı (Light tema sabit)!");

            Console.WriteLine("[DEBUG] Host oluşturuluyor...");
            // Build Host with Dependency Injection
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    Console.WriteLine("[DEBUG] Services kaydediliyor...");

                    // Register Models
                    services.AddSingleton<Models.AppSettings>();

                    // Register Services
                    services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
                    services.AddSingleton<IShortcutService, ShortcutService>();
                    services.AddSingleton<ISmartSuggestionsService, SmartSuggestionsService>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<INotificationService, NotificationService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();

                    // Register Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddTransient<AboutWindow>();
                    services.AddTransient<ShortcutDialog>();

                    // Register Background Services
                    // services.AddHostedService<KeyboardListenerService>(); // DEVRE DIŞI - MainViewModel kullanıyor
                    services.AddHostedService<AutoSaveService>();

                    Console.WriteLine("[DEBUG] Services kaydedildi.");
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            Console.WriteLine("[DEBUG] Host başlatılıyor...");
            await _host.StartAsync();

            Console.WriteLine("[DEBUG] MainWindow oluşturuluyor...");
            // Show Main Window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();

            Console.WriteLine("[DEBUG] MainWindow gösteriliyor...");
            mainWindow.Show();

            Console.WriteLine("[DEBUG] MainWindow gösterildi, program çalışıyor...");

            Console.WriteLine("[DEBUG] Base.OnStartup çağrılıyor...");
            base.OnStartup(e);

            Console.WriteLine("[DEBUG] OnStartup tamamlandı.");
        }
        catch (Exception ex)
        {
            var errorMessage = $"Program başlatılırken hata oluştu:\n\n" +
                              $"Hata: {ex.Message}\n\n" +
                              $"Detay: {ex.InnerException?.Message}\n\n" +
                              $"Stack Trace: {ex.StackTrace}";

            MessageBox.Show(errorMessage, "Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);

            // Console'a da yaz
            Console.WriteLine($"[ERROR] Startup failed: {ex}");

            Environment.Exit(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}

