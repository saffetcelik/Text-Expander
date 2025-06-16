using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

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
                    PositionAtTopCenter();
                    Topmost = true;
                    Opacity = 0.95; // Sabit opacity
                }

                PreviewTextBlock.Text = text;

                // EPİLEPSİ ÖNLEMİ: Artık gizlenip gösterilmiyor, sadece metin güncelleniyor
                if (string.IsNullOrEmpty(text))
                {
                    Console.WriteLine("[PREVIEW] Metin boş, ama pencere açık kalıyor (epilepsi önlemi)");
                    PreviewTextBlock.Text = "✏️ Yazmaya başlayın..."; // Boş yerine varsayılan metin
                }
                else
                {
                    Console.WriteLine("[PREVIEW] Metin güncellendi, pencere sabit kalıyor");
                }

                // Pencere her zaman görünür ve sabit pozisyonda kalıyor
                if (Visibility != Visibility.Visible)
                {
                    Show();
                    PositionAtTopCenter();
                    Topmost = true;
                    Opacity = 0.95;
                }

                Console.WriteLine($"[PREVIEW] Pencere pozisyonu: Left={Left}, Top={Top}, Width={Width}, Height={Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetText hatası: {ex.Message}");
            }
        }

        private void ShowWithAnimation()
        {
            Console.WriteLine($"[PREVIEW] ShowWithAnimation çağrıldı, Visibility: {Visibility}");

            if (Visibility != Visibility.Visible)
            {
                Console.WriteLine("[PREVIEW] Pencere gösteriliyor");
                Show();

                // Yukarıdan aşağıya kayma animasyonu
                var slideAnimation = new DoubleAnimation
                {
                    From = -100,
                    To = Top,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // Fade in animasyonu
                var fadeAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 0.95,
                    Duration = TimeSpan.FromMilliseconds(400)
                };

                BeginAnimation(TopProperty, slideAnimation);
                BeginAnimation(OpacityProperty, fadeAnimation);

                Console.WriteLine("[PREVIEW] Animasyonlar başlatıldı");
            }
            else
            {
                Console.WriteLine("[PREVIEW] Pencere zaten görünür");
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

                    double windowWidth = desiredSize.Width > 0 ? desiredSize.Width : 400; // Varsayılan genişlik
                    double windowHeight = desiredSize.Height > 0 ? desiredSize.Height : 120; // Varsayılan yükseklik

                    // Ekranın üst ortasına yerleştir
                    Left = (primaryScreen.WorkingArea.Width - windowWidth) / 2;
                    Top = 80; // Ekranın üstünden 80 pixel aşağıda

                    Console.WriteLine($"[PREVIEW] Ekran boyutu: {primaryScreen.WorkingArea.Width}x{primaryScreen.WorkingArea.Height}");
                    Console.WriteLine($"[PREVIEW] Pencere boyutu: {windowWidth}x{windowHeight}");
                    Console.WriteLine($"[PREVIEW] Hesaplanan pozisyon: Left={Left}, Top={Top}");
                }
                else
                {
                    // Fallback pozisyon
                    Left = 200;
                    Top = 80;
                    Console.WriteLine("[PREVIEW] Fallback pozisyon kullanıldı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PositionAtTopCenter hatası: {ex.Message}");
                // Fallback pozisyon
                Left = 200;
                Top = 80;
            }
        }

        public void HidePreview()
        {
            try
            {
                Console.WriteLine("[PREVIEW] HidePreview çağrıldı - ARTIK GİZLENMİYOR (epilepsi önlemi)");
                // ARTIK GİZLENMİYOR - Epilepsi önlemi için sürekli açık kalıyor
                // HideWithAnimation(); // Bu satır devre dışı
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
    }
}
