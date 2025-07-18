using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutPreviewWindow : Window
    {
        // Windows API constants for click-through functionality
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly ISettingsService _settingsService;
        private bool _isDragging = false;
        private Point _clickPosition;
        private double _normalHeight;
        private double _normalWidth;
        private double _minimizedHeight = 50; // Sadece başlık için yeterli yükseklik

        public event EventHandler? CloseRequested;

        public ShortcutPreviewWindow(ISettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;

            // SettingsService'i PreviewPanel'e geç
            PreviewPanel.SetSettingsService(_settingsService);

            // Ayarlardan değerleri al
            Opacity = _settingsService.Settings.ShortcutPreviewPanelOpacity;
            Width = _settingsService.Settings.ShortcutPreviewPanelWidth;
            Height = _settingsService.Settings.ShortcutPreviewPanelHeight;
            _normalHeight = Height; // Normal yüksekliği kaydet
            _normalWidth = Width; // Normal genişliği kaydet

            // Pozisyonu ayarla
            Console.WriteLine($"[SHORTCUT_PREVIEW] Pozisyon ayarlanıyor - Kaydedilmiş: Left={_settingsService.Settings.ShortcutPreviewPanelLeft}, Top={_settingsService.Settings.ShortcutPreviewPanelTop}");

            if (_settingsService.Settings.ShortcutPreviewPanelLeft >= 0 &&
                _settingsService.Settings.ShortcutPreviewPanelTop >= 0)
            {
                // Kaydedilmiş pozisyonu kullan
                Left = _settingsService.Settings.ShortcutPreviewPanelLeft;
                Top = _settingsService.Settings.ShortcutPreviewPanelTop;
                Console.WriteLine($"[SHORTCUT_PREVIEW] Kaydedilmiş pozisyon kullanıldı: Left={Left}, Top={Top}");
            }
            else
            {
                // İlk açılışta ana pencereye göre yerleştir
                Console.WriteLine("[SHORTCUT_PREVIEW] İlk açılış, ana pencereye göre pozisyon hesaplanıyor");
                PositionWindowToRight();
            }

            // Event handler'ları ekle
            PreviewPanel.MinimizeRequested += PreviewPanel_MinimizeRequested;
            PreviewPanel.AddShortcutRequested += PreviewPanel_AddShortcutRequested;
            PreviewPanel.ClickThroughChanged += PreviewPanel_ClickThroughChanged;
            PreviewPanel.SyncWithMainWindowChanged += PreviewPanel_SyncWithMainWindowChanged;
        }

        public void UpdateShortcuts(ObservableCollection<Shortcut> shortcuts)
        {
            PreviewPanel.Shortcuts = shortcuts;
        }

        private void PositionWindowToRight()
        {
            Console.WriteLine("[SHORTCUT_PREVIEW] PositionWindowToRight çağrıldı");

            // Ana pencereyi bul
            var mainWindow = Application.Current.MainWindow;
            Console.WriteLine($"[SHORTCUT_PREVIEW] Ana pencere: {mainWindow?.GetType().Name}");

            if (mainWindow != null)
            {
                Console.WriteLine($"[SHORTCUT_PREVIEW] Ana pencere durumu - IsLoaded: {mainWindow.IsLoaded}, Left: {mainWindow.Left}, Top: {mainWindow.Top}, Width: {mainWindow.Width}, Height: {mainWindow.Height}");

                // Ana pencere yüklenmemişse, yüklenmesini bekle
                if (!mainWindow.IsLoaded)
                {
                    Console.WriteLine("[SHORTCUT_PREVIEW] Ana pencere henüz yüklenmemiş, Loaded event'ini bekliyoruz");
                    mainWindow.Loaded += (s, e) => {
                        Console.WriteLine("[SHORTCUT_PREVIEW] Ana pencere yüklendi, pozisyon yeniden hesaplanıyor");
                        PositionWindowToRight();
                    };

                    // Geçici olarak ekranın sağ tarafına yerleştir
                    var tempWorkingArea = SystemParameters.WorkArea;
                    Left = tempWorkingArea.Right - Width - 10;
                    Top = (tempWorkingArea.Height - Height) / 2;
                    Console.WriteLine($"[SHORTCUT_PREVIEW] Geçici pozisyon: Left={Left}, Top={Top}");
                    return;
                }

                // Ana pencereye yapışık olarak konumlandır
                double newLeft = mainWindow.Left + mainWindow.Width + 5; // 5px boşluk
                double newTop = mainWindow.Top; // Ana pencere ile aynı yükseklikte başla

                Console.WriteLine($"[SHORTCUT_PREVIEW] Hesaplanan pozisyon: Left={newLeft}, Top={newTop}");

                // Eğer ekran dışına taşarsa, ana pencerenin soluna yerleştir
                var workingArea = SystemParameters.WorkArea;
                Console.WriteLine($"[SHORTCUT_PREVIEW] Çalışma alanı: Right={workingArea.Right}, Bottom={workingArea.Bottom}");

                if (newLeft + Width > workingArea.Right)
                {
                    newLeft = mainWindow.Left - Width - 5; // Ana pencerenin soluna
                    Console.WriteLine($"[SHORTCUT_PREVIEW] Ekran dışına taşıyor, sol tarafa yerleştiriliyor: Left={newLeft}");
                }

                // Dikey olarak ekran sınırları içinde tut
                if (newTop + Height > workingArea.Bottom)
                {
                    newTop = workingArea.Bottom - Height - 10;
                    Console.WriteLine($"[SHORTCUT_PREVIEW] Alt sınırı aşıyor, yukarı kaydırılıyor: Top={newTop}");
                }
                if (newTop < workingArea.Top)
                {
                    newTop = workingArea.Top + 10;
                    Console.WriteLine($"[SHORTCUT_PREVIEW] Üst sınırı aşıyor, aşağı kaydırılıyor: Top={newTop}");
                }

                Left = newLeft;
                Top = newTop;
                Console.WriteLine($"[SHORTCUT_PREVIEW] Final pozisyon: Left={Left}, Top={Top}");
            }
            else
            {
                Console.WriteLine("[SHORTCUT_PREVIEW] Ana pencere bulunamadı, ekranın sağ tarafına yerleştiriliyor");
                // Ana pencere bulunamazsa ekranın sağ tarafına yerleştir (eski davranış)
                var workingArea = SystemParameters.WorkArea;
                Left = workingArea.Right - Width - 10; // 10px margin
                Top = (workingArea.Height - Height) / 2; // Dikey olarak ortala
                Console.WriteLine($"[SHORTCUT_PREVIEW] Fallback pozisyon: Left={Left}, Top={Top}");
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _isDragging = true;
                _clickPosition = e.GetPosition(this);
                CaptureMouse();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isDragging && IsMouseCaptured)
            {
                var currentPosition = e.GetPosition(this);
                var deltaX = currentPosition.X - _clickPosition.X;
                var deltaY = currentPosition.Y - _clickPosition.Y;

                Left += deltaX;
                Top += deltaY;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            base.OnMouseLeftButtonUp(e);
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Resize işlemi için Windows API kullan
            e.Handled = true;

            // Windows resize işlemini başlat
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // SC_SIZE komutunu gönder
                const int WM_SYSCOMMAND = 0x112;
                const int SC_SIZE = 0xF000;
                const int WMSZ_BOTTOMRIGHT = 8;

                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(hwnd, WM_SYSCOMMAND, SC_SIZE + WMSZ_BOTTOMRIGHT, 0);
            }
        }

        // Native methods for resize
        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // Pozisyon değişikliklerini kaydet
            if (_settingsService != null && IsLoaded)
            {
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelLeft = Left;
                settings.ShortcutPreviewPanelTop = Top;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Boyut değişikliklerini ayarlara kaydet
            // Ancak minimize durumunda değilse ve manuel resize ise kaydet
            if (_settingsService != null && IsLoaded && !PreviewPanel.IsMinimized && Height > _minimizedHeight)
            {
                // Normal boyuttayken değişiklikleri kaydet
                _normalHeight = Height;
                _normalWidth = Width;

                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelWidth = Width;
                settings.ShortcutPreviewPanelHeight = Height;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();

                System.Diagnostics.Debug.WriteLine($"Size changed - Saving: Width={Width}, Height={Height}");
            }
        }

        private void PreviewPanel_CloseRequested(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PreviewPanel_OpacityChanged(object sender, double opacity)
        {
            Opacity = opacity;

            // Opacity değişikliğini ayarlara kaydet
            if (_settingsService != null)
            {
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelOpacity = opacity;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();
            }
        }

        private void PreviewPanel_MinimizeRequested(object? sender, bool isMinimized)
        {
            if (isMinimized)
            {
                // Minimize: mevcut boyutları kaydet ve küçült
                // Sadece minimize edilmeden önceki boyutları kaydet, ayarlardaki değerleri değiştirme
                _normalHeight = Height;
                _normalWidth = Width;

                // Debug için
                System.Diagnostics.Debug.WriteLine($"Minimizing - Saving to memory: Width={_normalWidth}, Height={_normalHeight}");

                Height = _minimizedHeight;
                MinHeight = _minimizedHeight;
                MaxHeight = _minimizedHeight;
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                // Restore: kaydedilmiş boyutları geri yükle
                System.Diagnostics.Debug.WriteLine($"Restoring - Using: Width={_normalWidth}, Height={_normalHeight}");

                // Restore işlemi sırasında SizeChanged event'ini geçici olarak devre dışı bırak
                var tempWidth = _normalWidth;
                var tempHeight = _normalHeight;

                MinHeight = 300;
                MaxHeight = double.PositiveInfinity;
                ResizeMode = ResizeMode.CanResizeWithGrip;

                // Boyutları geri yükle
                Width = tempWidth;
                Height = tempHeight;

                // Restore sonrası boyutları ayarlara kaydet (kullanıcının son ayarladığı boyut)
                if (_settingsService != null)
                {
                    var settings = _settingsService.GetCopy();
                    settings.ShortcutPreviewPanelWidth = tempWidth;
                    settings.ShortcutPreviewPanelHeight = tempHeight;
                    _settingsService.UpdateSettings(settings);
                    _ = _settingsService.SaveSettingsAsync();

                    System.Diagnostics.Debug.WriteLine($"Restore - Saved to settings: Width={tempWidth}, Height={tempHeight}");
                }
            }
        }

        public void SetOpacity(double opacity)
        {
            Opacity = Math.Max(0.3, Math.Min(1.0, opacity));
            PreviewPanel.PanelOpacity = Opacity;
        }

        private async void PreviewPanel_AddShortcutRequested(object? sender, EventArgs e)
        {
            try
            {
                // Ana penceredeki ViewModel'i al
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.DataContext is ViewModels.MainViewModel viewModel)
                {
                    // ShortcutDialog'u aç
                    var shortcutDialog = ServiceProviderExtensions.Services.GetRequiredService<ShortcutDialog>();
                    shortcutDialog.Owner = this;

                    var result = shortcutDialog.ShowDialog();

                    // Dialog başarıyla kapatıldıysa kısayolu ekle
                    if (result == true)
                    {
                        Console.WriteLine($"[DEBUG] Kısayol önizleme panelinden ekleniyor: Key='{shortcutDialog.ShortcutKey}', Text='{shortcutDialog.ExpansionText}'");

                        // Ana ViewModel'deki ShortcutService'i kullanarak kısayolu ekle
                        var shortcutService = ServiceProviderExtensions.Services.GetRequiredService<IShortcutService>();

                        // Kısayol zaten var mı kontrol et
                        if (shortcutService.ShortcutExists(shortcutDialog.ShortcutKey))
                        {
                            var confirmResult = MessageBox.Show(
                                $"'{shortcutDialog.ShortcutKey}' kısayolu zaten mevcut. Üzerine yazmak istiyor musunuz?",
                                "Kısayol Mevcut",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (confirmResult != MessageBoxResult.Yes)
                                return;
                        }

                        // Kısayolu ekle
                        shortcutService.AddShortcut(shortcutDialog.ShortcutKey, shortcutDialog.ExpansionText);
                        await shortcutService.SaveShortcutsAsync();

                        // Ana ViewModel'i güncelle
                        viewModel.OnPropertyChanged(nameof(viewModel.Shortcuts));
                        viewModel.UpdateStats();
                        viewModel.UpdateAnalytics();
                        viewModel.UpdateShortcutPreviewPanel();

                        // Önizleme panelini güncelle
                        UpdateShortcuts(viewModel.Shortcuts);

                        Console.WriteLine("[DEBUG] Kısayol başarıyla eklendi ve UI güncellendi");
                    }
                }
                else
                {
                    MessageBox.Show("Ana pencere bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol ekleme hatası: {ex.Message}");
                MessageBox.Show($"Kısayol eklenirken hata oluştu:\n{ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewPanel_ClickThroughChanged(object? sender, bool isEnabled)
        {
            SetClickThrough(isEnabled);
        }

        private void PreviewPanel_SyncWithMainWindowChanged(object? sender, bool isEnabled)
        {
            // Ana pencere ile senkronizasyon durumu değişti
            // MainViewModel'e bildirim gönder
            SyncWithMainWindowChanged?.Invoke(this, isEnabled);
        }

        // Ana pencere ile senkronizasyon için event
        public event EventHandler<bool>? SyncWithMainWindowChanged;

        private void SetClickThrough(bool enabled)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                if (enabled)
                {
                    // Enable click-through for the entire window
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);

                    // Add hit test hook to make header clickable
                    var source = HwndSource.FromHwnd(hwnd);
                    source?.AddHook(HitTestHook);

                    System.Diagnostics.Debug.WriteLine("Click-through enabled");
                }
                else
                {
                    // Disable click-through
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                    // Remove hit test hook
                    var source = HwndSource.FromHwnd(hwnd);
                    source?.RemoveHook(HitTestHook);

                    System.Diagnostics.Debug.WriteLine("Click-through disabled");
                }
            }
        }

        private IntPtr HitTestHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            const int HTCLIENT = 1;

            if (msg == WM_NCHITTEST)
            {
                // Get cursor position from lParam
                int x = (short)(lParam.ToInt32() & 0xFFFF);
                int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

                // Convert screen coordinates to window coordinates
                var screenPoint = new Point(x, y);
                var windowPoint = PointFromScreen(screenPoint);

                // Make the entire top area clickable (first 120 pixels to be sure)
                // This should definitely include all header buttons
                if (windowPoint.Y >= -10 && windowPoint.Y <= 120 &&
                    windowPoint.X >= -10 && windowPoint.X <= ActualWidth + 10)
                {
                    handled = true;
                    return (IntPtr)HTCLIENT; // Make header area fully interactive
                }

                // For other areas, make transparent
                handled = true;
                return (IntPtr)HTTRANSPARENT;
            }

            return IntPtr.Zero;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Window handle is now available, we can set click-through if needed
            if (PreviewPanel.IsClickThroughEnabled)
            {
                SetClickThrough(true);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Kapatılırken ayarlarda görünürlüğü false yap
            if (_settingsService != null)
            {
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelVisible = false;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();
            }
            base.OnClosed(e);
        }
    }
}
