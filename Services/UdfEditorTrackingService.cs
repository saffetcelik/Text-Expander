using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OtomatikMetinGenisletici.Services
{
    public interface IUdfEditorTrackingService
    {
        event EventHandler<bool>? UdfEditorVisibilityChanged;
        void StartTracking();
        void StopTracking();
        bool IsUdfEditorVisible { get; }
    }

    public class UdfEditorTrackingService : IUdfEditorTrackingService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private DispatcherTimer? _trackingTimer;
        private bool _isTracking = false;
        private bool _lastUdfEditorVisible = false;

        public event EventHandler<bool>? UdfEditorVisibilityChanged;
        public bool IsUdfEditorVisible { get; private set; } = false;

        public void StartTracking()
        {
            if (_isTracking) return;

            _isTracking = true;
            _trackingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 200ms interval - daha hızlı tepki
            };
            _trackingTimer.Tick += TrackingTimer_Tick;
            _trackingTimer.Start();

            Console.WriteLine("[UDF TRACKING] UDF editörü takibi başlatıldı");
        }

        public void StopTracking()
        {
            if (!_isTracking) return;

            _isTracking = false;
            _trackingTimer?.Stop();
            _trackingTimer = null;

            Console.WriteLine("[UDF TRACKING] UDF editörü takibi durduruldu");
        }

        private void TrackingTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                bool currentUdfVisible = CheckUdfEditorVisibility();
                
                if (currentUdfVisible != _lastUdfEditorVisible)
                {
                    _lastUdfEditorVisible = currentUdfVisible;
                    IsUdfEditorVisible = currentUdfVisible;
                    
                    Console.WriteLine($"[UDF TRACKING] UDF editörü görünürlük değişti: {currentUdfVisible}");
                    UdfEditorVisibilityChanged?.Invoke(this, currentUdfVisible);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UDF tracking hatası: {ex.Message}");
            }
        }

        private bool CheckUdfEditorVisibility()
        {
            bool udfFound = false;

            try
            {
                // Tüm pencereleri tara
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // Pencere görünür mü kontrol et
                        if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
                            return true; // Continue enumeration

                        // Pencere başlığını al
                        StringBuilder windowText = new StringBuilder(256);
                        int length = GetWindowText(hWnd, windowText, windowText.Capacity);
                        
                        if (length > 0)
                        {
                            string title = windowText.ToString();
                            
                            // .UDF içeren pencere var mı kontrol et (daha spesifik filtreleme)
                            if (title.Contains(".UDF", StringComparison.OrdinalIgnoreCase) &&
                                (title.Length > 4) && // En az ".UDF" + bir karakter
                                !title.Contains("Text-Expander", StringComparison.OrdinalIgnoreCase)) // Kendi uygulamamızı hariç tut
                            {
                                Console.WriteLine($"[UDF TRACKING] UDF editörü bulundu: '{title}'");
                                udfFound = true;
                                return false; // Stop enumeration
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Pencere kontrol hatası: {ex.Message}");
                    }
                    
                    return true; // Continue enumeration
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] EnumWindows hatası: {ex.Message}");
            }

            return udfFound;
        }
    }
}
