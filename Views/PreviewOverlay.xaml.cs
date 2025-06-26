using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici.Views
{
    public partial class PreviewOverlay : Window
    {
        private DispatcherTimer? _debounceTimer;
        private string _pendingText = "";
        private const int DEBOUNCE_DELAY_MS = 100;

        public PreviewOverlay()
        {
            try
            {
                Console.WriteLine("[PREVIEW] Basit PreviewOverlay başlatılıyor");

                InitializeComponent();

                // Debounce timer
                _debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
                };
                _debounceTimer.Tick += OnDebounceTimerTick;

                // ESC ile kapatma
                KeyDown += PreviewOverlay_KeyDown;

                // Basit pencere ayarları - karmaşık API yok
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = System.Windows.Media.Brushes.Transparent;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;
                Focusable = false;
                IsHitTestVisible = false;
                IsTabStop = false;

                // Gizli başlat
                Visibility = Visibility.Hidden;

                Console.WriteLine("[PREVIEW] Basit PreviewOverlay hazır");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PreviewOverlay hatası: {ex.Message}");
                throw;
            }
        }

        private void OnDebounceTimerTick(object? sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            ProcessTextUpdate(_pendingText);
        }

        private void ProcessTextUpdate(string text)
        {
            try
            {
                Console.WriteLine($"[PREVIEW] Metin işleniyor: '{text}'");

                if (string.IsNullOrEmpty(text?.Trim()))
                {
                    Console.WriteLine("[PREVIEW] Boş metin - pencere gizleniyor");
                    Visibility = Visibility.Hidden;
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

                // Göster
                Visibility = Visibility.Visible;
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
                var caretPos = WindowHelper.GetCaretPosition();
                if (caretPos.HasValue)
                {
                    Left = caretPos.Value.X + 10;
                    Top = caretPos.Value.Y + 25;
                    Console.WriteLine($"[PREVIEW] Pozisyon: {Left}, {Top}");
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
                _pendingText = text ?? "";
                _debounceTimer?.Stop();
                _debounceTimer?.Start();
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
                Visibility = Visibility.Hidden;
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
    }
}