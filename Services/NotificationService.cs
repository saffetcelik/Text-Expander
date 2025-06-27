using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Linq;

namespace OtomatikMetinGenisletici.Services
{
    public class NotificationService : INotificationService, IDisposable
    {
        private TaskbarIcon? _trayIcon;
        private bool _disposed = false;
        private MainWindow? _mainWindow;

        public bool IsTrayVisible => _trayIcon?.Visibility == Visibility.Visible;

        public NotificationService()
        {
            InitializeTrayIcon();
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private System.Drawing.Icon LoadCustomIcon()
        {
            try
            {
                // İlk olarak uygulama dizininde icon.ico dosyasını ara
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");

                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }

                // Eğer dosya bulunamazsa, embedded resource olarak dene
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("icon.ico"));

                if (!string.IsNullOrEmpty(resourceName))
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        return new System.Drawing.Icon(stream);
                    }
                }

                // Son çare olarak sistem iconunu kullan
                return System.Drawing.SystemIcons.Application;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Icon yüklenemedi: {ex.Message}");
                return System.Drawing.SystemIcons.Application;
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new TaskbarIcon
            {
                Icon = LoadCustomIcon(),
                ToolTipText = "Gelişmiş Otomatik Metin Genişletici"
            };

            // Context menu for tray icon
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var showMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "Göster"
            };
            showMenuItem.Click += ShowMenuItem_Click;

            var settingsMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "Ayarlar"
            };
            settingsMenuItem.Click += SettingsMenuItem_Click;

            var exitMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "Çıkış"
            };
            exitMenuItem.Click += ExitMenuItem_Click;

            contextMenu.Items.Add(showMenuItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(settingsMenuItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(exitMenuItem);

            _trayIcon.ContextMenu = contextMenu;

            // Çift tıklama ile pencereyi göster
            _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;

            // Sol tıklama ile de pencereyi göster (tek tıklama)
            _trayIcon.TrayLeftMouseUp += TrayIcon_TrayLeftMouseUp;

            // Sağ tıklama debug
            _trayIcon.TrayRightMouseUp += TrayIcon_TrayRightMouseUp;
        }

        private void ShowMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Context menu 'Göster' clicked");
            ShowMainWindow();
        }

        private void SettingsMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Context menu 'Ayarlar' clicked");
            ShowSettings();
        }

        private void ExitMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Context menu 'Çıkış' clicked");
            ExitApplication();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Tray icon double clicked");
            ShowMainWindow();
        }

        private void TrayIcon_TrayLeftMouseUp(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Tray icon left clicked");
            ShowMainWindow();
        }

        private void TrayIcon_TrayRightMouseUp(object sender, System.Windows.RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] Tray icon right clicked - context menu should appear");
        }

        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            var icon = type switch
            {
                NotificationType.Success => BalloonIcon.Info,
                NotificationType.Warning => BalloonIcon.Warning,
                NotificationType.Error => BalloonIcon.Error,
                _ => BalloonIcon.Info
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, GetMessageBoxImage(type));
            });
        }

        public void ShowTrayNotification(string title, string message)
        {
            _trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        public void ShowTrayIcon()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Visible;
            }
        }

        public void HideTrayIcon()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visibility = Visibility.Hidden;
            }
        }

        private MessageBoxImage GetMessageBoxImage(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.Information
            };
        }

        private void ShowMainWindow()
        {
            try
            {
                Console.WriteLine("[DEBUG] Tray icon clicked - ShowMainWindow called");

                // UI thread'de değilsek Dispatcher kullan
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(() => ShowMainWindow());
                    return;
                }

                // Önce kayıtlı MainWindow'u dene
                if (_mainWindow != null)
                {
                    Console.WriteLine("[DEBUG] Using registered MainWindow");
                    _mainWindow.ShowMainWindow();
                    return;
                }

                // Fallback: Application.Current.MainWindow kullan
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    Console.WriteLine("[DEBUG] Using Application.Current.MainWindow");
                    mainWindow.ShowMainWindow();
                    return;
                }

                // Son çare: Tüm açık pencereleri kontrol et
                Console.WriteLine("[DEBUG] MainWindow is null! Trying to find window...");
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mw)
                    {
                        Console.WriteLine("[DEBUG] Found MainWindow in Windows collection");
                        mw.ShowMainWindow();
                        return;
                    }
                }

                Console.WriteLine("[ERROR] No MainWindow found anywhere!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowMainWindow failed: {ex.Message}");
            }
        }

        private void ShowSettings()
        {
            try
            {
                Console.WriteLine("[DEBUG] Tray 'Ayarlar' clicked");

                // UI thread'de değilsek Dispatcher kullan
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(() => ShowSettings());
                    return;
                }

                // Önce ana pencereyi göster
                ShowMainWindow();

                // Kısa bir bekleme (ana pencerenin açılması için)
                System.Threading.Thread.Sleep(100);

                // MainWindow'u bul
                MainWindow? targetWindow = _mainWindow ?? Application.Current.MainWindow as MainWindow;

                if (targetWindow == null)
                {
                    // Son çare: Tüm açık pencereleri kontrol et
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is MainWindow mw)
                        {
                            targetWindow = mw;
                            break;
                        }
                    }
                }

                if (targetWindow != null)
                {
                    // Ayarlar penceresini aç
                    var settingsWindow = ServiceProviderExtensions.Services.GetRequiredService<Views.SettingsWindow>();
                    settingsWindow.Owner = targetWindow;
                    settingsWindow.ShowDialog();
                }
                else
                {
                    Console.WriteLine("[ERROR] Could not find MainWindow for settings dialog");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowSettings failed: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _trayIcon?.Dispose();
                _disposed = true;
            }
        }
    }
}
