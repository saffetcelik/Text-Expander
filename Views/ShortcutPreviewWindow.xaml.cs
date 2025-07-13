using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutPreviewWindow : Window
    {
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
            if (_settingsService.Settings.ShortcutPreviewPanelLeft >= 0 &&
                _settingsService.Settings.ShortcutPreviewPanelTop >= 0)
            {
                // Kaydedilmiş pozisyonu kullan
                Left = _settingsService.Settings.ShortcutPreviewPanelLeft;
                Top = _settingsService.Settings.ShortcutPreviewPanelTop;
            }
            else
            {
                // İlk açılışta ekranın sağına yerleştir
                PositionWindowToRight();
            }

            // Event handler'ları ekle
            PreviewPanel.MinimizeRequested += PreviewPanel_MinimizeRequested;
            PreviewPanel.AddShortcutRequested += PreviewPanel_AddShortcutRequested;
        }

        public void UpdateShortcuts(ObservableCollection<Shortcut> shortcuts)
        {
            PreviewPanel.Shortcuts = shortcuts;
        }

        private void PositionWindowToRight()
        {
            // Ana pencereyi bul
            var mainWindow = Application.Current.MainWindow;

            if (mainWindow != null && mainWindow.IsLoaded)
            {
                // Ana pencereye yapışık olarak konumlandır
                Left = mainWindow.Left + mainWindow.Width + 5; // 5px boşluk
                Top = mainWindow.Top; // Ana pencere ile aynı yükseklikte başla

                // Eğer ekran dışına taşarsa, ana pencerenin soluna yerleştir
                var workingArea = SystemParameters.WorkArea;
                if (Left + Width > workingArea.Right)
                {
                    Left = mainWindow.Left - Width - 5; // Ana pencerenin soluna
                }

                // Dikey olarak ekran sınırları içinde tut
                if (Top + Height > workingArea.Bottom)
                {
                    Top = workingArea.Bottom - Height - 10;
                }
                if (Top < workingArea.Top)
                {
                    Top = workingArea.Top + 10;
                }
            }
            else
            {
                // Ana pencere bulunamazsa ekranın sağ tarafına yerleştir (eski davranış)
                var workingArea = SystemParameters.WorkArea;
                Left = workingArea.Right - Width - 10; // 10px margin
                Top = (workingArea.Height - Height) / 2; // Dikey olarak ortala
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
