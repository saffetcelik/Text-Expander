using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Gma.System.MouseKeyHook;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici.Services
{
    public class KeyboardHookService : IKeyboardHookService
    {
        private IKeyboardMouseEvents? _globalHook;
        private StringBuilder _sentenceBuffer = new(); // TAM CÜMLE İÇİN
        private StringBuilder _wordBuffer = new();     // SADECE KELİME İÇİN
        private readonly HashSet<Keys> _modifierKeys = new()
        {
            Keys.Control, Keys.Alt, Keys.Shift, Keys.LWin, Keys.RWin
        };

        // Duplicate event prevention
        private string _lastProcessedWord = string.Empty;
        private DateTime _lastWordTime = DateTime.MinValue;
        private const int WORD_COOLDOWN_MS = 100; // 100ms cooldown between same words

        private string _lastProcessedSentence = string.Empty;
        private DateTime _lastSentenceTime = DateTime.MinValue;
        private const int SENTENCE_COOLDOWN_MS = 500; // 500ms cooldown between same sentences

        // Win32 API for proper character conversion
        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff,
            uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        public event Action<string>? KeyPressed;
        public event Action<string>? WordCompleted;
        public event Action<string>? SentenceCompleted;
        public event Action? CtrlSpacePressed;
        public event Action? TabPressed;
        public event Action<string>? SpacePressed;
        public bool IsListening => _globalHook != null;

        public void StartListening()
        {
            if (_globalHook != null)
            {
                Console.WriteLine("[DEBUG] Keyboard hook already started");
                WriteToLogFile("[DEBUG] Keyboard hook already started");
                return;
            }

            try
            {
                Console.WriteLine("[DEBUG] Starting keyboard hook...");
                WriteToLogFile("[DEBUG] Starting keyboard hook...");

                _globalHook = Hook.GlobalEvents();
                _globalHook.KeyDown += OnKeyDown;

                Console.WriteLine("[DEBUG] Keyboard hook started successfully!");
                WriteToLogFile("[DEBUG] Keyboard hook started successfully!");

                // Test hook çalışıyor mu
                Console.WriteLine($"[DEBUG] Hook object: {_globalHook}");
                WriteToLogFile($"[DEBUG] Hook object: {_globalHook}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to start keyboard hook: {ex.Message}");
                WriteToLogFile($"[ERROR] Failed to start keyboard hook: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                WriteToLogFile($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private void WriteToLogFile(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keyboard_debug.log");
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Log yazma hatası olursa sessizce devam et
            }
        }

        public void StopListening()
        {
            if (_globalHook == null) return;

            _globalHook.KeyDown -= OnKeyDown;
            _globalHook.Dispose();
            _globalHook = null;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            Console.WriteLine($"[DEBUG] KeyDown: {e.KeyCode}");
            WriteToLogFile($"[DEBUG] KeyDown: {e.KeyCode}");

            // Modifier tuşlarını yoksay
            if (_modifierKeys.Contains(e.KeyCode))
            {
                Console.WriteLine($"[DEBUG] Modifier key ignored: {e.KeyCode}");
                WriteToLogFile($"[DEBUG] Modifier key ignored: {e.KeyCode}");
                return;
            }

            // Ctrl+Space kombinasyonunu kontrol et
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Space)
            {
                Console.WriteLine($"[DEBUG] Ctrl+Space detected");
                CtrlSpacePressed?.Invoke();
                return;
            }

            // Tab tuşunu kontrol et (akıllı öneriler için)
            if (e.KeyCode == Keys.Tab)
            {
                Console.WriteLine($"[DEBUG] *** TAB TUŞU ALGILANDI ***");
                WriteToLogFile($"[DEBUG] *** TAB TUŞU ALGILANDI ***");

                // Önce Tab event'ini tetikle
                TabPressed?.Invoke();

                // Tab tuşunun varsayılan işlevini engelle (boşluk eklemesin)
                e.Handled = true;
                Console.WriteLine($"[DEBUG] *** TAB TUŞU VARSAYILAN İŞLEVİ ENGELLENDİ ***");
                WriteToLogFile($"[DEBUG] *** TAB TUŞU VARSAYILAN İŞLEVİ ENGELLENDİ ***");
                return;
            }

            // Boşluk tuşunu kontrol et (sonraki kelime tahmini için)
            if (e.KeyCode == Keys.Space)
            {
                Console.WriteLine($"[DEBUG] *** BOŞLUK TUŞU ALGILANDI ***");
                // SpacePressed olayı, kelime cümle tamponuna eklendikten sonra tetiklenecek
                // return; // Switch case'e devam et, normal boşluk işlemini de yap
            }

            // Diğer Ctrl ve Alt kombinasyonlarını yoksay (Shift'i izin ver)
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control ||
                (Control.ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                Console.WriteLine($"[DEBUG] Ctrl/Alt combination ignored");
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Space:
                    var word = _wordBuffer.ToString();
                    Console.WriteLine($"[DEBUG] Space pressed, word: '{word}'");
                    WriteToLogFile($"[DEBUG] Space pressed, word: '{word}'");

                    if (!string.IsNullOrEmpty(word))
                    {
                        // Kelimeyi cümle buffer'ına ekle
                        _sentenceBuffer.Append(word + " ");
                        Console.WriteLine($"[DEBUG] Sentence buffer: '{_sentenceBuffer}'");
                        WriteToLogFile($"[DEBUG] Sentence buffer: '{_sentenceBuffer}'");

                        // Duplicate word prevention
                        var now = DateTime.Now;
                        if (_lastProcessedWord != word ||
                            (now - _lastWordTime).TotalMilliseconds >= WORD_COOLDOWN_MS)
                        {
                            _lastProcessedWord = word;
                            _lastWordTime = now;
                            Console.WriteLine($"[DEBUG] WordCompleted event fired: '{word}'");
                            WriteToLogFile($"[DEBUG] WordCompleted event fired: '{word}'");
                            WordCompleted?.Invoke(word);
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Word ignored (duplicate): '{word}'");
                            WriteToLogFile($"[DEBUG] Word ignored (duplicate): '{word}'");
                        }
                    }

                    _wordBuffer.Clear(); // Sadece kelime buffer'ını temizle

                    // KeyPressed event'i için mevcut buffer'ı gönder
                    Console.WriteLine($"[DEBUG] KeyPressed event firing with sentence: '{_sentenceBuffer.ToString()}'");
                    WriteToLogFile($"[DEBUG] KeyPressed event firing with sentence: '{_sentenceBuffer.ToString()}'");
                    KeyPressed?.Invoke(_sentenceBuffer.ToString());

                    // Kelime cümle tamponuna eklendi, şimdi SpacePressed olayını tetikle
                    Console.WriteLine($"[DEBUG] SpacePressed event firing with: '{_sentenceBuffer.ToString()}'");
                    WriteToLogFile($"[DEBUG] SpacePressed event firing with: '{_sentenceBuffer.ToString()}'");
                    SpacePressed?.Invoke(_sentenceBuffer.ToString());
                    break;

                case Keys.Back:
                    // Önce kelime buffer'ından sil
                    if (_wordBuffer.Length > 0)
                    {
                        _wordBuffer.Length--;
                        Console.WriteLine($"[DEBUG] Backspace, word buffer: '{_wordBuffer}'");
                    }
                    // Sonra cümle buffer'ından da sil
                    else if (_sentenceBuffer.Length > 0)
                    {
                        _sentenceBuffer.Length--;
                        Console.WriteLine($"[DEBUG] Backspace, sentence buffer: '{_sentenceBuffer}'");
                    }

                    KeyPressed?.Invoke(_sentenceBuffer.ToString() + _wordBuffer.ToString());
                    break;

                case Keys.Enter:
                    Console.WriteLine($"[DEBUG] Enter pressed, clearing buffers");
                    _wordBuffer.Clear();
                    _sentenceBuffer.Clear();
                    break;

                case Keys.OemPeriod:    // .
                case Keys.Oemcomma:     // ,
                case Keys.OemSemicolon: // ; and :
                case Keys.OemQuestion:  // ?
                case Keys.D1 when (Control.ModifierKeys & Keys.Shift) == Keys.Shift: // !
                    {
                        // Önce mevcut kelimeyi cümle buffer'ına ekle
                        var currentWord = _wordBuffer.ToString();
                        if (!string.IsNullOrEmpty(currentWord))
                        {
                            _sentenceBuffer.Append(currentWord);
                            _wordBuffer.Clear();
                        }

                        // Noktalama işaretini ekle
                        char keyChar = GetKeyChar(e.KeyCode);
                        if (keyChar != '\0')
                        {
                            _sentenceBuffer.Append(keyChar);
                            Console.WriteLine($"[DEBUG] Punctuation added, sentence buffer: '{_sentenceBuffer}'");
                        }

                        // TAM CÜMLE TAMAMLANDI - sentence buffer'ını gönder
                        string completeSentence = _sentenceBuffer.ToString().Trim();
                        if (!string.IsNullOrEmpty(completeSentence) && completeSentence.Length > 3)
                        {
                            Console.WriteLine($"[DEBUG] COMPLETE SENTENCE: '{completeSentence}'");

                            // Duplicate sentence prevention
                            var now = DateTime.Now;
                            if (_lastProcessedSentence != completeSentence ||
                                (now - _lastSentenceTime).TotalMilliseconds >= SENTENCE_COOLDOWN_MS)
                            {
                                _lastProcessedSentence = completeSentence;
                                _lastSentenceTime = now;
                                Console.WriteLine($"[DEBUG] SentenceCompleted event fired: '{completeSentence}'");
                                SentenceCompleted?.Invoke(completeSentence);
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] Sentence ignored (duplicate): '{completeSentence}'");
                            }
                        }

                        // Cümle tamamlandı, buffer'ları temizle
                        _sentenceBuffer.Clear();
                        _wordBuffer.Clear();
                    }
                    break;

                default:
                    Console.WriteLine($"[DEBUG] Default case for key: {e.KeyCode}");
                    WriteToLogFile($"[DEBUG] Default case for key: {e.KeyCode}");

                    if (IsTypableKey(e.KeyCode))
                    {
                        Console.WriteLine($"[DEBUG] Key is typable: {e.KeyCode}");
                        WriteToLogFile($"[DEBUG] Key is typable: {e.KeyCode}");

                        char keyChar = GetKeyChar(e.KeyCode);
                        Console.WriteLine($"[DEBUG] GetKeyChar returned: '{keyChar}' (code: {(int)keyChar})");
                        WriteToLogFile($"[DEBUG] GetKeyChar returned: '{keyChar}' (code: {(int)keyChar})");

                        if (keyChar != '\0')
                        {
                            _wordBuffer.Append(keyChar);
                            Console.WriteLine($"[DEBUG] Character added: '{keyChar}', word buffer: '{_wordBuffer}'");
                            WriteToLogFile($"[DEBUG] Character added: '{keyChar}', word buffer: '{_wordBuffer}'");

                            // Mevcut tam metni gönder (cümle + kelime)
                            string currentText = _sentenceBuffer.ToString() + _wordBuffer.ToString();
                            Console.WriteLine($"[DEBUG] KeyPressed event firing with: '{currentText}'");
                            WriteToLogFile($"[DEBUG] KeyPressed event firing with: '{currentText}'");
                            KeyPressed?.Invoke(currentText);
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] GetKeyChar returned null character for: {e.KeyCode}");
                            WriteToLogFile($"[DEBUG] GetKeyChar returned null character for: {e.KeyCode}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Key is NOT typable: {e.KeyCode}");
                        WriteToLogFile($"[DEBUG] Key is NOT typable: {e.KeyCode}");
                    }
                    break;
            }
        }

        private bool IsTypableKey(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) ||
                   (key >= Keys.D0 && key <= Keys.D9) ||
                   (key >= Keys.NumPad0 && key <= Keys.NumPad9) ||
                   key == Keys.OemPeriod || key == Keys.Oemcomma ||
                   key == Keys.OemSemicolon || key == Keys.OemQuotes ||
                   key == Keys.OemOpenBrackets || key == Keys.OemPipe ||
                   key == Keys.Space || // Boşluk tuşu
                   // Türkçe karakterler için OEM tuşları
                   key == Keys.Oem1 || key == Keys.Oem2 || key == Keys.Oem3 ||
                   key == Keys.Oem4 || key == Keys.Oem5 || key == Keys.Oem6 ||
                   key == Keys.Oem7 || key == Keys.Oem8 || key == Keys.OemMinus ||
                   key == Keys.Oemplus || key == Keys.OemBackslash ||
                   key == Keys.OemCloseBrackets || key == Keys.OemQuestion;
        }

        private char GetKeyChar(Keys key)
        {
            try
            {
                // Klavye durumunu al
                byte[] keyboardState = new byte[256];
                if (!GetKeyboardState(keyboardState))
                    return GetFallbackChar(key);

                // Karakter dönüşümü için buffer
                StringBuilder buffer = new StringBuilder(2);

                // Mevcut klavye layout'unu al
                IntPtr keyboardLayout = GetKeyboardLayout(0);

                // Scan code'u al
                uint scanCode = MapVirtualKeyEx((uint)key, 0, keyboardLayout);

                // Virtual key'i Unicode karaktere çevir
                int result = ToUnicodeEx((uint)key, scanCode, keyboardState, buffer, buffer.Capacity, 0, keyboardLayout);

                if (result == 1 && buffer.Length > 0)
                {
                    char ch = buffer[0];
                    Console.WriteLine($"[DEBUG] Key {key} -> '{ch}' (Unicode: {(int)ch})");
                    return ch;
                }
                else if (result > 1 && buffer.Length > 0)
                {
                    // Çoklu karakter durumu (dead keys vs.)
                    char ch = buffer[0];
                    Console.WriteLine($"[DEBUG] Key {key} -> '{ch}' (Multi-char result)");
                    return ch;
                }

                // Başarısız olursa fallback kullan
                Console.WriteLine($"[DEBUG] Key {key} -> fallback (ToUnicodeEx result: {result})");
                return GetFallbackChar(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] GetKeyChar exception for {key}: {ex.Message}");
                // Hata durumunda fallback kullan
                return GetFallbackChar(key);
            }
        }

        private char GetFallbackChar(Keys key)
        {
            // Shift tuşunun basılı olup olmadığını kontrol et
            bool isShiftPressed = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            bool isCapsLockOn = Control.IsKeyLocked(Keys.CapsLock);

            // Büyük harf durumu: Shift basılı XOR CapsLock açık
            bool shouldBeUpperCase = isShiftPressed ^ isCapsLockOn;

            // Temel karakter desteği (fallback)
            return key switch
            {
                _ when key >= Keys.A && key <= Keys.Z => shouldBeUpperCase ?
                    (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A)),
                _ when key >= Keys.D0 && key <= Keys.D9 => (char)('0' + (key - Keys.D0)),
                _ when key >= Keys.NumPad0 && key <= Keys.NumPad9 => (char)('0' + (key - Keys.NumPad0)),
                Keys.Space => ' ',
                Keys.OemPeriod => '.',
                Keys.Oemcomma => ',',
                Keys.OemQuotes => isShiftPressed ? '"' : '\'',
                Keys.OemMinus => isShiftPressed ? '_' : '-',
                Keys.Oemplus => isShiftPressed ? '+' : '=',
                // Türkçe karakterler için temel mapping (klavye layout'a göre değişebilir)
                // Not: Bu mapping Türkçe Q klavye için yaklaşık değerlerdir
                Keys.Oem1 => isShiftPressed ? 'İ' : 'ı',
                Keys.Oem2 => isShiftPressed ? 'Ş' : 'ş',
                Keys.Oem3 => isShiftPressed ? 'Ğ' : 'ğ',
                Keys.Oem4 => isShiftPressed ? 'Ü' : 'ü',
                Keys.Oem5 => isShiftPressed ? 'Ç' : 'ç',
                Keys.Oem6 => isShiftPressed ? 'Ö' : 'ö',
                _ => '\0'
            };
        }

        public void Dispose()
        {
            StopListening();
        }
    }
}
