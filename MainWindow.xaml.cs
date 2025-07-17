using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using OtomatikMetinGenisletici.ViewModels;
using OtomatikMetinGenisletici.Services;
using OtomatikMetinGenisletici.Views;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici;

/// <summary>
/// Modern Main Window with Dependency Injection
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly INotificationService _notificationService;

    // Windows API for flashing window
    [DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

    // Windows API for bringing window to front
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    // Windows API for global hotkey
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private const int HOTKEY_ID = 9000;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_M = 0x4D;

    public MainWindow(MainViewModel viewModel, INotificationService notificationService)
    {
        try
        {
            Console.WriteLine("[DEBUG] MainWindow constructor başlıyor...");

            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            DataContext = _viewModel;

            // Pencere başlığına versiyon numarasını ekle
            SetWindowTitleWithVersion();

            Console.WriteLine("[DEBUG] NotificationService ayarlanıyor...");
            // NotificationService'e bu MainWindow referansını ver
            _notificationService.SetMainWindow(this);

            Console.WriteLine("[DEBUG] ImageRecognitionService ayarlanıyor...");
            // ImageRecognitionService'i WindowHelper'a bağla
            var imageRecognitionService = ServiceProviderExtensions.Services.GetService<IImageRecognitionService>();
            if (imageRecognitionService != null)
            {
                WindowHelper.SetImageRecognitionService(imageRecognitionService);
                Console.WriteLine("[DEBUG] ImageRecognitionService WindowHelper'a bağlandı");
            }

            Console.WriteLine("[DEBUG] Tray icon başlatılıyor...");
            // Initialize tray icon
            _notificationService.ShowTrayIcon();

            Console.WriteLine("[DEBUG] Event'ler bağlanıyor...");
            // Handle window state changes
            StateChanged += OnStateChanged;

            Console.WriteLine("[DEBUG] Global hotkey kaydediliyor...");
            // Global hotkey (Ctrl+Shift+M) to show window
            try
            {
                RegisterGlobalHotkey();
                Console.WriteLine("[DEBUG] Global hotkey başarıyla kaydedildi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Global hotkey kaydetme hatası: {ex.Message}");
            }

            Console.WriteLine("[DEBUG] Kısayol önizleme paneli kontrol ediliyor...");
            // Kısayol önizleme panelini ayarlara göre göster
            try
            {
                // Loaded event'inde göster (UI tamamen yüklendikten sonra)
                Loaded += async (s, e) =>
                {
                    if (_viewModel.IsShortcutPreviewPanelVisible)
                    {
                        Console.WriteLine("[DEBUG] Kısayol önizleme paneli açılışta gösteriliyor");

                        // Kısayolların yüklenmesini bekle
                        await Task.Delay(500); // UI'nin tamamen yüklenmesini bekle

                        // Kısayolları yükle ve paneli göster
                        await _viewModel.LoadShortcutsAsync();
                        _viewModel.ShowShortcutPreviewPanel();
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol önizleme paneli başlatma hatası: {ex.Message}");
            }

            Console.WriteLine("[DEBUG] MainWindow constructor tamamlandı.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] MainWindow constructor hatası: {ex}");
            MessageBox.Show($"MainWindow başlatma hatası:\n\n{ex.Message}\n\nDetay: {ex.InnerException?.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (WindowState == WindowState.Minimized)
            {
                Console.WriteLine("[DEBUG] Window minimized, hiding to tray");

                // Async olarak hide et (UI thread'i bloklamayı önle)
                Dispatcher.BeginInvoke(() =>
                {
                    Hide();
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] OnStateChanged failed: {ex.Message}");
        }
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void ShowSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = ServiceProviderExtensions.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = ServiceProviderExtensions.Services.GetRequiredService<AboutWindow>();
        aboutWindow.Owner = this;
        aboutWindow.ShowDialog();
    }

    // Smart Suggestions Event Handlers
    private async void ShowSmartSuggestionsStats_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var stats = await _viewModel.GetSmartSuggestionsStatisticsAsync();
            var message = $"Öğrenme İstatistikleri:\n\n" +
                         $"Toplam Benzersiz Kelime: {stats.TotalUniqueWords:N0}\n" +
                         $"Toplam Kelime Sayısı: {stats.TotalWordCount:N0}\n" +
                         $"İki Kelime Dizileri: {stats.TotalBigrams:N0}\n" +
                         $"Üç Kelime Dizileri: {stats.TotalTrigrams:N0}\n" +
                         $"Tamamlama Önekleri: {stats.CompletionPrefixes:N0}\n" +
                         $"Son Öğrenme: {stats.LastLearningSession:dd.MM.yyyy HH:mm}";

            MessageBox.Show(message, "Akıllı Öneriler İstatistikleri",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"İstatistikler alınırken hata oluştu: {ex.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportSmartSuggestionsData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Dosyaları (*.json)|*.json|Tüm Dosyalar (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"akilli_oneriler_yedek_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var smartSuggestionsService = ServiceProviderExtensions.Services.GetRequiredService<ISmartSuggestionsService>();
                await smartSuggestionsService.ExportLearningDataAsync(saveFileDialog.FileName);

                MessageBox.Show("Akıllı öneriler verileri başarıyla dışa aktarıldı!",
                    "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veri dışa aktarılırken hata oluştu: {ex.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ResetSmartSuggestionsData_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Tüm akıllı öneriler öğrenme verileri silinecek. Bu işlem geri alınamaz.\n\nDevam etmek istediğinizden emin misiniz?",
            "Öğrenme Verilerini Sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var smartSuggestionsService = ServiceProviderExtensions.Services.GetRequiredService<ISmartSuggestionsService>();
                await smartSuggestionsService.ResetLearningDataAsync();

                // Dashboard'ı yenile
                await _viewModel.RefreshSmartSuggestionsDataAsync();

                MessageBox.Show("Akıllı öneriler öğrenme verileri başarıyla sıfırlandı!",
                    "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler sıfırlanırken hata oluştu: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Yeni Smart Suggestions Event Handlers
    private async void RefreshSmartSuggestions_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshSmartSuggestionsDataAsync();
        MessageBox.Show("Akıllı öneriler verileri yenilendi!", "Bilgi",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenSmartSuggestionsSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = ServiceProviderExtensions.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }



    private async void ShowDetailedStats_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var stats = await _viewModel.GetSmartSuggestionsStatisticsAsync();
            var detailedMessage = $"Detaylı Akıllı Öneriler İstatistikleri:\n\n" +
                                 $"📚 Kelime İstatistikleri:\n" +
                                 $"   • Toplam Benzersiz Kelime: {stats.TotalUniqueWords:N0}\n" +
                                 $"   • Toplam Kelime Sayısı: {stats.TotalWordCount:N0}\n" +
                                 $"   • Tamamlama Önekleri: {stats.CompletionPrefixes:N0}\n\n" +
                                 $"🔗 N-Gram İstatistikleri:\n" +
                                 $"   • İki Kelime Dizileri (Bigram): {stats.TotalBigrams:N0}\n" +
                                 $"   • Üç Kelime Dizileri (Trigram): {stats.TotalTrigrams:N0}\n\n" +
                                 $"🎯 Performans:\n" +
                                 $"   • Doğruluk Skoru: {stats.AccuracyScore:P1}\n" +
                                 $"   • Ortalama Tahmin Süresi: {stats.AveragePredictionTime:F2}ms\n\n" +
                                 $"🕒 Zaman Bilgileri:\n" +
                                 $"   • Son Öğrenme: {stats.LastLearningSession:dd.MM.yyyy HH:mm}\n" +
                                 $"   • Toplam Öğrenme Süresi: {stats.TotalLearningTime.TotalMinutes:F0} dakika\n\n" +
                                 $"🔝 En Çok Kullanılan Kelimeler:\n";

            for (int i = 0; i < Math.Min(5, stats.MostCommonWords.Count); i++)
            {
                var word = stats.MostCommonWords[i];
                detailedMessage += $"   {i + 1}. {word.Word} ({word.Count} kez)\n";
            }

            MessageBox.Show(detailedMessage, "Detaylı İstatistikler",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Detaylı istatistikler alınırken hata oluştu: {ex.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportSmartSuggestionsData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Dosyaları (*.json)|*.json|Tüm Dosyalar (*.*)|*.*",
                DefaultExt = "json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var smartSuggestionsService = ServiceProviderExtensions.Services.GetRequiredService<ISmartSuggestionsService>();
                await smartSuggestionsService.ImportLearningDataAsync(openFileDialog.FileName);

                // Dashboard'ı yenile
                await _viewModel.RefreshSmartSuggestionsDataAsync();

                MessageBox.Show("Akıllı öneriler verileri başarıyla içe aktarıldı!",
                    "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veri içe aktarılırken hata oluştu: {ex.Message}",
                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }



    private void ExitApplication_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Uygulamayı kapatmak istediğinizden emin misiniz?",
            "Çıkış",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    public void ShowMainWindow()
    {
        try
        {
            Console.WriteLine("[DEBUG] ShowMainWindow called");

            // UI thread'de çalıştığımızdan emin ol
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowMainWindow());
                return;
            }

            // Önizleme ekranını gizle
            _viewModel.HidePreview();

            // Ana pencereyi göster
            if (WindowState == WindowState.Minimized)
            {
                Console.WriteLine("[DEBUG] Window was minimized, restoring to normal");
                WindowState = WindowState.Normal;
            }

            // Pencereyi görünür yap
            if (!IsVisible)
            {
                Console.WriteLine("[DEBUG] Window was hidden, showing");
                Show();
            }

            // Pencereyi aktif hale getir ve en öne getir
            Console.WriteLine("[DEBUG] Bringing window to front");

            // Windows API kullanarak pencereyi en öne getir
            BringToFront();

            Console.WriteLine("[DEBUG] ShowMainWindow completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] ShowMainWindow failed: {ex.Message}");
        }
    }

    private void BringToFront()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;

            // Windows API kullanarak pencereyi restore et
            if (handle != IntPtr.Zero)
            {
                if (IsIconic(handle))
                {
                    Console.WriteLine("[DEBUG] Window is minimized, restoring with Windows API");
                    ShowWindow(handle, SW_RESTORE);
                }
                else
                {
                    Console.WriteLine("[DEBUG] Window is not minimized, showing with Windows API");
                    ShowWindow(handle, SW_SHOW);
                }

                // Foreground'a getir
                SetForegroundWindow(handle);
            }

            // WPF metodları da kullan
            Show();

            // Window state'i normal yap
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            // Activate et
            Activate();

            // Topmost ile en öne getir
            Topmost = true;
            Topmost = false;

            // Focus ver
            Focus();

            // Windows API ile flash
            FlashWindow();

            Console.WriteLine("[DEBUG] BringToFront completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] BringToFront failed: {ex.Message}");
        }
    }

    private void FlashWindow()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                FlashWindow(handle, true);
            }
        }
        catch
        {
            // Ignore flash window errors
        }
    }

    private void RegisterGlobalHotkey()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                RegisterHotKey(handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_M);

                // Add message hook for hotkey
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(HwndHook);
            }
        }
        catch
        {
            // Ignore hotkey registration errors
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ShowMainWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Pencere başlığına versiyon numarasını ekler
    /// </summary>
    private void SetWindowTitleWithVersion()
    {
        try
        {
            // Assembly'den versiyon bilgisini al
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            if (version != null)
            {
                // Versiyon formatı: Major.Minor.Build (Revision'ı gösterme)
                string versionString = $"{version.Major}.{version.Minor}.{version.Build}";
                Title = $"Gelişmiş Otomatik Metin Genişletici v{versionString}";
                Console.WriteLine($"[DEBUG] Pencere başlığı ayarlandı: {Title}");
            }
            else
            {
                Title = "Gelişmiş Otomatik Metin Genişletici";
                Console.WriteLine("[DEBUG] Versiyon bilgisi alınamadı, varsayılan başlık kullanıldı");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] SetWindowTitleWithVersion hatası: {ex.Message}");
            Title = "Gelişmiş Otomatik Metin Genişletici";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Cleanup hotkey
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                UnregisterHotKey(handle, HOTKEY_ID);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        base.OnClosed(e);
    }

    private void ClearLearningLog_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearLearningLog();
        MessageBox.Show("Öğrenme logu temizlendi!", "Bilgi",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ResetLearningData_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Bu işlem tüm öğrenme verilerini kalıcı olarak silecektir!\n\n" +
            "• Öğrenilen kelimeler\n" +
            "• Kelime çiftleri (bigrams)\n" +
            "• Üçlü kelime grupları (trigrams)\n" +
            "• Öğrenme logları\n" +
            "• İstatistikler\n\n" +
            "Bu işlem geri alınamaz. Devam etmek istediğinizden emin misiniz?",
            "Öğrenme Verilerini Sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _viewModel.ResetAllLearningDataAsync();
                MessageBox.Show("Tüm öğrenme verileri başarıyla sıfırlandı!", "Başarılı",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Öğrenme verileri sıfırlanırken hata oluştu:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ToggleShortcutPreview_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleShortcutPreviewPanel();
    }

    private async void NGramCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel != null)
        {
            await _viewModel.UpdateNGramDisplayCountAsync((int)e.NewValue);
        }
    }

    private async void MinFrequencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel != null)
        {
            await _viewModel.UpdateNGramMinFrequencyAsync((int)e.NewValue);
        }
    }

    private async void RefreshNGramData_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.RefreshNGramDataAsync();
            MessageBox.Show("N-Gram verileri yenilendi!", "Bilgi",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Veri Düzenleme Event Handler'ları
    private async void EditWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is WordUsageStatistic word)
        {
            var dialog = new EditWordDialog(word.Word, word.Count);
            if (dialog.ShowDialog() == true)
            {
                if (_viewModel != null)
                {
                    bool success = await _viewModel.UpdateWordAsync(word.Word, dialog.NewWord, dialog.NewCount);
                    if (success)
                    {
                        await _viewModel.RefreshSmartSuggestionsDataAsync();
                        MessageBox.Show("Kelime başarıyla güncellendi!", "Başarılı",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Kelime güncellenirken hata oluştu!", "Hata",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    private async void DeleteWord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is WordUsageStatistic word)
        {
            var result = MessageBox.Show($"'{word.Word}' kelimesini silmek istediğinizden emin misiniz?\n\nBu işlem geri alınamaz ve ilgili bigram/trigramlar da silinecektir.",
                "Kelime Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && _viewModel != null)
            {
                bool success = await _viewModel.DeleteWordAsync(word.Word);
                if (success)
                {
                    await _viewModel.RefreshSmartSuggestionsDataAsync();
                    MessageBox.Show("Kelime başarıyla silindi!", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Kelime silinirken hata oluştu!", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void EditBigram_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NGramStatistic bigram)
        {
            var dialog = new EditBigramDialog(bigram.NGram, bigram.Count);
            if (dialog.ShowDialog() == true)
            {
                if (_viewModel != null)
                {
                    bool success = await _viewModel.UpdateBigramAsync(bigram.NGram, dialog.NewBigram, dialog.NewCount);
                    if (success)
                    {
                        await _viewModel.RefreshSmartSuggestionsDataAsync();
                        MessageBox.Show("Bigram başarıyla güncellendi!", "Başarılı",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Bigram güncellenirken hata oluştu!", "Hata",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    private async void DeleteBigram_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NGramStatistic bigram)
        {
            var result = MessageBox.Show($"'{bigram.NGram}' bigramını silmek istediğinizden emin misiniz?\n\nBu işlem geri alınamaz.",
                "Bigram Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && _viewModel != null)
            {
                bool success = await _viewModel.DeleteBigramAsync(bigram.NGram);
                if (success)
                {
                    await _viewModel.RefreshSmartSuggestionsDataAsync();
                    MessageBox.Show("Bigram başarıyla silindi!", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Bigram silinirken hata oluştu!", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void EditTrigram_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NGramStatistic trigram)
        {
            var dialog = new EditTrigramDialog(trigram.NGram, trigram.Count);
            if (dialog.ShowDialog() == true)
            {
                if (_viewModel != null)
                {
                    bool success = await _viewModel.UpdateTrigramAsync(trigram.NGram, dialog.NewTrigram, dialog.NewCount);
                    if (success)
                    {
                        await _viewModel.RefreshSmartSuggestionsDataAsync();
                        MessageBox.Show("Trigram başarıyla güncellendi!", "Başarılı",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Trigram güncellenirken hata oluştu!", "Hata",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    private async void DeleteTrigram_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NGramStatistic trigram)
        {
            var result = MessageBox.Show($"'{trigram.NGram}' trigramını silmek istediğinizden emin misiniz?\n\nBu işlem geri alınamaz.",
                "Trigram Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && _viewModel != null)
            {
                bool success = await _viewModel.DeleteTrigramAsync(trigram.NGram);
                if (success)
                {
                    await _viewModel.RefreshSmartSuggestionsDataAsync();
                    MessageBox.Show("Trigram başarıyla silindi!", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Trigram silinirken hata oluştu!", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }


}