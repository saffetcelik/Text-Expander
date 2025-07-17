using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = System.Drawing.Point;

namespace OtomatikMetinGenisletici.Services
{
    public interface IImageRecognitionService : IDisposable
    {
        Point? FindCaretByImageRecognition();
        bool IsEnabled { get; set; }
    }

    public class ImageRecognitionService : IImageRecognitionService
    {
        private Mat? _templateCache = null;
        private Bitmap? _screenshotBuffer = null;
        private readonly object _templateLock = new object();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public bool IsEnabled { get; set; } = true;

        public Point? FindCaretByImageRecognition()
        {
            if (!IsEnabled)
                return null;

            try
            {
                // Aktif pencereyi al
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                // Pencere başlığını kontrol et
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, 256);
                string title = windowText.ToString();

                Console.WriteLine($"[IMAGE RECOGNITION] Aktif pencere: '{title}'");

                // .UDF içeren pencereler veya Döküman Editörü için çalış
                if (!title.Contains(".UDF") && !title.Contains("Döküman Editörü"))
                {
                    Console.WriteLine($"[IMAGE RECOGNITION] Pencere '.UDF' veya 'Döküman Editörü' içermiyor, atlanıyor");
                    return null;
                }

                if (title.Contains(".UDF"))
                {
                    Console.WriteLine($"[IMAGE RECOGNITION] .UDF penceresi tespit edildi: '{title}'");
                }
                else if (title.Contains("Döküman Editörü"))
                {
                    Console.WriteLine($"[IMAGE RECOGNITION] Döküman Editörü penceresi tespit edildi: '{title}'");
                }
                Console.WriteLine($"[IMAGE RECOGNITION] Görsel tanıma başlatılıyor...");

                // Template'i al (cache'li)
                Mat? template = GetCachedTemplate();
                if (template == null || template.Empty())
                {
                    Console.WriteLine($"[IMAGE RECOGNITION] Template yüklenemedi");
                    return null;
                }

                // Pencere boyutlarını al
                RECT windowRect;
                if (!GetWindowRect(hwnd, out windowRect))
                {
                    Console.WriteLine($"[IMAGE RECOGNITION] Pencere boyutları alınamadı");
                    return null;
                }

                // Basit arama alanı hesapla (pencere içinde)
                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;

                // Arama alanını genişlettik (UDF editörü için daha geniş alan)
                int searchXStart = windowRect.Left + (windowWidth / 10);     // Sol %10
                int searchYStart = windowRect.Top + (windowHeight / 8);      // Üst %12.5
                int searchWidth = (windowWidth * 4) / 5;                     // Genişlik %80
                int searchHeight = (windowHeight * 3) / 4;                   // Yükseklik %75

                Console.WriteLine($"[IMAGE RECOGNITION] Arama alanı: {searchXStart},{searchYStart} - {searchWidth}x{searchHeight}");

                // Screenshot buffer'ı yeniden kullan
                if (_screenshotBuffer == null ||
                    _screenshotBuffer.Width != searchWidth ||
                    _screenshotBuffer.Height != searchHeight)
                {
                    _screenshotBuffer?.Dispose();
                    _screenshotBuffer = new Bitmap(searchWidth, searchHeight, PixelFormat.Format24bppRgb);
                }

                // Hızlı screenshot
                using (Graphics g = Graphics.FromImage(_screenshotBuffer))
                {
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.CopyFromScreen(searchXStart, searchYStart, 0, 0, _screenshotBuffer.Size, CopyPixelOperation.SourceCopy);
                }

                // Basit OpenCV işlemi
                double minVal = 1.0; // Varsayılan değer
                using (Mat screenshotMat = BitmapConverter.ToMat(_screenshotBuffer))
                using (Mat screenshotGray = new Mat())
                using (Mat result = new Mat())
                {
                    Cv2.CvtColor(screenshotMat, screenshotGray, ColorConversionCodes.BGR2GRAY);
                    Cv2.MatchTemplate(screenshotGray, template, result, TemplateMatchModes.SqDiffNormed);

                    double maxVal;
                    OpenCvSharp.Point minLoc, maxLoc;
                    Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);

                    Console.WriteLine($"[IMAGE RECOGNITION] Template match sonucu: minVal={minVal:F3}, threshold=0.35");

                    if (minVal <= 0.35) // Threshold'u daha da artırdık (UDF editörü için)
                    {
                        int caretX = searchXStart + minLoc.X + (template.Width / 2); // Template merkezini kullan
                        int caretY = searchYStart + minLoc.Y + template.Height + 5;  // Biraz daha aşağı

                        Console.WriteLine($"[IMAGE RECOGNITION] İmleç bulundu: {caretX},{caretY} (minVal={minVal:F3})");
                        return new Point(caretX, caretY);
                    }
                }

                Console.WriteLine($"[IMAGE RECOGNITION] İmleç bulunamadı (threshold aşıldı: {minVal:F3} > 0.35)");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] FindCaretByImageRecognition hatası: {ex.Message}");
                return null;
            }
        }

        // Template cache metodu
        private Mat? GetCachedTemplate()
        {
            if (_templateCache == null)
            {
                lock (_templateLock)
                {
                    if (_templateCache == null)
                    {
                        string templatePath = Path.Combine(AppContext.BaseDirectory, "imlec.png");
                        Console.WriteLine($"[IMAGE RECOGNITION] Template yolu: {templatePath}");

                        if (File.Exists(templatePath))
                        {
                            try
                            {
                                var fileInfo = new FileInfo(templatePath);
                                Console.WriteLine($"[IMAGE RECOGNITION] Template dosya boyutu: {fileInfo.Length} bytes");

                                _templateCache = Cv2.ImRead(templatePath, ImreadModes.Grayscale);
                                if (_templateCache != null && !_templateCache.Empty())
                                {
                                    Console.WriteLine($"[IMAGE RECOGNITION] Template başarıyla yüklendi: {_templateCache.Width}x{_templateCache.Height}");
                                }
                                else
                                {
                                    Console.WriteLine($"[ERROR] Template yüklendi ama boş veya geçersiz");
                                    _templateCache = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Template yükleme hatası: {ex.Message}");
                                _templateCache = null;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[ERROR] Template dosyası bulunamadı: {templatePath}");
                        }
                    }
                }
            }
            return _templateCache;
        }

        public void Dispose()
        {
            CleanupResources();
        }

        // Form kapatılırken cache'i temizle
        private void CleanupResources()
        {
            _templateCache?.Dispose();
            _templateCache = null;
            _screenshotBuffer?.Dispose();
            _screenshotBuffer = null;
        }
    }
}
