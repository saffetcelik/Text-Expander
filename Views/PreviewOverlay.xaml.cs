using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using OtomatikMetinGenisletici.Helpers;
using OtomatikMetinGenisletici.Services;

namespace OtomatikMetinGenisletici.Views
{
    public partial class PreviewOverlay : Window
    {
        // Win32 API constants for window behavior
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        // Test projesindeki gibi gelişmiş caret pozisyon bulma için ek API'ler
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private string _lastSetText = "";
        // Debounce timer kaldırıldı - senkron işlem için
        private IImageRecognitionService? _imageRecognitionService;

        // UDF editörü senkronizasyon için event - artık kullanılmıyor, gerçek UDF tracking kullanıyoruz
        // public static event EventHandler<bool>? UdfEditorVisibilityChanged;

        public PreviewOverlay()
        {
            try
            {
                Console.WriteLine("[PREVIEW] Hızlı PreviewOverlay başlatılıyor");

                // Hızlı initialization - minimum gereksinimler
                InitializeComponent();

                // Basit pencere ayarları - performans odaklı
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = System.Windows.Media.Brushes.Transparent;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;
                Focusable = false;
                IsHitTestVisible = false;
                IsTabStop = false;

                // Gizli başlat - hızlı
                Visibility = Visibility.Hidden;

                // Timer kaldırıldı - senkron işlem için

                // Event handler'ları lazy initialize et
                InitializeEventHandlersLazy();

                // SourceInitialized event'ini dinle - Win32 API ayarları için
                SourceInitialized += PreviewOverlay_SourceInitialized;

                // ImageRecognitionService'i başlat
                _imageRecognitionService = new ImageRecognitionService();
                Console.WriteLine("[PREVIEW] ImageRecognitionService başlatıldı");

                Console.WriteLine("[PREVIEW] Hızlı PreviewOverlay hazır");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PreviewOverlay hatası: {ex.Message}");
                throw;
            }
        }

        private void PreviewOverlay_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                // Win32 API ile pencere davranışını ayarla (test projesindeki gibi)
                var hwnd = new WindowInteropHelper(this).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

                Console.WriteLine("[PREVIEW] Win32 API ayarları uygulandı - pencere artık aktif olmayacak");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Win32 API ayarları hatası: {ex.Message}");
            }
        }

        private void InitializeEventHandlersLazy()
        {
            // Event handler'ları ilk kullanımda ekle
            KeyDown += PreviewOverlay_KeyDown;
        }

        private void ProcessTextUpdate(string text)
        {
            try
            {
                Console.WriteLine($"[PREVIEW] Metin işleniyor: '{text}'");

                if (string.IsNullOrEmpty(text?.Trim()))
                {
                    Console.WriteLine("[PREVIEW] Boş metin - pencere gizleniyor");
                    if (Visibility == Visibility.Visible)
                    {
                        Visibility = Visibility.Hidden;
                        // UDF editörü event'ini kaldırdık - artık gerçek UDF tracking kullanıyoruz
                        // UdfEditorVisibilityChanged?.Invoke(this, false);
                    }
                    return;
                }

                // Metni göster
                ParseAndDisplayText(text);

                // Pencereyi göster
                if (Visibility != Visibility.Visible)
                {
                    ShowPreview();
                }
                else
                {
                    UpdatePosition();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ProcessTextUpdate hatası: {ex.Message}");
            }
        }

        private void ParseAndDisplayText(string text)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (FindName("PreviewTextBlock") is System.Windows.Controls.TextBlock textBlock)
                    {
                        textBlock.Text = text;
                        Console.WriteLine($"[PREVIEW] Metin güncellendi: '{text}'");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ParseAndDisplayText hatası: {ex.Message}");
            }
        }

        private void ShowPreview()
        {
            try
            {
                Console.WriteLine("[PREVIEW] Pencere gösteriliyor");

                // Pozisyonu ayarla
                UpdatePosition();

                // Göster - test projesindeki gibi basit Show() kullan
                if (Visibility != Visibility.Visible)
                {
                    Show(); // Visibility yerine Show() kullan
                    Console.WriteLine("[PREVIEW] Pencere Show() ile gösterildi");

                    // UDF editörü event'ini kaldırdık - artık gerçek UDF tracking kullanıyoruz
                    // UdfEditorVisibilityChanged?.Invoke(this, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowPreview hatası: {ex.Message}");
            }
        }

        private void UpdatePosition()
        {
            try
            {
                Console.WriteLine("[PREVIEW] UpdatePosition çağrıldı");

                // Test projesindeki gibi daha güvenilir pozisyonlama sistemi
                var caretPos = GetCaretPositionImproved();
                Console.WriteLine($"[PREVIEW] GetCaretPositionImproved sonucu: {(caretPos.HasValue ? $"{caretPos.Value.X},{caretPos.Value.Y}" : "null")}");

                if (caretPos.HasValue)
                {
                    // Ekran sınırlarını kontrol et
                    var screenWidth = SystemParameters.PrimaryScreenWidth;
                    var screenHeight = SystemParameters.PrimaryScreenHeight;

                    double newLeft = caretPos.Value.X + 10;
                    double newTop = caretPos.Value.Y + 25;

                    // Sağ kenara taşma kontrolü
                    if (newLeft + Width > screenWidth)
                    {
                        newLeft = caretPos.Value.X - Width - 10;
                    }

                    // Alt kenara taşma kontrolü
                    if (newTop + Height > screenHeight)
                    {
                        newTop = caretPos.Value.Y - Height - 10;
                    }

                    // Negatif değerleri düzelt
                    newLeft = Math.Max(0, newLeft);
                    newTop = Math.Max(0, newTop);

                    Left = newLeft;
                    Top = newTop;

                    Console.WriteLine($"[PREVIEW] Pozisyon güncellendi: {Left}, {Top} (Caret: {caretPos.Value.X}, {caretPos.Value.Y})");
                }
                else
                {
                    Console.WriteLine($"[PREVIEW] Caret pozisyonu bulunamadı, pencere gizleniyor");
                    if (Visibility == Visibility.Visible)
                    {
                        Hide();
                        // UDF editörü event'ini kaldırdık - artık gerçek UDF tracking kullanıyoruz
                        // UdfEditorVisibilityChanged?.Invoke(this, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdatePosition hatası: {ex.Message}");
            }
        }

        public void SetText(string text)
        {
            try
            {
                var normalizedText = text ?? "";

                // Aynı text için tekrar işlem yapma - performance optimization
                if (_lastSetText == normalizedText)
                {
                    return;
                }

                _lastSetText = normalizedText;

                // Hızlı tab basma için direkt işleme geç
                ProcessTextUpdate(normalizedText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetText hatası: {ex.Message}");
            }
        }

        public void HidePreview()
        {
            try
            {
                Console.WriteLine("[PREVIEW] Pencere gizleniyor");
                // Test projesindeki gibi basit Hide() kullan
                if (Visibility == Visibility.Visible)
                {
                    Hide(); // Visibility yerine Hide() kullan
                    Console.WriteLine("[PREVIEW] Pencere Hide() ile gizlendi");

                    // UDF editörü event'ini kaldırdık - artık gerçek UDF tracking kullanıyoruz
                    // UdfEditorVisibilityChanged?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] HidePreview hatası: {ex.Message}");
            }
        }

        private void PreviewOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HidePreview();
            }
        }

        // Test projesindeki gibi gelişmiş caret pozisyon bulma metodu
        private System.Drawing.Point? GetCaretPositionImproved()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                // Pencere başlığını kontrol et
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, 256);
                string title = windowText.ToString();

                Console.WriteLine($"[CARET] Aktif pencere: '{title}'");

                // .UDF pencereleri veya Döküman Editörü için önce ImageRecognitionService'i dene
                if ((title.Contains(".UDF") || title.Contains("Döküman Editörü")) && _imageRecognitionService != null && _imageRecognitionService.IsEnabled)
                {
                    var imageCaretPos = _imageRecognitionService.FindCaretByImageRecognition();
                    if (imageCaretPos.HasValue)
                    {
                        if (title.Contains(".UDF"))
                        {
                            Console.WriteLine($"[CARET] .UDF penceresi - imlec.png ile bulundu: {imageCaretPos.Value.X}, {imageCaretPos.Value.Y}");
                        }
                        else
                        {
                            Console.WriteLine($"[CARET] Döküman Editörü penceresi - imlec.png ile bulundu: {imageCaretPos.Value.X}, {imageCaretPos.Value.Y}");
                        }
                        return imageCaretPos.Value;
                    }
                    else
                    {
                        if (title.Contains(".UDF"))
                        {
                            Console.WriteLine($"[CARET] .UDF penceresi - imlec.png ile bulunamadı, standart yöntemlere geçiliyor");
                        }
                        else
                        {
                            Console.WriteLine($"[CARET] Döküman Editörü penceresi - imlec.png ile bulunamadı, standart yöntemlere geçiliyor");
                        }
                    }
                }

                // GUITHREADINFO ile caret pozisyonunu bul
                GUITHREADINFO guiThreadInfo = new GUITHREADINFO();
                guiThreadInfo.cbSize = (uint)Marshal.SizeOf(guiThreadInfo);
                uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

                if (threadId != 0 && GetGUIThreadInfo(threadId, ref guiThreadInfo))
                {
                    // Caret bilgisi var mı kontrol et
                    if (!(guiThreadInfo.rcCaret.Left == 0 && guiThreadInfo.rcCaret.Right == 0))
                    {
                        System.Drawing.Point caretPos = new System.Drawing.Point(
                            guiThreadInfo.rcCaret.Left,
                            guiThreadInfo.rcCaret.Bottom
                        );

                        // Client koordinatlarını ekran koordinatlarına çevir
                        if (ClientToScreen(guiThreadInfo.hwndCaret, ref caretPos))
                        {
                            Console.WriteLine($"[CARET] GUITHREADINFO ile bulundu: {caretPos.X}, {caretPos.Y}");
                            return caretPos;
                        }
                    }
                }

                Console.WriteLine($"[CARET] GUITHREADINFO ile bulunamadı");

                // UDF editörü için özel fallback: Pencere merkezine yakın bir konum
                if (title.Contains(".UDF"))
                {
                    RECT windowRect;
                    if (GetWindowRect(hwnd, out windowRect))
                    {
                        // Pencere içinde merkezi bir konum hesapla
                        int centerX = windowRect.Left + (windowRect.Right - windowRect.Left) / 2;
                        int centerY = windowRect.Top + (windowRect.Bottom - windowRect.Top) / 3; // Üst 1/3'lük kısım

                        Console.WriteLine($"[CARET] .UDF penceresi için merkez pozisyon kullanıldı: {centerX}, {centerY}");
                        return new System.Drawing.Point(centerX, centerY);
                    }
                }

                // Genel fallback: Mouse pozisyonunu kullan
                var mousePos = System.Windows.Forms.Cursor.Position;
                Console.WriteLine($"[CARET] Mouse pozisyonu kullanıldı: {mousePos.X}, {mousePos.Y}");
                return mousePos;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetCaretPositionImproved hatası: {ex.Message}");
                return null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // ImageRecognitionService'i temizle
                _imageRecognitionService?.Dispose();
                _imageRecognitionService = null;
                Console.WriteLine("[PREVIEW] ImageRecognitionService temizlendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnClosed hatası: {ex.Message}");
            }

            base.OnClosed(e);
        }
    }
}