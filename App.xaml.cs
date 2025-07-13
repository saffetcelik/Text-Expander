using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtomatikMetinGenisletici.Services;
using OtomatikMetinGenisletici.ViewModels;
using OtomatikMetinGenisletici.Views;
using ModernWpf;
using System.Threading;

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
    private static Mutex? _mutex;
    private const string MutexName = "OtomatikMetinGenisletici_SingleInstance";

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

            // Tek instance kontrolü
            bool isNewInstance;
            _mutex = new Mutex(true, MutexName, out isNewInstance);

            if (!isNewInstance)
            {
                Console.WriteLine("[DEBUG] Program zaten çalışıyor!");

                var result = MessageBox.Show(
                    "Metin Genişletici Programı  Zaten Açık!\n\nYeniden başlatılsın mı?",
                    "Metin Genişletici Programı  Zaten Açık",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    Console.WriteLine("[DEBUG] Kullanıcı yeniden başlatmayı seçti, mevcut instance kapatılıyor...");

                    // Mevcut instance'ları bul ve kapat
                    try
                    {
                        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                        var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

                        foreach (var process in processes)
                        {
                            if (process.Id != currentProcess.Id)
                            {
                                Console.WriteLine($"[DEBUG] Process kapatılıyor: {process.Id}");
                                process.Kill();
                                process.WaitForExit(3000); // 3 saniye bekle
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Mevcut process kapatma hatası: {ex.Message}");
                    }

                    // Kısa bir süre bekle
                    await Task.Delay(1000);
                    Console.WriteLine("[DEBUG] Yeni instance başlatılıyor...");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Kullanıcı iptal etti, program kapatılıyor...");
                    Shutdown();
                    return;
                }
            }

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
                    services.AddSingleton<ISettingsService, SettingsService>();

                    // SmartSuggestionsService'i ShortcutService'e bağımlı olarak kaydet
                    services.AddSingleton<ISmartSuggestionsService>(provider =>
                    {
                        var settingsService = provider.GetRequiredService<ISettingsService>();
                        var shortcutService = provider.GetRequiredService<IShortcutService>() as ShortcutService;
                        return new SmartSuggestionsService(settingsService, shortcutService);
                    });
                    services.AddSingleton<INotificationService, NotificationService>();
                    services.AddSingleton<IImageRecognitionService, ImageRecognitionService>();
                    services.AddSingleton<IWindowBehaviorService, WindowBehaviorService>();
                    services.AddSingleton<IAdvancedInputService, AdvancedInputService>();
                    services.AddSingleton<ITourService, TourService>();
                    services.AddSingleton<IUdfEditorTrackingService, UdfEditorTrackingService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();

                    // Register Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddTransient<AboutWindow>();
                    services.AddTransient<ShortcutDialog>();
                    services.AddTransient<Views.ShortcutPreviewWindow>();
                    services.AddTransient<Views.ShortcutPreviewPanel>();

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
        try
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            // Mutex'i serbest bırak
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] OnExit hatası: {ex.Message}");
        }

        base.OnExit(e);
    }
}

