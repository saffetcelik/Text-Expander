using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici.Views
{
    public partial class PreviewOverlay : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public PreviewOverlay()
        {
            try
            {
                Console.WriteLine("[PREVIEW] PreviewOverlay constructor başlıyor...");
                InitializeComponent();

                // ESC tuşu ile kapatma özelliği
                KeyDown += PreviewOverlay_KeyDown;

                Hide();
                Console.WriteLine("[PREVIEW] PreviewOverlay constructor tamamlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PreviewOverlay constructor hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public void SetText(string text)
        {
            try
            {
                Console.WriteLine($"[PREVIEW] SetText çağrıldı: '{text}'");

                // Pencere kapalı mı kontrol et - sadece ilk açılışta
                if (!IsLoaded)
                {
                    Console.WriteLine("[PREVIEW] Pencere henüz yüklenmemiş, yükleniyor...");
                    Show();
                    PositionNearCursor();
                    Topmost = true;
                    Opacity = 0.95; // Sabit opacity
                }

                // Eski format kontrolü ve yeni formata çevirme
                ParseAndDisplayText(text);

                // Pencere her zaman görünür ve cursor pozisyonunda kalıyor
                if (Visibility != Visibility.Visible)
                {
                    Show();
                    PositionNearCursor();
                    Topmost = true;
                    Opacity = 0.95;
                }
                else
                {
                    // Pencere zaten açıksa sadece pozisyonu güncelle
                    PositionNearCursor();
                }

                Console.WriteLine($"[PREVIEW] Pencere pozisyonu: Left={Left}, Top={Top}, Width={Width}, Height={Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetText hatası: {ex.Message}");
            }
        }

        private void ParseAndDisplayText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    Console.WriteLine("[PREVIEW] Metin boş, gizleniyor");
                    PreviewTextBlock.Text = "";
                    return;
                }

                // Sadece ana metni al, tüm bilgi yazılarını kaldır
                string mainText = "";

                // Farklı formatları kontrol et ve sadece ana metni çıkar
                if (text.Contains("(Tab") || text.Contains("(Ctrl+Space"))
                {
                    // Format: "💡 öneri metni (Tab - %80)"
                    var parts = text.Split('(');
                    if (parts.Length >= 2)
                    {
                        mainText = parts[0].Trim();
                    }
                }
                else
                {
                    // Basit metin
                    mainText = text;
                }

                // Tüm emoji'leri ve gereksiz karakterleri temizle
                mainText = mainText.Replace("💡", "").Replace("🔤", "").Replace("🔮", "").Replace("→", "")
                                  .Replace("Önerilen:", "").Replace("|", "").Replace("Kelime:", "")
                                  .Replace("Karakter:", "").Trim();

                // Sadece temiz metni göster
                PreviewTextBlock.Text = mainText;

                Console.WriteLine($"[PREVIEW] Minimal format - Sadece metin: '{mainText}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ParseAndDisplayText hatası: {ex.Message}");
                PreviewTextBlock.Text = text; // Fallback
            }
        }

        private void ShowWithAnimation()
        {
            Console.WriteLine($"[PREVIEW] ShowWithAnimation çağrıldı, Visibility: {Visibility}");

            if (Visibility != Visibility.Visible)
            {
                Console.WriteLine("[PREVIEW] Pencere gösteriliyor");

                // Önce pozisyonu ayarla
                PositionNearCursor();
                Show();

                // Yukarıdan aşağıya kayma animasyonu
                var slideAnimation = new DoubleAnimation
                {
                    From = Top - 30, // Mevcut pozisyondan 30 pixel yukarıdan başla
                    To = Top,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // Fade in animasyonu
                var fadeAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 0.95,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                BeginAnimation(TopProperty, slideAnimation);
                BeginAnimation(OpacityProperty, fadeAnimation);

                Console.WriteLine("[PREVIEW] Animasyonlar başlatıldı");
            }
            else
            {
                Console.WriteLine("[PREVIEW] Pencere zaten görünür");
                // Pencere zaten açıksa sadece pozisyonu güncelle
                PositionNearCursor();
            }
        }

        private void HideWithAnimation()
        {
            if (Visibility == Visibility.Visible)
            {
                // Fade out animasyonu
                var fadeAnimation = new DoubleAnimation
                {
                    From = 0.95,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250)
                };

                fadeAnimation.Completed += (s, e) => Hide();
                BeginAnimation(OpacityProperty, fadeAnimation);
            }
        }

        private void PositionNearCursor()
        {
            try
            {
                // Cursor pozisyonunu al
                var caretPos = WindowHelper.GetCaretPosition();

                if (caretPos.HasValue)
                {
                    Console.WriteLine($"[PREVIEW] Cursor pozisyonu bulundu: X={caretPos.Value.X}, Y={caretPos.Value.Y}");

                    // Pencere boyutunu hesapla
                    Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var desiredSize = DesiredSize;

                    double windowWidth = desiredSize.Width > 0 ? desiredSize.Width : 450;
                    double windowHeight = desiredSize.Height > 0 ? desiredSize.Height : 140;

                    // Cursor'un tam sağ altına yerleştir
                    double targetLeft = caretPos.Value.X + 10; // Cursor'un 10 pixel sağında
                    double targetTop = caretPos.Value.Y + 20; // Cursor'un 20 pixel altında

                    // Ekran sınırlarını kontrol et
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    if (primaryScreen?.WorkingArea != null)
                    {
                        // Sol sınır kontrolü
                        if (targetLeft < primaryScreen.WorkingArea.Left)
                            targetLeft = primaryScreen.WorkingArea.Left + 10;

                        // Sağ sınır kontrolü
                        if (targetLeft + windowWidth > primaryScreen.WorkingArea.Right)
                            targetLeft = primaryScreen.WorkingArea.Right - windowWidth - 10;

                        // Alt sınır kontrolü - eğer cursor çok aşağıdaysa üste taşı
                        if (targetTop + windowHeight > primaryScreen.WorkingArea.Bottom)
                            targetTop = caretPos.Value.Y - windowHeight - 10; // Cursor'un üstüne

                        // Üst sınır kontrolü
                        if (targetTop < primaryScreen.WorkingArea.Top)
                            targetTop = primaryScreen.WorkingArea.Top + 10;
                    }

                    Left = targetLeft;
                    Top = targetTop;

                    Console.WriteLine($"[PREVIEW] Cursor yakını pozisyon: Left={Left}, Top={Top}");
                }
                else
                {
                    Console.WriteLine("[PREVIEW] Cursor pozisyonu alınamadı, fallback pozisyon kullanılıyor");
                    PositionAtTopCenter();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PositionNearCursor hatası: {ex.Message}");
                PositionAtTopCenter();
            }
        }

        private void PositionAtTopCenter()
        {
            try
            {
                // Birincil ekranı al
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen?.WorkingArea != null)
                {
                    // Pencere boyutunu hesapla
                    Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var desiredSize = DesiredSize;

                    double windowWidth = desiredSize.Width > 0 ? desiredSize.Width : 450; // Varsayılan genişlik artırıldı
                    double windowHeight = desiredSize.Height > 0 ? desiredSize.Height : 140; // Varsayılan yükseklik artırıldı

                    // Ekranın üst ortasına yerleştir - biraz daha aşağıda
                    Left = (primaryScreen.WorkingArea.Width - windowWidth) / 2;
                    Top = 120; // Ekranın üstünden 120 pixel aşağıda (daha fazla boşluk)

                    Console.WriteLine($"[PREVIEW] Ekran boyutu: {primaryScreen.WorkingArea.Width}x{primaryScreen.WorkingArea.Height}");
                    Console.WriteLine($"[PREVIEW] Pencere boyutu: {windowWidth}x{windowHeight}");
                    Console.WriteLine($"[PREVIEW] Hesaplanan pozisyon: Left={Left}, Top={Top}");
                }
                else
                {
                    // Fallback pozisyon - daha merkezi
                    Left = 300;
                    Top = 120;
                    Console.WriteLine("[PREVIEW] Fallback pozisyon kullanıldı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PositionAtTopCenter hatası: {ex.Message}");
                // Fallback pozisyon
                Left = 300;
                Top = 120;
            }
        }

        public void HidePreview()
        {
            try
            {
                Console.WriteLine("[PREVIEW] HidePreview çağrıldı - pencere gizleniyor");
                HideWithAnimation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] HidePreview hatası: {ex.Message}");
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("[PREVIEW] Gizle butonu tıklandı - kullanıcı tarafından gizleniyor");
                HideWithAnimation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] HideButton_Click hatası: {ex.Message}");
            }
        }

        // Sürükleme özelliği için event handler
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    Console.WriteLine("[PREVIEW] Sürükleme başlatıldı");
                    this.DragMove();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Border_MouseLeftButtonDown hatası: {ex.Message}");
            }
        }

        // ESC tuşu ile kapatma
        private void PreviewOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Escape)
                {
                    Console.WriteLine("[PREVIEW] ESC tuşu basıldı - önizleme gizleniyor");
                    HideWithAnimation();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PreviewOverlay_KeyDown hatası: {ex.Message}");
            }
        }
    }
}
