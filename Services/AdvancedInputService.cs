using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OtomatikMetinGenisletici.Services
{
    public interface IAdvancedInputService
    {
        Task<bool> SendTextAsync(string text);
        Task<bool> SendTextAsTypingAsync(string text); // Yeni: Karakter karakter yazma
        Task<bool> SimulateCtrlVAsync();
        Task<bool> SimulateKeyPressAsync(ushort virtualKeyCode);
        Task<string?> GetClipboardTextAsync();
        Task<bool> SetClipboardTextAsync(string text);
        Task<bool> RestoreClipboardAsync(string? originalText);
    }

    public class AdvancedInputService : IAdvancedInputService
    {
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

        // MOUSEINPUT yapısı
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

        // HARDWAREINPUT yapısı
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

        public async Task<bool> SendTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                Console.WriteLine($"[ADVANCED INPUT] Sending text: '{text}'");

                // Get original clipboard content
                string? originalClipboard = await GetClipboardTextAsync();

                // Set text to clipboard
                if (!await SetClipboardTextAsync(text))
                {
                    Console.WriteLine("[ERROR] Failed to set clipboard text");
                    return false;
                }

                // Simulate Ctrl+V
                bool success = await SimulateCtrlVAsync();

                // Small delay to ensure paste operation completes
                await Task.Delay(50);

                // Restore original clipboard
                await RestoreClipboardAsync(originalClipboard);

                Console.WriteLine($"[ADVANCED INPUT] Text sent successfully: {success}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendTextAsync hatası: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SimulateCtrlVAsync()
        {
            return await Task.Run(() =>
            {
                try
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

                    uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                    Console.WriteLine($"[ADVANCED INPUT] Ctrl+V simulated, result: {result}");
                    return result == inputs.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] SimulateCtrlVAsync hatası: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> SimulateKeyPressAsync(ushort virtualKeyCode)
        {
            return await Task.Run(() =>
            {
                try
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

                    uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                    Console.WriteLine($"[ADVANCED INPUT] Key {virtualKeyCode} simulated, result: {result}");
                    return result == inputs.Length;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] SimulateKeyPressAsync hatası: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<string?> GetClipboardTextAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Clipboard.ContainsText())
                        {
                            return Clipboard.GetText();
                        }
                        return null;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] GetClipboardTextAsync hatası: {ex.Message}");
                    return null;
                }
            });
        }

        public async Task<bool> SetClipboardTextAsync(string text)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetText(text);
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] SetClipboardTextAsync hatası: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> RestoreClipboardAsync(string? originalText)
        {
            if (originalText == null)
                return true;

            return await Task.Run(() =>
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetText(originalText);
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] RestoreClipboardAsync hatası: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Metni karakter karakter yazarak gönderir - öğrenme sistemine girmesi için
        /// Türkçe karakterler için clipboard fallback kullanır
        /// </summary>
        public async Task<bool> SendTextAsTypingAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                Console.WriteLine($"[ADVANCED INPUT] Typing text: '{text}'");

                // Türkçe karakterleri kontrol et
                bool hasTurkishChars = ContainsTurkishCharacters(text);

                if (hasTurkishChars)
                {
                    Console.WriteLine($"[ADVANCED INPUT] Türkçe karakter algılandı, clipboard fallback kullanılıyor");
                    // Türkçe karakterler varsa clipboard kullan ama öğrenme sistemine girmesi için işaretle
                    return await SendTextAsync(text);
                }

                // ASCII karakterler için SendInput kullan
                return await Task.Run(() =>
                {
                    foreach (char c in text)
                    {
                        try
                        {
                            // ASCII karakter için VkKeyScan kullan
                            short vkResult = VkKeyScan(c);
                            if (vkResult == -1)
                            {
                                Console.WriteLine($"[WARNING] VkKeyScan failed for character '{c}', skipping");
                                continue;
                            }

                            byte vk = (byte)(vkResult & 0xFF);
                            byte shift = (byte)((vkResult >> 8) & 0xFF);

                            List<INPUT> inputs = new List<INPUT>();

                            // Shift gerekiyorsa önce Shift bas
                            if ((shift & 1) != 0) // Shift key needed
                            {
                                inputs.Add(new INPUT
                                {
                                    type = INPUT_KEYBOARD,
                                    U = new InputUnion
                                    {
                                        ki = new KEYBDINPUT
                                        {
                                            wVk = VK_SHIFT,
                                            dwFlags = 0,
                                            time = 0,
                                            dwExtraInfo = GetMessageExtraInfo()
                                        }
                                    }
                                });
                            }

                            // Ana karakter - key down
                            inputs.Add(new INPUT
                            {
                                type = INPUT_KEYBOARD,
                                U = new InputUnion
                                {
                                    ki = new KEYBDINPUT
                                    {
                                        wVk = vk,
                                        dwFlags = 0,
                                        time = 0,
                                        dwExtraInfo = GetMessageExtraInfo()
                                    }
                                }
                            });

                            // Ana karakter - key up
                            inputs.Add(new INPUT
                            {
                                type = INPUT_KEYBOARD,
                                U = new InputUnion
                                {
                                    ki = new KEYBDINPUT
                                    {
                                        wVk = vk,
                                        dwFlags = KEYEVENTF_KEYUP,
                                        time = 0,
                                        dwExtraInfo = GetMessageExtraInfo()
                                    }
                                }
                            });

                            // Shift'i bırak
                            if ((shift & 1) != 0)
                            {
                                inputs.Add(new INPUT
                                {
                                    type = INPUT_KEYBOARD,
                                    U = new InputUnion
                                    {
                                        ki = new KEYBDINPUT
                                        {
                                            wVk = VK_SHIFT,
                                            dwFlags = KEYEVENTF_KEYUP,
                                            time = 0,
                                            dwExtraInfo = GetMessageExtraInfo()
                                        }
                                    }
                                });
                            }

                            // Input'u gönder
                            uint result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));

                            // Kısa bekleme
                            Thread.Sleep(2);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Character '{c}' typing failed: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"[ADVANCED INPUT] Text typed successfully: '{text}'");
                    return true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendTextAsTypingAsync hatası: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Metinde Türkçe karakterleri kontrol eder
        /// </summary>
        private bool ContainsTurkishCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // Türkçe karakterler: ç, ğ, ı, ö, ş, ü ve büyük harfleri
            char[] turkishChars = { 'ç', 'ğ', 'ı', 'ö', 'ş', 'ü', 'Ç', 'Ğ', 'İ', 'Ö', 'Ş', 'Ü' };

            return text.IndexOfAny(turkishChars) >= 0;
        }

        // VkKeyScan için P/Invoke
        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const ushort VK_SHIFT = 0x10;
    }
}
