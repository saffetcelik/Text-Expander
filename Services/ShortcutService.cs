using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Newtonsoft.Json;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public class ShortcutService : IShortcutService
    {
        private const string ShortcutsFileName = "kisayollar.json";
        private readonly ObservableCollection<Shortcut> _shortcuts = new();

        // Win32 API'leri
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_BACK = 0x08;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        // Duplicate prevention
        private volatile bool _isExpanding = false;
        private string _lastExpandedShortcut = string.Empty;
        private DateTime _lastExpansionTime = DateTime.MinValue;
        private const int EXPANSION_COOLDOWN_MS = 500; // 500ms cooldown

        public ObservableCollection<Shortcut> Shortcuts => _shortcuts;

        public event Action<string>? ShortcutExpanded;

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
                ExpandText(shortcutKey, expansion);
                //MessageBox.Show($"Metin genişletilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                _shortcuts.Add(new Shortcut(key, expansion));
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

                // Akıllı genişletme stratejisi
                if (expansion.StartsWith(shortcutKey, StringComparison.OrdinalIgnoreCase))
                {
                    // APPEND MODE: Genişletme kısayol ile başlıyorsa, sadece devamını ekle
                    string completionText = expansion.Substring(shortcutKey.Length);
                    Clipboard.SetText(completionText);

                    // Kısa bekleme
                    Thread.Sleep(10);

                    // Sadece eksik kısmı yapıştır
                    SendCtrlV();
                }
                else
                {
                    // REPLACE MODE: Genişletme kısayol ile başlamıyorsa, kısayolu sil ve tam metni yaz
                    Clipboard.SetText(expansion);

                    // Kısayolu sil
                    for (int i = 0; i < shortcutKey.Length; i++)
                    {
                        SendBackspace();
                    }

                    // Kısa bekleme
                    Thread.Sleep(10);

                    // Tam metni yapıştır
                    SendCtrlV();
                }

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

        private void SendBackspace()
        {
            keybd_event(VK_BACK, 0, 0, UIntPtr.Zero);
            keybd_event(VK_BACK, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void SendCtrlV()
        {
            // Ctrl tuşunu bas
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            // V tuşunu bas
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            // V tuşunu bırak
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            // Ctrl tuşunu bırak
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public Shortcut? GetShortcut(string key)
        {
            return _shortcuts.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        public bool ShortcutExists(string key)
        {
            return _shortcuts.Any(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
