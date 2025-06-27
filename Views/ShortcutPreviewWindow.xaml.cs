using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutPreviewWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private bool _isDragging = false;
        private Point _clickPosition;
        private double _normalHeight;
        private double _minimizedHeight = 35; // Çok ince minimize hali

        public event EventHandler? CloseRequested;

        public ShortcutPreviewWindow(ISettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;

            // Ayarlardan değerleri al
            Opacity = _settingsService.Settings.ShortcutPreviewPanelOpacity;
            Width = _settingsService.Settings.ShortcutPreviewPanelWidth;
            Height = _settingsService.Settings.ShortcutPreviewPanelHeight;
            _normalHeight = Height; // Normal yüksekliği kaydet

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

            // Minimize event handler'ını ekle
            PreviewPanel.MinimizeRequested += PreviewPanel_MinimizeRequested;
        }

        public void UpdateShortcuts(ObservableCollection<Shortcut> shortcuts)
        {
            PreviewPanel.Shortcuts = shortcuts;
        }

        private void PositionWindowToRight()
        {
            // Ekranın sağ tarafına yapışık olarak konumlandır
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 10; // 10px margin
            Top = (workingArea.Height - Height) / 2; // Dikey olarak ortala
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
            if (_settingsService != null && IsLoaded)
            {
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelWidth = Width;
                settings.ShortcutPreviewPanelHeight = Height;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();
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
                // Minimize: sadece başlık görünsün
                _normalHeight = Height; // Mevcut yüksekliği kaydet
                Height = _minimizedHeight;
                MinHeight = _minimizedHeight;
                MaxHeight = _minimizedHeight;
                ResizeMode = ResizeMode.NoResize; // Resize'ı devre dışı bırak
            }
            else
            {
                // Restore: normal boyuta dön
                Height = _normalHeight;
                MinHeight = 300; // Orijinal minimum yükseklik
                MaxHeight = double.PositiveInfinity;
                ResizeMode = ResizeMode.CanResizeWithGrip; // Resize'ı tekrar etkinleştir
            }
        }

        public void SetOpacity(double opacity)
        {
            Opacity = Math.Max(0.3, Math.Min(1.0, opacity));
            PreviewPanel.PanelOpacity = Opacity;
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
