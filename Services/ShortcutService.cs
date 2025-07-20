using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Newtonsoft.Json;
using OtomatikMetinGenisletici.Models;
using System.Collections.Generic;
using Clipboard = System.Windows.Clipboard;

namespace OtomatikMetinGenisletici.Services
{
    public class ShortcutService : IShortcutService
    {
        private const string ShortcutsFileName = "kisayollar.json";
        private readonly ObservableCollection<Models.Shortcut> _shortcuts = new();

        // Win32 API'leri - Modern SendInput kullanıyoruz
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // INPUT yapıları
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public InputUnion U;
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // Sabitler
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const ushort VK_BACK = 0x08;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_V = 0x56;

        // Duplicate prevention
        private volatile bool _isExpanding = false;
        private string _lastExpandedShortcut = string.Empty;
        private DateTime _lastExpansionTime = DateTime.MinValue;
        private const int EXPANSION_COOLDOWN_MS = 500; // 500ms cooldown

        // Learning exclusion tracking
        private string _lastExpandedText = string.Empty;
        private DateTime _lastExpansionEndTime = DateTime.MinValue;
        private const int LEARNING_EXCLUSION_WINDOW_MS = 5000; // 5 saniye öğrenme hariç tutma penceresi (artırıldı)

        public ObservableCollection<Models.Shortcut> Shortcuts => _shortcuts;

        public event Action<string>? ShortcutExpanded;

        /// <summary>
        /// Verilen metnin yakın zamanda genişletilen bir kısayol olup olmadığını kontrol eder
        /// </summary>
        public bool IsRecentlyExpandedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(_lastExpandedText))
                return false;

            // Zaman penceresi kontrolü
            if (DateTime.Now > _lastExpansionEndTime)
                return false;

            // Metin eşleşme kontrolü - genişletilen metin ile tam veya kısmi eşleşme
            var normalizedText = text.Trim().ToLowerInvariant();
            var normalizedExpanded = _lastExpandedText.Trim().ToLowerInvariant();

            // Tam eşleşme
            if (normalizedText == normalizedExpanded)
                return true;

            // Genişletilen metin, gelen metnin içinde yer alıyor mu?
            if (normalizedText.Contains(normalizedExpanded))
                return true;

            // Ters kontrol: Gelen metin, genişletilen metnin içinde yer alıyor mu?
            if (normalizedExpanded.Contains(normalizedText))
                return true;

            // Kısayol + genişletme kombinasyonlarını algıla (örn: "safsaffet")
            foreach (var shortcut in _shortcuts)
            {
                var shortcutKey = shortcut.Key.ToLowerInvariant();
                var expansion = shortcut.Expansion.ToLowerInvariant();

                // "shortcut + expansion" pattern'ini algıla
                if (normalizedText.Contains(shortcutKey + expansion) ||
                    normalizedText.Contains(expansion + shortcutKey))
                {
                    Console.WriteLine($"[EXPANSION_DETECTION] Kısayol+genişletme kombinasyonu algılandı: '{text}'");
                    return true;
                }
            }

            // Gelen metin, genişletilen metnin içinde yer alıyor mu?
            if (normalizedExpanded.Contains(normalizedText))
                return true;

            return false;
        }

        public async Task LoadShortcutsAsync()
        {
            try
            {
                if (File.Exists(ShortcutsFileName))
                {
                    var json = await File.ReadAllTextAsync(ShortcutsFileName);
                    var shortcuts = JsonConvert.DeserializeObject<List<Shortcut>>(json);
                    
                    _shortcuts.Clear();
                    if (shortcuts != null)
                    {
                        foreach (var shortcut in shortcuts)
                        {
                            _shortcuts.Add(shortcut);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kısayollar yüklenirken hata oluştu: {ex.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveShortcutsAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_shortcuts.ToList(), Formatting.Indented);
                await File.WriteAllTextAsync(ShortcutsFileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kısayollar kaydedilirken hata oluştu: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void AddShortcut(string key, string expansion)
        {
            var existingShortcut = _shortcuts.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            
            if (existingShortcut != null)
            {
                existingShortcut.Expansion = expansion;
            }
            else
            {
                _shortcuts.Add(new Models.Shortcut(key, expansion));
            }
        }

        public void RemoveShortcut(string key)
        {
            var shortcut = _shortcuts.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (shortcut != null)
            {
                _shortcuts.Remove(shortcut);
            }
        }

        public bool TryExpandShortcut(string input, out string expansion)
        {
            expansion = string.Empty;

            // Duplicate expansion prevention
            if (_isExpanding)
            {
                return false; // Zaten bir genişletme işlemi devam ediyor
            }

            // Cooldown check - aynı kısayol çok kısa sürede tekrar tetiklenmesin (case insensitive)
            var now = DateTime.Now;
            if (string.Equals(_lastExpandedShortcut, input, StringComparison.OrdinalIgnoreCase) &&
                (now - _lastExpansionTime).TotalMilliseconds < EXPANSION_COOLDOWN_MS)
            {
                return false; // Çok kısa sürede aynı kısayol tekrar tetiklendi
            }

            // Tam eşleşme ara (case insensitive)
            var shortcut = _shortcuts.FirstOrDefault(s =>
                string.Equals(input, s.Key, StringComparison.OrdinalIgnoreCase));

            if (shortcut != null)
            {
                // Expansion lock
                _isExpanding = true;
                _lastExpandedShortcut = input;
                _lastExpansionTime = now;

                try
                {
                    expansion = shortcut.Expansion;
                    shortcut.LastUsed = DateTime.Now;
                    shortcut.UsageCount++;

                    // Kısayolu genişlet
                    ExpandText(shortcut.Key, expansion);

                    // Genişletilen metni öğrenme hariç tutma için kaydet
                    _lastExpandedText = expansion;
                    _lastExpansionEndTime = DateTime.Now.AddMilliseconds(LEARNING_EXCLUSION_WINDOW_MS);

                    ShortcutExpanded?.Invoke(expansion);
                    return true;
                }
                finally
                {
                    // Expansion lock'u serbest bırak
                    _isExpanding = false;
                }
            }

            return false;
        }

        private void ExpandText(string shortcutKey, string expansion)
        {
            try
            {
                // Clipboard'ı güvenli şekilde yedekle
                string originalClipboard = string.Empty;
                try
                {
                    originalClipboard = Clipboard.GetText();
                }
                catch
                {
                    // Clipboard erişim hatası durumunda boş string kullan
                }

                // HER ZAMAN REPLACE MODE KULLAN - Tekrarlama problemini önlemek için
                // Bu sayede "saf" → "saffet" her zaman temiz bir şekilde çalışır
                // ve "safsaffet" gibi birleşik metinler oluşmaz

                // Modern SendInput ile Ctrl+Backspace + yapıştır
                ReplaceTextWithSendInput(0, expansion); // characterCount artık kullanılmıyor

                // Orijinal clipboard içeriğini geri yükle (async)
                Task.Run(async () =>
                {
                    await Task.Delay(200); // Yapıştırma işleminin tamamlanmasını bekle
                    try
                    {
                        if (!string.IsNullOrEmpty(originalClipboard))
                        {
                            Clipboard.SetText(originalClipboard);
                        }
                    }
                    catch
                    {
                        // Clipboard geri yükleme hatalarını yoksay
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Metin genişletilirken hata oluştu: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ReplaceTextWithSendInput(int characterCount, string replacement)
        {
            try
            {
                // Clipboard'ı yedekle
                string originalClipboard = string.Empty;
                try
                {
                    originalClipboard = Clipboard.GetText();
                }
                catch { }

                // 1. Ctrl+Backspace ile kelimeyi sil (tek işlem)
                var deleteInputs = new INPUT[]
                {
                    // Ctrl key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // Backspace key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_BACK,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // Backspace key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_BACK,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // Ctrl key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    }
                };

                // Ctrl+Backspace'i gönder
                SendInput((uint)deleteInputs.Length, deleteInputs, Marshal.SizeOf(typeof(INPUT)));

                // Kelime silme işleminin tamamlanmasını bekle
                Thread.Sleep(100);

                // 2. Yeni metni clipboard'a koy ve Ctrl+V ile yapıştır
                Clipboard.SetText(replacement);
                Thread.Sleep(20);

                // Ctrl+V için INPUT array
                var pasteInputs = new INPUT[]
                {
                    // Ctrl key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // V key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_V,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // V key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_V,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // Ctrl key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    }
                };

                // Ctrl+V'yi gönder
                SendInput((uint)pasteInputs.Length, pasteInputs, Marshal.SizeOf(typeof(INPUT)));

                // Orijinal clipboard'ı geri yükle (async)
                Task.Run(async () =>
                {
                    await Task.Delay(300);
                    try
                    {
                        if (!string.IsNullOrEmpty(originalClipboard))
                        {
                            Clipboard.SetText(originalClipboard);
                        }
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Metin değiştirme hatası: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendCtrlVWithSendInput()
        {
            try
            {
                // Ctrl+V için INPUT array
                var pasteInputs = new INPUT[]
                {
                    // Ctrl key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // V key down
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_V,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYDOWN,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // V key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_V,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    },
                    // Ctrl key up
                    new INPUT
                    {
                        type = INPUT_KEYBOARD,
                        U = new InputUnion
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VK_CONTROL,
                                wScan = 0,
                                dwFlags = KEYEVENTF_KEYUP,
                                time = 0,
                                dwExtraInfo = GetMessageExtraInfo()
                            }
                        }
                    }
                };

                // Ctrl+V'yi gönder
                SendInput((uint)pasteInputs.Length, pasteInputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ctrl+V gönderme hatası: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public Models.Shortcut? GetShortcut(string key)
        {
            return _shortcuts.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        public bool ShortcutExists(string key)
        {
            return _shortcuts.Any(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
