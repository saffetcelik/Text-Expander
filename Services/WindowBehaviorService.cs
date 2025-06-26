using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OtomatikMetinGenisletici.Services
{
    public interface IWindowBehaviorService
    {
        void ApplyAdvancedWindowBehavior(Window window);
        void SetWindowNoActivate(Window window);
        void SetWindowToolWindow(Window window);
        void SetWindowOpacity(Window window, double opacity);
        void StartAutoHideTimer(Window window, int delaySeconds = 5);
        void StopAutoHideTimer();
    }

    public class WindowBehaviorService : IWindowBehaviorService
    {
        private DispatcherTimer? _autoHideTimer;
        private Window? _targetWindow;

        // Windows API constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public void ApplyAdvancedWindowBehavior(Window window)
        {
            try
            {
                Console.WriteLine("[WINDOW BEHAVIOR] Applying advanced window behavior...");

                // Apply all advanced behaviors
                SetWindowNoActivate(window);
                SetWindowToolWindow(window);
                SetWindowOpacity(window, 0.95);

                // Ensure window is topmost
                window.Topmost = true;

                Console.WriteLine("[WINDOW BEHAVIOR] Advanced window behavior applied successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ApplyAdvancedWindowBehavior hatası: {ex.Message}");
            }
        }

        public void SetWindowNoActivate(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle != IntPtr.Zero)
                {
                    int currentStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, currentStyle | WS_EX_NOACTIVATE);
                    Console.WriteLine("[WINDOW BEHAVIOR] WS_EX_NOACTIVATE applied");
                }
                else
                {
                    // If window is not yet created, apply when it's loaded
                    window.SourceInitialized += (s, e) =>
                    {
                        var h = new WindowInteropHelper(window);
                        if (h.Handle != IntPtr.Zero)
                        {
                            int currentStyle = GetWindowLong(h.Handle, GWL_EXSTYLE);
                            SetWindowLong(h.Handle, GWL_EXSTYLE, currentStyle | WS_EX_NOACTIVATE);
                            Console.WriteLine("[WINDOW BEHAVIOR] WS_EX_NOACTIVATE applied (delayed)");
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetWindowNoActivate hatası: {ex.Message}");
            }
        }

        public void SetWindowToolWindow(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle != IntPtr.Zero)
                {
                    int currentStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, currentStyle | WS_EX_TOOLWINDOW);
                    Console.WriteLine("[WINDOW BEHAVIOR] WS_EX_TOOLWINDOW applied");
                }
                else
                {
                    // If window is not yet created, apply when it's loaded
                    window.SourceInitialized += (s, e) =>
                    {
                        var h = new WindowInteropHelper(window);
                        if (h.Handle != IntPtr.Zero)
                        {
                            int currentStyle = GetWindowLong(h.Handle, GWL_EXSTYLE);
                            SetWindowLong(h.Handle, GWL_EXSTYLE, currentStyle | WS_EX_TOOLWINDOW);
                            Console.WriteLine("[WINDOW BEHAVIOR] WS_EX_TOOLWINDOW applied (delayed)");
                        }
                    };
                }

                // Also ensure it doesn't show in taskbar
                window.ShowInTaskbar = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetWindowToolWindow hatası: {ex.Message}");
            }
        }

        public void SetWindowOpacity(Window window, double opacity)
        {
            try
            {
                window.Opacity = Math.Max(0.1, Math.Min(1.0, opacity));
                Console.WriteLine($"[WINDOW BEHAVIOR] Opacity set to {window.Opacity:P0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetWindowOpacity hatası: {ex.Message}");
            }
        }

        public void StartAutoHideTimer(Window window, int delaySeconds = 5)
        {
            try
            {
                StopAutoHideTimer(); // Stop any existing timer

                _targetWindow = window;
                _autoHideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(delaySeconds)
                };

                _autoHideTimer.Tick += (s, e) =>
                {
                    try
                    {
                        StopAutoHideTimer();
                        if (_targetWindow != null && _targetWindow.IsVisible)
                        {
                            Console.WriteLine("[WINDOW BEHAVIOR] Auto-hide timer triggered");
                            _targetWindow.Hide();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Auto-hide timer tick hatası: {ex.Message}");
                    }
                };

                _autoHideTimer.Start();
                Console.WriteLine($"[WINDOW BEHAVIOR] Auto-hide timer started ({delaySeconds}s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] StartAutoHideTimer hatası: {ex.Message}");
            }
        }

        public void StopAutoHideTimer()
        {
            try
            {
                if (_autoHideTimer != null)
                {
                    _autoHideTimer.Stop();
                    _autoHideTimer = null;
                    Console.WriteLine("[WINDOW BEHAVIOR] Auto-hide timer stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] StopAutoHideTimer hatası: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopAutoHideTimer();
            _targetWindow = null;
        }
    }
}
