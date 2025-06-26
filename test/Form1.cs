using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
// Add this using statement for OpenCVSharp if you install it via NuGet
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = System.Drawing.Point;

namespace TypingAssistant
{
    public partial class Form1 : Form
    {

        private Mat? _templateCache = null;
        private Bitmap? _screenshotBuffer = null;
        private readonly object _templateLock = new object();

        // Windows API için gerekli DllImport'lar
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // INPUT yapısı
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        // InputUnion yapısı
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        // MOUSEINPUT yapısı (gerekli değil ama INPUT birleşimi için tanımalıyız)
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // KEYBDINPUT yapısı
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // HARDWAREINPUT yapısı (gerekli değil ama INPUT birleşimi için tanımalıyız)
        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // dwFlags için sabitler
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        // Klavye tuşları için sanal kodlar
        private const ushort VK_CONTROL = 0x11; // Ctrl key virtual code
        private const ushort VK_V = 0x56;       // V key virtual code

        // Yeni yardımcı metotlar
        private void SimulateKeyPress(ushort virtualKeyCode)
        {
            INPUT[] inputs = new INPUT[2];

            // Key Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = virtualKeyCode;
            inputs[0].U.ki.dwFlags = 0; // Key down
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = GetMessageExtraInfo();

            // Key Up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = virtualKeyCode;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP; // Key up
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = GetMessageExtraInfo();

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SimulateCtrlV()
        {
            INPUT[] inputs = new INPUT[4];

            // Ctrl Key Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_CONTROL;
            inputs[0].U.ki.dwFlags = 0; // Key down
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = GetMessageExtraInfo();

            // V Key Down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_V;
            inputs[1].U.ki.dwFlags = 0; // Key down
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = GetMessageExtraInfo();

            // V Key Up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = VK_V;
            inputs[2].U.ki.dwFlags = KEYEVENTF_KEYUP; // Key up
            inputs[2].U.ki.time = 0;
            inputs[2].U.ki.dwExtraInfo = GetMessageExtraInfo();

            // Ctrl Key Up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = VK_CONTROL;
            inputs[3].U.ki.dwFlags = KEYEVENTF_KEYUP; // Key up
            inputs[3].U.ki.time = 0;
            inputs[3].U.ki.dwExtraInfo = GetMessageExtraInfo();

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private GlobalKeyboardHook? _globalKeyboardHook;
        private LearningClass? _learningClass;
        private StringBuilder _typedText = new StringBuilder();
        private string _currentSuggestion = "";
        private bool _processingF12 = false;

        private System.Windows.Forms.Timer _hideFormTimer; // Declare the timer
        private DateTime _suggestionShownTime; // To track when the suggestion appeared

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008; // Existing, just for reference
        public Form1()
        {
            InitializeComponent();
            //this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            //this.StartPosition = FormStartPosition.Manual;
            this.Opacity = 0.70; // Set form opacity to 70%
            this.ShowInTaskbar = false;

            _hideFormTimer = new System.Windows.Forms.Timer();
            _hideFormTimer.Interval = 5000; // 5 seconds
            _hideFormTimer.Tick += HideFormTimer_Tick;
        }
        private void HideFormTimer_Tick(object? sender, EventArgs e)
        {
            _hideFormTimer.Stop(); // Stop the timer once it fires
            this.Invoke((MethodInvoker)(() =>
            {
                this.Hide(); // Hide the form
                _currentSuggestion = string.Empty; // Clear suggestion when hiding
                UpdateUi(); // Update UI to reflect no suggestion
            }));
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_TOOLWINDOW: Ensures the form doesn't appear in the Alt-Tab dialog or taskbar.
                // WS_EX_NOACTIVATE: Ensures the form does not take activation when shown, allowing clicks/keys to pass through.
                cp.ExStyle |= (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

                // WS_VISIBLE (0x10000000) is a standard style that makes the window visible upon creation.
                // By removing it, we ensure the window is created as hidden.
                cp.Style &= ~((int)0x10000000); // Remove WS_VISIBLE

                return cp;
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            _learningClass = new LearningClass("learning_data.json");
            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyDown += GlobalKeyboardHook_KeyDown;
            // Pencerenin genişletilmiş stilini al
            int initialStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);

            // NOACTIVATE ve TOOLWINDOW stillerini ekle
            SetWindowLong(this.Handle, GWL_EXSTYLE, initialStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Formu her zaman en üstte tut (zaten mevcut olabilir)
            this.TopMost = true;

            // Başlangıçta formu gizle
            //this.Hide();
        }

        private void GlobalKeyboardHook_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_learningClass == null) return;
            
            MoveFormToCaret();

            if (_processingF12) return;

            if (e.KeyCode == Keys.F12)
            {
                if (!string.IsNullOrEmpty(_currentSuggestion))
                {
                    e.Handled = true;
                    ApplySuggestion();
                    _hideFormTimer.Stop();
                    _hideFormTimer.Start();
                }
                return;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod)
            {
                if (e.KeyCode == Keys.OemPeriod)
                {
                    _typedText.Append(".");
                }
                if (_typedText.Length > 0)
                {
                    _learningClass.Learn(_typedText.ToString());
                    _typedText.Clear();
                }
                UpdateUi();
                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                if (_typedText.Length > 0)
                {
                    _typedText.Length--;
                }
            }
            else if (e.KeyCode == Keys.Space)
            {
                _typedText.Append(" ");
            }
            else
            {
                char keyChar = KeyCodeToChar(e.KeyCode, (Control.ModifierKeys & Keys.Shift) == Keys.Shift);
                if (keyChar != '\0')
                {
                    _typedText.Append(keyChar);
                }
            }

            UpdateSuggestion();
            UpdateUi();
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        // GetClientRect için Win32 API
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private void MoveFormToCaret()
        {
            try
            {
                // Öneri yoksa direkt çık
                if (string.IsNullOrEmpty(_currentSuggestion))
                {
                    this.Invoke((MethodInvoker)(() => this.Hide()));
                    return;
                }

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, 256);
                string title = windowText.ToString();

                Point? caretPoint = null;

                // Sadece Editörü pencerelerinde image recognition
                if (title.Contains("Editörü"))
                {
                    caretPoint = FindCaretByImageRecognition();
                }

                // Bulunamazsa standart yöntem
                if (!caretPoint.HasValue)
                {
                    GUITHREADINFO guiThreadInfo = new GUITHREADINFO();
                    guiThreadInfo.cbSize = (uint)Marshal.SizeOf(guiThreadInfo);
                    uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

                    if (threadId != 0 && GetGUIThreadInfo(threadId, ref guiThreadInfo))
                    {
                        if (!(guiThreadInfo.rcCaret.Left == 0 && guiThreadInfo.rcCaret.Right == 0))
                        {
                            Point currentCaretPos = new Point(guiThreadInfo.rcCaret.Left, guiThreadInfo.rcCaret.Bottom);
                            ClientToScreen(guiThreadInfo.hwndCaret, ref currentCaretPos);
                            caretPoint = currentCaretPos;
                        }
                    }
                }

                this.Invoke((MethodInvoker)(() =>
                {
                    if (caretPoint.HasValue)
                    {
                        this.Location = new Point(caretPoint.Value.X, caretPoint.Value.Y + 5);
                        this.Show();
                    }
                    else
                    {
                        this.Hide();
                    }
                }));
            }
            catch { }
        }
        private Point? FindCaretByImageRecognition()
        {
            try
            {
                // Aktif pencereyi al
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                // Pencere başlığını kontrol et
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(hwnd, windowText, 256);
                string title = windowText.ToString();

                // Sadece "Editörü" içeren pencereler için çalış
                if (!title.Contains("Editörü"))
                {
                    return null;
                }

                // Template'i al (cache'li)
                Mat? template = GetCachedTemplate();
                if (template == null || template.Empty())
                {
                    return null;
                }

                // Pencere boyutlarını al
                RECT windowRect;
                if (!GetWindowRect(hwnd, out windowRect))
                {
                    return null;
                }

                // Basit arama alanı hesapla (pencere içinde)
                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;

                // Arama alanı (pencere içinde merkezi alan)
                int searchXStart = windowRect.Left + (windowWidth / 8);      // Sol %12.5
                int searchYStart = windowRect.Top + (windowHeight / 6);      // Üst %16.7
                int searchWidth = (windowWidth * 3) / 4;                     // Genişlik %75
                int searchHeight = (windowHeight * 2) / 3;                   // Yükseklik %66.7

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
                using (Mat screenshotMat = BitmapConverter.ToMat(_screenshotBuffer))
                using (Mat screenshotGray = new Mat())
                using (Mat result = new Mat())
                {
                    Cv2.CvtColor(screenshotMat, screenshotGray, ColorConversionCodes.BGR2GRAY);
                    Cv2.MatchTemplate(screenshotGray, template, result, TemplateMatchModes.SqDiffNormed);

                    double minVal, maxVal;
                    OpenCvSharp.Point minLoc, maxLoc;
                    Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);



                    if (minVal <= 0.15) // Basit threshold
                    {
                        int caretX = searchXStart + minLoc.X;
                        int caretY = searchYStart + minLoc.Y + template.Height + 5;
                        return new Point(caretX, caretY);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
        // Template cache metodu
        // Basit template cache
        private Mat? GetCachedTemplate()
        {
            if (_templateCache == null)
            {
                lock (_templateLock)
                {
                    if (_templateCache == null)
                    {
                        string templatePath = Path.Combine(AppContext.BaseDirectory, "imlec.png");
                        if (File.Exists(templatePath))
                        {
                            _templateCache = Cv2.ImRead(templatePath, ImreadModes.Grayscale);
                        }
                    }
                }
            }
            return _templateCache;
        }
        // Form kapatılırken cache'i temizle
        private void CleanupResources()
        {
            _templateCache?.Dispose();
            _templateCache = null;
            _screenshotBuffer?.Dispose();
            _screenshotBuffer = null;
        }

        private void ApplySuggestion()
        {
            if (_learningClass == null) return;
            
            _processingF12 = true;

            string textToPaste = "";
            string currentText = _typedText.ToString();
            string[] words = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (currentText.Length > 0 && !char.IsWhiteSpace(currentText.Last()) && char.IsLetterOrDigit(_currentSuggestion.FirstOrDefault()))
            {
                string lastWord = words.Last();
                if (_currentSuggestion.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                {
                    textToPaste = _currentSuggestion.Substring(lastWord.Length);
                }
                else
                {
                    if (!currentText.EndsWith(" "))
                    {
                        textToPaste += " ";
                    }
                    textToPaste += _currentSuggestion;
                }
            }
            else
            {
                textToPaste = _currentSuggestion;
            }

            // Öneri kelimesinden sonra bir boşluk ekleyelim.
            textToPaste += " ";


            Thread thread = new Thread(() =>
            {
                // Orijinal pano içeriğini sakla
                string? originalClipboardText = null;
                try
                {
                    // Try/Catch bloğu ekledik, çünkü bazı durumlarda clipboard'a erişim hata verebilir.
                    if (Clipboard.ContainsText())
                    {
                        originalClipboardText = Clipboard.GetText();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Pano içeriği okunurken hata: {ex.Message}");
                    // Hata durumunda originalClipboardText null kalacak
                }

                // Önerilen metni panoya kopyala
                try
                {
                    Clipboard.SetText(textToPaste);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Panoya yazarken hata: {ex.Message}");
                    this.Invoke((MethodInvoker)delegate { _processingF12 = false; });
                    return; // Hata olursa işlemi sonlandır
                }

                // Ctrl+V tuş kombinasyonunu simüle et
                SimulateCtrlV();

                // Kısa bir gecikme ekleyelim (Uyap'ta test edilebilir, gerekirse ayarlayın)
                Thread.Sleep(50); // Örneğin 50ms, Uyap'ın tuş vuruşlarını işlemesi için zaman tanır

                // Orijinal pano içeriğini geri yükle
                if (originalClipboardText != null)
                {
                    try
                    {
                        Clipboard.SetText(originalClipboardText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Orijinal pano içeriği geri yüklenirken hata: {ex.Message}");
                    }
                }

                this.Invoke((MethodInvoker)delegate
                {
                    _typedText.Append(textToPaste);
                    _learningClass.Learn(_typedText.ToString());
                    UpdateSuggestion(); // Yeni typedText'e göre öneriyi güncelle
                    UpdateUi();
                    _processingF12 = false;
                });
            });
            thread.SetApartmentState(ApartmentState.STA); // Pano işlemleri için STA gereklidir
            thread.Start();
        }

        private void UpdateSuggestion()
        {
            if (_learningClass == null) return;
            string text = _typedText.ToString();

            if (string.IsNullOrWhiteSpace(text) || text.Length <= 2)
            {
                _currentSuggestion = "";
                _hideFormTimer.Stop();
                this.Invoke((MethodInvoker)(() => this.Hide()));
                return;
            }

            string newSuggestion = _learningClass.GetSuggestion(text);

            // Sadece öneri değiştiyse işlem yap
            if (_currentSuggestion != newSuggestion)
            {
                _currentSuggestion = newSuggestion;

                if (!string.IsNullOrEmpty(_currentSuggestion))
                {
                    _suggestionShownTime = DateTime.Now;
                    _hideFormTimer.Stop();
                    _hideFormTimer.Start();
                    // Form konumunu güncelle
                    Task.Run(() => MoveFormToCaret()); // Async olarak çalıştır
                }
                else
                {
                    _hideFormTimer.Stop();
                    this.Invoke((MethodInvoker)(() => this.Hide()));
                }
            }
        }
        private void UpdateUi()
        {
            if (this.IsDisposed) return;
            this.Invoke((MethodInvoker)delegate
            {
                lblSuggestion.Text = _currentSuggestion;
                // The form's visibility is now managed by MoveFormToCaret and HideFormTimer_Tick
                // We only show it in MoveFormToCaret if a caret is found and a suggestion is present.
                // We hide it via the timer or when there's no suggestion/caret.
            });
        }

        private char KeyCodeToChar(Keys keyCode, bool shiftPressed)
        {
            var keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            if (shiftPressed)
            {
                keyboardState[(int)Keys.ShiftKey] = 0x80;
            }
            else
            {
                keyboardState[(int)Keys.ShiftKey] = 0;
            }

            var resultBuilder = new StringBuilder(2);
            var virtualKeyCode = (uint)keyCode;
            var scanCode = MapVirtualKey(virtualKeyCode, 0);
            var inputLocaleIdentifier = GetKeyboardLayout(0);

            int result = ToUnicodeEx(virtualKeyCode, scanCode, keyboardState, resultBuilder, resultBuilder.Capacity, 0, inputLocaleIdentifier);

            if (result > 0)
            {
                return resultBuilder[0];
            }

            return '\0';
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            //_typedText.Clear();
            //_currentSuggestion = "";
            //UpdateUi();
        }

        private void btnLearn_Click(object sender, EventArgs e)
        {
            // If textBoxInput is removed, you'll need another way to get text for learning.
            // For now, I'm commenting this out, as it would cause an error.
            // If you intend to have a manual "learn" feature, you'd need a different input mechanism.
            // _learningClass.Learn(textBoxInput.Text); 
            //_typedText.Clear();
           // UpdateUi();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _globalKeyboardHook?.Dispose();
            _learningClass?.Save();
            CleanupResources(); // Bu satırı ekleyin
        }

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }

    public class LearningClass
    {
        private readonly string _filePath;
        private Dictionary<string, Dictionary<string, int>> _ngrams;

        public LearningClass(string filePath)
        {
            _filePath = filePath;
            _ngrams = new Dictionary<string, Dictionary<string, int>>();
            Load();
        }

        public void Learn(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] words = Tokenize(text);
            if (words.Length == 0) return;

            for (int i = 0; i < words.Length; i++)
            {
                for (int n = 1; n <= 4; n++)
                {
                    if (i >= n - 1)
                    {
                        string context = string.Join(" ", words.Skip(i - n + 1).Take(n - 1));
                        string nextWord = words[i];

                        if (string.IsNullOrEmpty(context))
                        {
                            context = "___START___";
                        }

                        if (!_ngrams.ContainsKey(context))
                        {
                            _ngrams[context] = new Dictionary<string, int>();
                        }
                        if (!_ngrams[context].ContainsKey(nextWord))
                        {
                            _ngrams[context][nextWord] = 0;
                        }
                        _ngrams[context][nextWord]++;
                    }
                }
            }
            Save();
        }

        public string GetSuggestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string[] words = Tokenize(text);
            string? lastWord = words.LastOrDefault();

            for (int n = 4; n >= 2; n--)
            {
                if (words.Length >= n - 1)
                {
                    string context = string.Join(" ", words.Skip(words.Length - n + 1).Take(n - 1));
                    if (_ngrams.ContainsKey(context))
                    {
                        var suggestions = _ngrams[context].OrderByDescending(kvp => kvp.Value);
                        if (suggestions.Any()) return suggestions.First().Key;
                    }
                }
            }

            if (!string.IsNullOrEmpty(lastWord) && !text.EndsWith(" ") && !IsPunctuation(lastWord.Last()))
            {
                var allWords = _ngrams.SelectMany(g => g.Value.Keys).Distinct();
                var completions = allWords
                   .Where(k => k.StartsWith(lastWord, StringComparison.Ordinal))
                   .OrderBy(k => k.Length)
                   .ToList();
                if (completions.Any() && completions.First() != lastWord) return completions.First();
            }

            if (words.Length >= 1)
            {
                string context = words.Last();
                if (_ngrams.ContainsKey(context))
                {
                    var suggestions = _ngrams[context].OrderByDescending(kvp => kvp.Value);
                    if (suggestions.Any()) return suggestions.First().Key;
                }
            }

            return "";
        }

        private bool IsPunctuation(char c)
        {
            return char.IsPunctuation(c);
        }

        private string[] Tokenize(string text)
        {
            var tokens = new List<string>();
            var currentToken = new StringBuilder();

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                }
                else if (IsPunctuation(c))
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    currentToken.Append(c);
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens.ToArray();
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_ngrams, Formatting.Indented);
                File.WriteAllText(_filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving learning data: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath, Encoding.UTF8);
                    var loadedNgrams = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json);
                    _ngrams = loadedNgrams ?? new Dictionary<string, Dictionary<string, int>>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading learning data: {ex.Message}");
                _ngrams = new Dictionary<string, Dictionary<string, int>>();
            }
        }
    }

    public class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private IntPtr _hookID = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _proc;

        public event KeyEventHandler? KeyDown;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName!), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                KeyEventArgs e = new KeyEventArgs((Keys)vkCode);
                KeyDown?.Invoke(this, e);
                if (e.Handled)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~GlobalKeyboardHook()
        {
            Dispose();
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}