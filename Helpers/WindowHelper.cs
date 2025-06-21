using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Helpers
{
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetCaretPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Aktif pencerenin başlığını alır
        /// </summary>
        public static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                int length = GetWindowTextLength(handle);
                if (length == 0) return string.Empty;

                StringBuilder builder = new StringBuilder(length + 1);
                GetWindowText(handle, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Aktif pencerenin process adını alır
        /// </summary>
        public static string GetActiveWindowProcessName()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                GetWindowThreadProcessId(handle, out uint processId);
                
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Aktif pencere bu uygulamanın kendisi mi kontrol eder
        /// </summary>
        public static bool IsActiveWindowThisApplication()
        {
            try
            {
                string activeProcessName = GetActiveWindowProcessName();
                string currentProcessName = Process.GetCurrentProcess().ProcessName;
                
                return string.Equals(activeProcessName, currentProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Aktif pencere bu uygulamanın ana penceresi mi kontrol eder
        /// </summary>
        public static bool IsActiveWindowMainWindow()
        {
            try
            {
                string activeTitle = GetActiveWindowTitle();
                
                // Ana pencere başlığını kontrol et
                return activeTitle.Contains("Otomatik Metin Genişletici", StringComparison.OrdinalIgnoreCase) ||
                       activeTitle.Contains("Gelişmiş Otomatik Metin Genişletici", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Metin tamamlamanın aktif olup olmayacağını belirler
        /// </summary>
        public static bool ShouldTextExpansionBeActive()
        {
            try
            {
                // Eğer aktif pencere bu uygulama ise metin tamamlamayı devre dışı bırak
                if (IsActiveWindowThisApplication())
                {
                    return false;
                }

                // Diğer durumlarda aktif
                return true;
            }
            catch
            {
                // Hata durumunda güvenli tarafta kal - devre dışı bırak
                return false;
            }
        }

        /// <summary>
        /// Pencere filtrelerine göre metin tamamlamanın aktif olup olmayacağını belirler
        /// </summary>
        public static bool ShouldTextExpansionBeActive(IEnumerable<WindowFilter>? windowFilters, bool windowFilteringEnabled = true, WindowFilterMode filterMode = WindowFilterMode.AllowList)
        {
            try
            {
                // Eğer aktif pencere bu uygulama ise metin tamamlamayı devre dışı bırak
                if (IsActiveWindowThisApplication())
                {
                    return false;
                }

                // Pencere filtreleme devre dışıysa her zaman aktif
                if (!windowFilteringEnabled || windowFilters == null)
                {
                    return true;
                }

                // Aktif pencere bilgilerini al
                string activeTitle = GetActiveWindowTitle();
                string activeProcessName = GetActiveWindowProcessName();

                Console.WriteLine($"[WINDOW FILTER] Aktif pencere: '{activeTitle}', Process: '{activeProcessName}', Mod: {filterMode}");

                // Aktif filtreler arasında eşleşme var mı kontrol et
                var activeFilters = windowFilters.Where(f => f.IsEnabled).ToList();

                // Eğer hiçbir aktif filtre yoksa
                if (!activeFilters.Any())
                {
                    Console.WriteLine($"[WINDOW FILTER] Hiçbir aktif filtre yok - Metin tamamlama AKTİF");
                    return true;
                }

                bool hasMatch = false;
                foreach (var filter in activeFilters)
                {
                    if (IsWindowMatchingFilter(activeTitle, activeProcessName, filter))
                    {
                        hasMatch = true;
                        Console.WriteLine($"[WINDOW FILTER] Eşleşen filtre bulundu: '{filter.Name}'");
                        break;
                    }
                }

                // Filtreleme moduna göre karar ver
                bool result = filterMode switch
                {
                    WindowFilterMode.AllowList => hasMatch,  // İzin listesi: eşleşme varsa aktif
                    WindowFilterMode.BlockList => !hasMatch, // Engel listesi: eşleşme yoksa aktif
                    _ => hasMatch
                };

                Console.WriteLine($"[WINDOW FILTER] Sonuç: {(result ? "AKTİF" : "PASİF")} (Eşleşme: {hasMatch}, Mod: {filterMode})");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShouldTextExpansionBeActive hatası: {ex.Message}");
                // Hata durumunda güvenli tarafta kal - aktif
                return true;
            }
        }

        /// <summary>
        /// Aktif penceredeki cursor pozisyonunu ekran koordinatlarında alır
        /// </summary>
        public static POINT? GetCaretPosition()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return null;

                uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
                uint currentThreadId = GetCurrentThreadId();

                if (foregroundThreadId != currentThreadId)
                {
                    // Farklı thread'e attach ol
                    if (!AttachThreadInput(currentThreadId, foregroundThreadId, true))
                        return null;
                }

                try
                {
                    IntPtr focusedWindow = GetFocus();
                    if (focusedWindow == IntPtr.Zero)
                        return null;

                    if (GetCaretPos(out POINT caretPos))
                    {
                        // Client koordinatlarını ekran koordinatlarına çevir
                        if (ClientToScreen(focusedWindow, ref caretPos))
                        {
                            return caretPos;
                        }
                    }
                }
                finally
                {
                    if (foregroundThreadId != currentThreadId)
                    {
                        // Thread attach'ı kaldır
                        AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Açık pencerelerin listesini alır
        /// </summary>
        public static List<OpenWindow> GetOpenWindows()
        {
            var windows = new List<OpenWindow>();

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // Görünür pencereler ve başlığı olan pencereler
                        if (IsWindowVisible(hWnd))
                        {
                            var title = GetWindowTitle(hWnd);
                            if (!string.IsNullOrEmpty(title) && title.Trim().Length > 0)
                            {
                                GetWindowThreadProcessId(hWnd, out uint processId);
                                var process = Process.GetProcessById((int)processId);

                                // Kendi uygulamamızı listeden çıkar
                                if (process.ProcessName.Equals("OtomatikMetinGenisletici", StringComparison.OrdinalIgnoreCase))
                                    return true;

                                var window = new OpenWindow
                                {
                                    Handle = hWnd,
                                    Title = title,
                                    ProcessName = process.ProcessName,
                                    ProcessPath = process.MainModule?.FileName ?? ""
                                };

                                windows.Add(window);
                            }
                        }
                    }
                    catch
                    {
                        // Hata durumunda bu pencereyi atla
                    }

                    return true; // Devam et
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetOpenWindows hatası: {ex.Message}");
            }

            return windows.OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            var buff = new StringBuilder(nChars);
            return GetWindowText(hWnd, buff, nChars) > 0 ? buff.ToString() : "";
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// Pencere bilgilerinin filtreyle eşleşip eşleşmediğini kontrol eder
        /// </summary>
        private static bool IsWindowMatchingFilter(string windowTitle, string processName, WindowFilter filter)
        {
            try
            {
                switch (filter.FilterType)
                {
                    case WindowFilterType.TitleContains:
                        return ContainsMatch(windowTitle, filter.TitlePattern, filter.IsRegex);

                    case WindowFilterType.TitleEquals:
                        return EqualsMatch(windowTitle, filter.TitlePattern, filter.IsRegex);

                    case WindowFilterType.TitleStartsWith:
                        return StartsWithMatch(windowTitle, filter.TitlePattern, filter.IsRegex);

                    case WindowFilterType.TitleEndsWith:
                        return EndsWithMatch(windowTitle, filter.TitlePattern, filter.IsRegex);

                    case WindowFilterType.ProcessEquals:
                        return EqualsMatch(processName, filter.ProcessName, filter.IsRegex);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] IsWindowMatchingFilter hatası: {ex.Message}");
                return false;
            }
        }

        private static bool ContainsMatch(string text, string pattern, bool isRegex)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            if (isRegex)
            {
                try
                {
                    return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsMatch(string text, string pattern, bool isRegex)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            if (isRegex)
            {
                try
                {
                    return Regex.IsMatch(text, $"^{pattern}$", RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithMatch(string text, string pattern, bool isRegex)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            if (isRegex)
            {
                try
                {
                    return Regex.IsMatch(text, $"^{pattern}", RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return text.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EndsWithMatch(string text, string pattern, bool isRegex)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            if (isRegex)
            {
                try
                {
                    return Regex.IsMatch(text, $"{pattern}$", RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return text.EndsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Aktif pencere bilgilerini detaylı şekilde alır
        /// </summary>
        public static (string Title, string ProcessName, string ProcessPath) GetActiveWindowDetails()
        {
            try
            {
                string title = GetActiveWindowTitle();
                string processName = GetActiveWindowProcessName();
                string processPath = "";

                try
                {
                    IntPtr handle = GetForegroundWindow();
                    GetWindowThreadProcessId(handle, out uint processId);
                    Process process = Process.GetProcessById((int)processId);
                    processPath = process.MainModule?.FileName ?? "";
                }
                catch
                {
                    processPath = "";
                }

                return (title, processName, processPath);
            }
            catch
            {
                return ("", "", "");
            }
        }
    }
}
