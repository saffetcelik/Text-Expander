using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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


    }
}
