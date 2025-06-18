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


    }
}
