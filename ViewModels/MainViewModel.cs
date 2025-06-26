using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;
using OtomatikMetinGenisletici.Helpers;
using OtomatikMetinGenisletici.Views;

namespace OtomatikMetinGenisletici.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IShortcutService _shortcutService;
        private readonly ISmartSuggestionsService _smartSuggestionsService;
        private readonly ISettingsService _settingsService;
        private readonly IKeyboardHookService _keyboardHookService;
        private readonly IAdvancedInputService _advancedInputService;
        private readonly ITourService _tourService;
        private PreviewOverlay? _previewOverlay;
        private Views.ShortcutPreviewWindow? _shortcutPreviewWindow;

        private string _shortcutFilter = string.Empty;

        private string _contextBuffer = string.Empty;
        private Shortcut? _selectedShortcut;
        private List<SmartSuggestion> _currentSmartSuggestions = new();
        private string _lastActiveWindow = string.Empty;



        public ObservableCollection<Shortcut> Shortcuts => _shortcutService.Shortcuts;
        public ObservableCollection<Shortcut> FilteredShortcuts { get; } = new();

        // Stats Properties
        public int TotalShortcuts => Shortcuts.Count;
        public int TotalExpansions => Shortcuts.Sum(s => s.UsageCount);

        public bool IsListening => _keyboardHookService.IsListening;

        // Analytics Properties
        public ObservableCollection<Shortcut> TopShortcuts { get; } = new();
        public ObservableCollection<Shortcut> RecentActivities { get; } = new();

        // Smart Suggestions Properties
        public ObservableCollection<SmartSuggestion> SmartSuggestions { get; } = new();
        public ObservableCollection<WordUsageStatistic> MostUsedWords { get; } = new();
        public ObservableCollection<NGramStatistic> TopBigrams { get; } = new();
        public ObservableCollection<NGramStatistic> TopTrigrams { get; } = new();

        public bool IsSmartSuggestionsEnabled
        {
            get => _settingsService?.Settings?.SmartSuggestionsEnabled ?? false;
        }

        public string SmartSuggestionsStatusText => IsSmartSuggestionsEnabled ? "🟢 Aktif" : "🔴 Pasif";
        public string SmartSuggestionsStatusColor => IsSmartSuggestionsEnabled ? "Green" : "Red";



        // Kısayol Önizleme Paneli Özellikleri
        public bool IsShortcutPreviewPanelVisible
        {
            get => _settingsService?.Settings?.ShortcutPreviewPanelVisible ?? false;
        }

        public string ShortcutPreviewPanelStatusText => IsShortcutPreviewPanelVisible ? "🟢 Görünür" : "🔴 Gizli";
        public string ShortcutPreviewPanelStatusColor => IsShortcutPreviewPanelVisible ? "Green" : "Red";

        // Pencere Filtreleme Özellikleri
        public bool IsWindowFilteringEnabled
        {
            get => _settingsService?.Settings?.WindowFilteringEnabled ?? true;
        }

        public string WindowFilteringStatusText
        {
            get
            {
                if (!IsWindowFilteringEnabled)
                    return "🔴 Pasif";

                var activeFilters = WindowFilters?.Count(f => f.IsEnabled) ?? 0;
                var modeText = WindowFilterMode == WindowFilterMode.AllowList ? "İzin" : "Engel";
                return activeFilters > 0 ? $"🟢 {activeFilters} {modeText}" : "🟡 Filtre Yok";
            }
        }
        public string WindowFilteringStatusColor => IsWindowFilteringEnabled ? "Green" : "Red";

        public ObservableCollection<WindowFilter> WindowFilters => _settingsService?.Settings?.WindowFilters ?? new ObservableCollection<WindowFilter>();

        public WindowFilterMode WindowFilterMode => _settingsService?.Settings?.WindowFilterMode ?? WindowFilterMode.AllowList;

        // Learning Log Properties
        private string _learningLog = "Öğrenme logu burada görünecek...\n";
        public string LearningLog
        {
            get => _learningLog;
            set { _learningLog = value; OnPropertyChanged(); }
        }

        private ObservableCollection<LearningLogEntry> _learningLogEntries = new ObservableCollection<LearningLogEntry>();
        public ObservableCollection<LearningLogEntry> LearningLogEntries
        {
            get => _learningLogEntries;
            set { _learningLogEntries = value; OnPropertyChanged(); }
        }

        private int _totalLearnedSentences;
        public int TotalLearnedSentences
        {
            get => _totalLearnedSentences;
            set { _totalLearnedSentences = value; OnPropertyChanged(); }
        }

        // Statistics Properties
        private int _totalLearnedWords;
        public int TotalLearnedWords
        {
            get => _totalLearnedWords;
            set { _totalLearnedWords = value; OnPropertyChanged(); }
        }

        private int _totalSuggestionsGiven;
        public int TotalSuggestionsGiven
        {
            get => _totalSuggestionsGiven;
            set { _totalSuggestionsGiven = value; OnPropertyChanged(); }
        }

        private int _acceptedSuggestions;
        public int AcceptedSuggestions
        {
            get => _acceptedSuggestions;
            set { _acceptedSuggestions = value; OnPropertyChanged(); }
        }

        private double _accuracyRate;
        public double AccuracyRate
        {
            get => _accuracyRate;
            set { _accuracyRate = value; OnPropertyChanged(); }
        }



        // Progress Properties
        private double _vocabularyProgress;
        public double VocabularyProgress
        {
            get => _vocabularyProgress;
            set { _vocabularyProgress = value; OnPropertyChanged(); }
        }

        private string _vocabularyProgressText = "0 / 1000 kelime";
        public string VocabularyProgressText
        {
            get => _vocabularyProgressText;
            set { _vocabularyProgressText = value; OnPropertyChanged(); }
        }

        private double _predictionAccuracy;
        public double PredictionAccuracy
        {
            get => _predictionAccuracy;
            set { _predictionAccuracy = value; OnPropertyChanged(); }
        }

        private string _predictionAccuracyText = "0% doğruluk";
        public string PredictionAccuracyText
        {
            get => _predictionAccuracyText;
            set { _predictionAccuracyText = value; OnPropertyChanged(); }
        }

        private double _learningSpeed;
        public double LearningSpeed
        {
            get => _learningSpeed;
            set { _learningSpeed = value; OnPropertyChanged(); }
        }

        private string _learningSpeedText = "Yavaş";
        public string LearningSpeedText
        {
            get => _learningSpeedText;
            set { _learningSpeedText = value; OnPropertyChanged(); }
        }

        // N-Gram Settings
        private int _ngramDisplayCount = 10;
        public int NGramDisplayCount
        {
            get => _ngramDisplayCount;
            set { _ngramDisplayCount = value; OnPropertyChanged(); }
        }

        private int _ngramMinFrequency = 2;
        public int NGramMinFrequency
        {
            get => _ngramMinFrequency;
            set { _ngramMinFrequency = value; OnPropertyChanged(); }
        }

        public string ShortcutFilter
        {
            get => _shortcutFilter;
            set
            {
                _shortcutFilter = value;
                OnPropertyChanged();
                FilterShortcuts();
            }
        }



        public Shortcut? SelectedShortcut
        {
            get => _selectedShortcut;
            set
            {
                _selectedShortcut = value;
                OnPropertyChanged();
            }
        }





        public ICommand AddShortcutCommand { get; set; }
        public ICommand EditShortcutCommand { get; set; }
        public ICommand DeleteShortcutCommand { get; }

        public ICommand OpenSettingsCommand { get; set; }
        public ICommand StartTourCommand { get; set; }

        public MainViewModel(
            IShortcutService shortcutService,
            ISmartSuggestionsService smartSuggestionsService,
            ISettingsService settingsService,
            IKeyboardHookService keyboardHookService,
            IAdvancedInputService advancedInputService,
            ITourService tourService)
        {
            try
            {
                Console.WriteLine("*** MAINVIEWMODEL CONSTRUCTOR BAŞLADI ***");
                System.Diagnostics.Debug.WriteLine("*** MAINVIEWMODEL CONSTRUCTOR BAŞLADI ***");

                _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
                _smartSuggestionsService = smartSuggestionsService ?? throw new ArgumentNullException(nameof(smartSuggestionsService));
                _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
                _keyboardHookService = keyboardHookService ?? throw new ArgumentNullException(nameof(keyboardHookService));
                _advancedInputService = advancedInputService ?? throw new ArgumentNullException(nameof(advancedInputService));
                _tourService = tourService ?? throw new ArgumentNullException(nameof(tourService));

                Console.WriteLine("[DEBUG] Servisler atandı, PreviewOverlay oluşturuluyor...");

                // PreviewOverlay'i UI thread'de asenkron oluştur - donmayı önle
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        Console.WriteLine("[DEBUG] PreviewOverlay asenkron oluşturuluyor...");
                        _previewOverlay = new PreviewOverlay();
                        Console.WriteLine("[DEBUG] PreviewOverlay başarıyla oluşturuldu");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] PreviewOverlay oluşturma hatası: {ex.Message}");
                        Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                        _previewOverlay = null;
                    }
                }, DispatcherPriority.Background);

                Console.WriteLine("[DEBUG] Command'lar oluşturuluyor...");
                AddShortcutCommand = new RelayCommand(AddShortcut);
                EditShortcutCommand = new RelayCommand(EditShortcut, () => SelectedShortcut != null);
                DeleteShortcutCommand = new RelayCommand(DeleteShortcut, () => SelectedShortcut != null);

                OpenSettingsCommand = new RelayCommand(OpenSettings);
                StartTourCommand = new RelayCommand(StartTour);

                Console.WriteLine("[DEBUG] Servisler başlatılıyor...");
                InitializeServices();

                // İlk çalıştırma kontrolü - tur başlat
                CheckAndStartFirstRunTour();

                // Önizleme gizleme timer'ını başlat
                InitializePreviewTimer();

                Console.WriteLine("[DEBUG] MainViewModel constructor tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] MainViewModel constructor hatası: {ex}");
                throw;
            }
        }

        private async void InitializeServices()
        {
            try
            {
                Console.WriteLine("[DEBUG] InitializeServices başlıyor...");

                Console.WriteLine("[DEBUG] Settings yükleniyor...");
                await _settingsService.LoadSettingsAsync();

                Console.WriteLine("[DEBUG] Shortcuts yükleniyor...");
                await _shortcutService.LoadShortcutsAsync();

                Console.WriteLine("[DEBUG] Smart Suggestions başlatılıyor...");
                await _smartSuggestionsService.InitializeAsync();

                Console.WriteLine("[DEBUG] Event'ler bağlanıyor...");
                _shortcutService.Shortcuts.CollectionChanged += (s, e) =>
                {
                    FilterShortcuts();
                    UpdateStats();
                    UpdateAnalytics();
                };

                // Smart Suggestions event'lerini bağla
                _smartSuggestionsService.SuggestionsUpdated += OnSmartSuggestionsUpdated;
                _smartSuggestionsService.SuggestionAccepted += OnSmartSuggestionAccepted;

                // Settings değişikliklerini dinle
                _settingsService.SettingsChanged += OnSettingsChanged;

                // Klavye hook event'lerini bağla
                _keyboardHookService.KeyPressed += OnKeyPressed;
                _keyboardHookService.WordCompleted += OnWordCompleted;
                _keyboardHookService.SentenceCompleted += OnSentenceCompleted;
                _keyboardHookService.CtrlSpacePressed += OnCtrlSpacePressed;
                _keyboardHookService.TabPressed += OnTabPressed;
                _keyboardHookService.SpacePressed += OnSpacePressed;

                Console.WriteLine("[DEBUG] Klavye dinleme başlatılıyor...");
                _keyboardHookService.StartListening();
                Console.WriteLine($"[DEBUG] Klavye dinleme durumu: {_keyboardHookService.IsListening}");

                Console.WriteLine("[DEBUG] UI güncelleniyor...");
                FilterShortcuts();
                UpdateStats();
                UpdateAnalytics();

                Console.WriteLine("[DEBUG] Öğrenme logları yükleniyor...");
                // Öğrenme loglarını dosyadan yükle
                LoadLearningLogFromFile();

                Console.WriteLine("[DEBUG] Smart Suggestions dashboard yükleniyor...");
                // Dashboard verilerini arka planda yükle
                _ = Task.Run(async () => await RefreshSmartSuggestionsDataAsync());

                Console.WriteLine("[DEBUG] PreviewOverlay test ediliyor...");
                // PreviewOverlay'i test et
                TestPreviewOverlay();

                Console.WriteLine("[DEBUG] Pencere değişikliği algılama hazır (OnKeyPressed içinde kontrol edilecek)");

                // Kısayol önizleme paneli ayarlarda açıksa göster
                if (IsShortcutPreviewPanelVisible)
                {
                    Console.WriteLine("[DEBUG] Kısayol önizleme paneli ayarlarda açık, gösteriliyor...");
                    ShowShortcutPreviewPanel();
                }

                Console.WriteLine("[DEBUG] InitializeServices tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] InitializeServices hatası: {ex}");
                throw;
            }
        }

        private void SafeSetPreviewText(string text)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // PreviewOverlay constructor'da oluşturulmuş olmalı
                        if (_previewOverlay == null)
                        {
                            Console.WriteLine("[WARNING] PreviewOverlay henüz hazır değil, lazy loading yapılıyor...");
                            _previewOverlay = new PreviewOverlay();
                        }

                        _previewOverlay.SetText(text);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Visibility") || ex.Message.Contains("kapatıldıktan"))
                    {
                        Console.WriteLine("[DEBUG] PreviewOverlay kapatılmış, yeniden oluşturuluyor...");
                        _previewOverlay = new PreviewOverlay();
                        _previewOverlay.SetText(text);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] SafeSetPreviewText UI thread hatası: {ex.Message}");
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SafeSetPreviewText hatası: {ex.Message}");
            }
        }

        private void TestPreviewOverlay()
        {
            try
            {
                Console.WriteLine("[DEBUG] TestPreviewOverlay başlıyor...");
                WriteToLogFile("[DEBUG] TestPreviewOverlay başlıyor...");

                if (_previewOverlay == null)
                {
                    Console.WriteLine("[ERROR] _previewOverlay null, test edilemiyor");
                    WriteToLogFile("[ERROR] _previewOverlay null, test edilemiyor");
                    return;
                }

                Console.WriteLine("[DEBUG] _previewOverlay mevcut, test başlatılıyor...");
                WriteToLogFile("[DEBUG] _previewOverlay mevcut, test başlatılıyor...");

                // WindowHelper durumunu test et
                bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive();
                Console.WriteLine($"[DEBUG] ShouldTextExpansionBeActive: {shouldBeActive}");
                WriteToLogFile($"[DEBUG] ShouldTextExpansionBeActive: {shouldBeActive}");

                // Smart Suggestions durumunu test et
                bool smartEnabled = IsSmartSuggestionsEnabled;
                Console.WriteLine($"[DEBUG] IsSmartSuggestionsEnabled: {smartEnabled}");
                WriteToLogFile($"[DEBUG] IsSmartSuggestionsEnabled: {smartEnabled}");

                // İlk açılışta önizlemeyi gizle (sadece yazı yazarken görünecek)
                HidePreview();
                Console.WriteLine("[DEBUG] İlk açılışta önizleme gizlendi (sadece yazı yazarken görünecek)");
                WriteToLogFile("[DEBUG] İlk açılışta önizleme gizlendi (sadece yazı yazarken görünecek)");

                // AYAR DEBUG - Başlangıçta ayarları kontrol et
                Console.WriteLine($"[AYAR DEBUG] Constructor'da SmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
                WriteToLogFile($"[AYAR DEBUG] Constructor'da SmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");

                // Akıllı öneriler durumunu test et
                TestSmartSuggestions();

                Console.WriteLine("[DEBUG] TestPreviewOverlay tamamlandı");
                WriteToLogFile("[DEBUG] TestPreviewOverlay tamamlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TestPreviewOverlay hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] TestPreviewOverlay hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                WriteToLogFile($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private void WriteToLogFile(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_new.log");
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Log yazma hatası olursa sessizce devam et
            }
        }

        private void InitializePreviewTimer()
        {
            try
            {
                // 3 saniye sonra preview'ı otomatik kapatan timer
                _previewAutoHideTimer = new System.Timers.Timer(3000); // 3 saniye
                _previewAutoHideTimer.Elapsed += OnPreviewAutoHideTimerElapsed;
                _previewAutoHideTimer.AutoReset = false; // Sadece bir kez çalışsın
                Console.WriteLine("[DEBUG] Preview otomatik kapanma timer'ı başlatıldı (3 saniye)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] InitializePreviewTimer hatası: {ex.Message}");
            }
        }

        private void OnPreviewAutoHideTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Console.WriteLine("[TIMER] Preview otomatik kapanma timer'ı tetiklendi (3 saniye sonra)");

                // Preview'ı gizle (3 saniye boyunca yazı yazılmadı)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HidePreview();
                    Console.WriteLine("[TIMER] Preview 3 saniye sonra otomatik kapandı");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnPreviewAutoHideTimerElapsed hatası: {ex.Message}");
            }
        }

        private void RestartPreviewAutoHideTimer()
        {
            try
            {
                // Mevcut timer'ı durdur
                _previewAutoHideTimer?.Stop();

                // Timer'ı yeniden başlat (3 saniye)
                _previewAutoHideTimer?.Start();

                Console.WriteLine("[TIMER] Preview otomatik kapanma timer'ı yeniden başlatıldı (3 saniye)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RestartPreviewAutoHideTimer hatası: {ex.Message}");
            }
        }

        private async void TestSmartSuggestions()
        {
            try
            {
                Console.WriteLine("[TEST] *** AKILLI ÖNERİLER TEST BAŞLIYOR ***");
                Console.WriteLine($"[TEST] IsSmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
                Console.WriteLine($"[TEST] SmartSuggestionsService null mu: {_smartSuggestionsService == null}");
                Console.WriteLine($"[TEST] Settings.SmartSuggestionsEnabled: {_settingsService.Settings.SmartSuggestionsEnabled}");
                Console.WriteLine($"[TEST] Settings.LearningEnabled: {_settingsService.Settings.LearningEnabled}");

                if (_smartSuggestionsService != null)
                {
                    Console.WriteLine($"[TEST] SmartSuggestionsService.IsEnabled: {_smartSuggestionsService.IsEnabled}");

                    // Test öğrenme
                    Console.WriteLine("[TEST] Test öğrenme başlıyor...");
                    await _smartSuggestionsService.LearnFromTextAsync("Merhaba bugün nasılsın");
                    Console.WriteLine("[TEST] Test öğrenme tamamlandı");

                    // Test öneri alma
                    Console.WriteLine("[TEST] Test öneri alma başlıyor...");
                    var testSuggestions = await _smartSuggestionsService.GetSuggestionsAsync("Merhaba bugün", 3);
                    Console.WriteLine($"[TEST] Test öneriler: {testSuggestions.Count} adet");

                    foreach (var sug in testSuggestions)
                    {
                        Console.WriteLine($"[TEST] Öneri: '{sug.Text}' (Confidence: {sug.Confidence:P0})");
                    }
                }

                Console.WriteLine("[TEST] *** AKILLI ÖNERİLER TEST BİTTİ ***");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TestSmartSuggestions hatası: {ex.Message}");
            }
        }

        private void UpdateStats()
        {
            OnPropertyChanged(nameof(TotalShortcuts));
            OnPropertyChanged(nameof(TotalExpansions));
            OnPropertyChanged(nameof(IsListening));
        }

        private void UpdateAnalytics()
        {
            // Update top shortcuts
            TopShortcuts.Clear();
            var topShortcuts = Shortcuts
                .OrderByDescending(s => s.UsageCount)
                .Take(5);
            foreach (var shortcut in topShortcuts)
            {
                TopShortcuts.Add(shortcut);
            }

            // Update recent activities
            RecentActivities.Clear();
            var recentActivities = Shortcuts
                .OrderByDescending(s => s.LastUsed)
                .Take(5);
            foreach (var shortcut in recentActivities)
            {
                RecentActivities.Add(shortcut);
            }
        }

        private async void OnKeyPressed(string buffer)
        {
            Console.WriteLine($"[KEYPRESS] *** OnKeyPressed çağrıldı, buffer: '{buffer}' ***");
            WriteToLogFile($"[KEYPRESS] *** OnKeyPressed çağrıldı, buffer: '{buffer}' ***");

            // Yazı yazma zamanını güncelle
            _lastKeyPressTime = DateTime.Now;

            // Preview otomatik kapanma timer'ını yeniden başlat
            RestartPreviewAutoHideTimer();

            // Aktif pencere değişikliği kontrolü
            string currentActiveWindow = WindowHelper.GetActiveWindowTitle();
            if (!string.IsNullOrEmpty(_lastActiveWindow) && _lastActiveWindow != currentActiveWindow)
            {
                Console.WriteLine($"[FOCUS] Pencere değişti: '{_lastActiveWindow}' -> '{currentActiveWindow}'");
                WriteToLogFile($"[FOCUS] Pencere değişti: '{_lastActiveWindow}' -> '{currentActiveWindow}'");

                // Pencere değiştiğinde ön izleme penceresini kapat
                Console.WriteLine("[FOCUS] Pencere değişti, ön izleme penceresi kapatılıyor");
                WriteToLogFile("[FOCUS] Pencere değişti, ön izleme penceresi kapatılıyor");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    HidePreview();
                });
            }
            _lastActiveWindow = currentActiveWindow;

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled, WindowFilterMode);
            Console.WriteLine($"[KEYPRESS] ShouldTextExpansionBeActive: {shouldBeActive}");
            WriteToLogFile($"[KEYPRESS] ShouldTextExpansionBeActive: {shouldBeActive}");

            if (!shouldBeActive)
            {
                Console.WriteLine($"[KEYPRESS] OnKeyPressed: Pencere filtreleri nedeniyle işlem yapılmıyor");
                WriteToLogFile($"[KEYPRESS] OnKeyPressed: Pencere filtreleri nedeniyle işlem yapılmıyor");
                return;
            }

            _contextBuffer = buffer;
            Console.WriteLine($"[KEYPRESS] Context buffer güncellendi: '{_contextBuffer}'");
            WriteToLogFile($"[KEYPRESS] Context buffer güncellendi: '{_contextBuffer}'");
            Console.WriteLine($"[DEBUG] IsSmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
            WriteToLogFile($"[DEBUG] IsSmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
            Console.WriteLine($"[DEBUG] Buffer boş mu: {string.IsNullOrWhiteSpace(buffer)}");
            WriteToLogFile($"[DEBUG] Buffer boş mu: {string.IsNullOrWhiteSpace(buffer)}");

            // Akıllı öneriler etkinse kapsamlı öneri kontrolü yap
            if (IsSmartSuggestionsEnabled && !string.IsNullOrWhiteSpace(buffer))
            {
                Console.WriteLine($"[DEBUG] Akıllı öneriler etkin, ProcessSmartSuggestionsAsync çağrılıyor...");
                WriteToLogFile($"[DEBUG] Akıllı öneriler etkin, ProcessSmartSuggestionsAsync çağrılıyor...");
                await ProcessSmartSuggestionsAsync(buffer);
            }
            else
            {
                Console.WriteLine($"[DEBUG] Akıllı öneriler atlandı - Enabled: {IsSmartSuggestionsEnabled}, Buffer boş: {string.IsNullOrWhiteSpace(buffer)}");
                WriteToLogFile($"[DEBUG] Akıllı öneriler atlandı - Enabled: {IsSmartSuggestionsEnabled}, Buffer boş: {string.IsNullOrWhiteSpace(buffer)}");

                // Akıllı öneriler kapalıysa mevcut önerileri temizle
                _currentSuggestion = "";
                _currentSmartSuggestions.Clear();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SmartSuggestions.Clear();
                });
            }

            // SONRA preview'ı göster (akıllı öneriler öncelikli)
            ShowPreview(buffer);
        }

        private async Task ProcessSmartSuggestionsAsync(string buffer)
        {
            try
            {
                Console.WriteLine($"[SMART SUGGESTIONS] *** ProcessSmartSuggestionsAsync başlıyor: '{buffer}' ***");
                WriteToLogFile($"[SMART SUGGESTIONS] *** ProcessSmartSuggestionsAsync başlıyor: '{buffer}' ***");

                var words = buffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"[SMART SUGGESTIONS] Words count: {words.Length}");
                WriteToLogFile($"[SMART SUGGESTIONS] Words count: {words.Length}");
                Console.WriteLine($"[SMART SUGGESTIONS] Buffer ends with space: {buffer.EndsWith(" ")}");
                WriteToLogFile($"[SMART SUGGESTIONS] Buffer ends with space: {buffer.EndsWith(" ")}");

                // 1. ÖNCE KELİME TAMAMLAMA KONTROL ET (henüz tamamlanmamış kelime varsa)
                bool hasWordCompletion = false;
                if (words.Length > 0 && !buffer.EndsWith(" "))
                {
                    var lastWord = words.Last();
                    Console.WriteLine($"[SMART SUGGESTIONS] Last word: '{lastWord}', length: {lastWord.Length}");
                    WriteToLogFile($"[SMART SUGGESTIONS] Last word: '{lastWord}', length: {lastWord.Length}");

                    if (lastWord.Length >= 2)
                    {
                        Console.WriteLine($"[SMART SUGGESTIONS] Kelime tamamlama kontrol ediliyor: '{lastWord}'");
                        WriteToLogFile($"[SMART SUGGESTIONS] Kelime tamamlama kontrol ediliyor: '{lastWord}'");
                        await UpdateWordCompletionAsync(lastWord, buffer);

                        // Kelime tamamlama önerisi bulunduysa işaretle
                        if (_currentSmartSuggestions.Count > 0)
                        {
                            Console.WriteLine($"[SMART SUGGESTIONS] Kelime tamamlama önerisi bulundu: {_currentSmartSuggestions.Count} öneri");
                            hasWordCompletion = true;
                        }
                    }
                }

                // 2. SÜREKLI SONRAKI KELİME TAHMİNİ (cümle yapısına göre)
                // HER DURUMDA sonraki kelimeyi tahmin et - sürekli çalışsın
                Console.WriteLine($"[SMART SUGGESTIONS] *** SÜREKLİ SONRAKI KELİME TAHMİNİ BAŞLIYOR ***");
                WriteToLogFile($"[SMART SUGGESTIONS] *** SÜREKLİ SONRAKI KELİME TAHMİNİ BAŞLIYOR ***");

                await PredictNextWordContinuously(words, buffer);

                Console.WriteLine($"[SMART SUGGESTIONS] ProcessSmartSuggestionsAsync tamamlandı. Öneri sayısı: {_currentSmartSuggestions.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ProcessSmartSuggestionsAsync hatası: {ex.Message}");
            }
        }

        // SÜREKLİ SONRAKI KELİME TAHMİN SİSTEMİ
        private async Task PredictNextWordContinuously(string[] words, string buffer)
        {
            try
            {
                Console.WriteLine($"[NEXT WORD] *** PredictNextWordContinuously başlıyor ***");
                WriteToLogFile($"[NEXT WORD] *** PredictNextWordContinuously başlıyor ***");
                Console.WriteLine($"[NEXT WORD] Kelime sayısı: {words.Length}, Buffer: '{buffer}'");
                WriteToLogFile($"[NEXT WORD] Kelime sayısı: {words.Length}, Buffer: '{buffer}'");

                if (words.Length == 0)
                {
                    Console.WriteLine($"[NEXT WORD] Kelime yok, tahmin atlanıyor");
                    return;
                }

                // CÜMLE YAPISI ANALİZİ - Son kelimeleri analiz et
                string context = "";
                string analysisType = "";

                // ÖNEMLİ: Buffer boşlukla bitmiyorsa (kelime yazılıyor), son kelimeyi çıkar
                bool isTypingWord = !buffer.EndsWith(" ");
                string[] contextWords = words;

                if (isTypingWord && words.Length > 0)
                {
                    // Kullanıcı kelime yazıyor - son kelimeyi çıkarıp önceki kelimeleri analiz et
                    contextWords = words.Take(words.Length - 1).ToArray();
                    Console.WriteLine($"[NEXT WORD] Kullanıcı kelime yazıyor, son kelime çıkarıldı. Analiz edilecek kelimeler: {contextWords.Length}");
                    WriteToLogFile($"[NEXT WORD] Kullanıcı kelime yazıyor, son kelime çıkarıldı. Analiz edilecek kelimeler: {contextWords.Length}");
                }

                if (contextWords.Length >= 4)
                {
                    // 4-gram analizi (en güçlü tahmin)
                    context = string.Join(" ", contextWords.TakeLast(4));
                    analysisType = "4-gram";
                }
                else if (contextWords.Length >= 3)
                {
                    // 3-gram analizi (trigram)
                    context = string.Join(" ", contextWords.TakeLast(3));
                    analysisType = "3-gram (trigram)";
                }
                else if (contextWords.Length >= 2)
                {
                    // 2-gram analizi (bigram)
                    context = string.Join(" ", contextWords.TakeLast(2));
                    analysisType = "2-gram (bigram)";
                }
                else if (contextWords.Length >= 1)
                {
                    // 1-gram analizi (unigram)
                    context = contextWords.Last();
                    analysisType = "1-gram (unigram)";
                }
                else
                {
                    // Hiç kelime yok - tahmin yapılamaz
                    Console.WriteLine($"[NEXT WORD] Analiz edilecek kelime yok, tahmin atlanıyor");
                    WriteToLogFile($"[NEXT WORD] Analiz edilecek kelime yok, tahmin atlanıyor");
                    return;
                }

                Console.WriteLine($"[NEXT WORD] {analysisType} analizi yapılıyor: '{context}'");
                WriteToLogFile($"[NEXT WORD] {analysisType} analizi yapılıyor: '{context}'");

                // Akıllı öneri servisinden sonraki kelime tahminlerini al
                var suggestions = await _smartSuggestionsService.GetSuggestionsAsync(context, 5);

                if (suggestions.Any())
                {
                    Console.WriteLine($"[NEXT WORD] {suggestions.Count} sonraki kelime önerisi bulundu");
                    WriteToLogFile($"[NEXT WORD] {suggestions.Count} sonraki kelime önerisi bulundu");

                    // En iyi öneriyi seç
                    var bestSuggestion = suggestions.First();
                    _currentSuggestion = bestSuggestion.Text;
                    _currentSmartSuggestions.Clear();
                    _currentSmartSuggestions.AddRange(suggestions);

                    Console.WriteLine($"[NEXT WORD] En iyi sonraki kelime önerisi: '{bestSuggestion.Text}' (Güven: {bestSuggestion.Confidence:P0})");
                    WriteToLogFile($"[NEXT WORD] En iyi sonraki kelime önerisi: '{bestSuggestion.Text}' (Güven: {bestSuggestion.Confidence:P0})");

                    // UI'ı güncelle
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SmartSuggestions.Clear();
                        foreach (var suggestion in suggestions.Take(5))
                        {
                            SmartSuggestions.Add(suggestion);
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"[NEXT WORD] {analysisType} için sonraki kelime önerisi bulunamadı");
                    WriteToLogFile($"[NEXT WORD] {analysisType} için sonraki kelime önerisi bulunamadı");

                    // Daha basit analiz dene (bir seviye aşağı)
                    if (words.Length > 1)
                    {
                        await TrySimplePrediction(words);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] PredictNextWordContinuously hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] PredictNextWordContinuously hatası: {ex.Message}");
            }
        }

        // BASİT TAHMİN SİSTEMİ (fallback)
        private async Task TrySimplePrediction(string[] words)
        {
            try
            {
                Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin sistemi başlıyor");
                WriteToLogFile($"[SIMPLE PREDICTION] Basit tahmin sistemi başlıyor");

                // Son kelimeye göre basit tahmin
                var lastWord = words.Last().ToLower();
                var simplePredictions = GetSimpleNextWordPredictions(lastWord);

                if (simplePredictions.Any())
                {
                    _currentSuggestion = simplePredictions.First().Text;
                    _currentSmartSuggestions.Clear();
                    _currentSmartSuggestions.AddRange(simplePredictions);

                    Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin bulundu: '{_currentSuggestion}'");
                    WriteToLogFile($"[SIMPLE PREDICTION] Basit tahmin bulundu: '{_currentSuggestion}'");

                    // UI'ı güncelle
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SmartSuggestions.Clear();
                        foreach (var suggestion in simplePredictions.Take(3))
                        {
                            SmartSuggestions.Add(suggestion);
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin de bulunamadı");
                    WriteToLogFile($"[SIMPLE PREDICTION] Basit tahmin de bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TrySimplePrediction hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] TrySimplePrediction hatası: {ex.Message}");
            }
        }

        private void ShowPreview(string buffer)
        {
            try
            {
                Console.WriteLine($"[PREVIEW] *** ShowPreview çağrıldı, buffer: '{buffer}' ***");
                WriteToLogFile($"[PREVIEW] *** ShowPreview çağrıldı, buffer: '{buffer}' ***");

                // Thread safety check
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => ShowPreview(buffer));
                    return;
                }

                // Buffer boşsa önizlemeyi gizle
                if (string.IsNullOrEmpty(buffer?.Trim()))
                {
                    Console.WriteLine("[PREVIEW] Buffer boş, önizleme gizleniyor");
                    HidePreview();
                    return;
                }

                // PreviewOverlay null kontrolü
                if (_previewOverlay == null)
                {
                    Console.WriteLine("[ERROR] _previewOverlay null, preview gösterilemiyor");
                    WriteToLogFile("[ERROR] _previewOverlay null, preview gösterilemiyor");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowPreview başlangıç hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] ShowPreview başlangıç hatası: {ex.Message}");
                return;
            }

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa önizleme gösterme
            bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled, WindowFilterMode);
            Console.WriteLine($"[PREVIEW] ShouldTextExpansionBeActive: {shouldBeActive}");
            WriteToLogFile($"[PREVIEW] ShouldTextExpansionBeActive: {shouldBeActive}");

            if (!shouldBeActive)
            {
                Console.WriteLine("[PREVIEW] Pencere filtreleri nedeniyle metin genişletme duraklatıldı");
                WriteToLogFile("[PREVIEW] Pencere filtreleri nedeniyle metin genişletme duraklatıldı");
                // Uyarı mesajı gösterme, sadece önizlemeyi gizle
                HidePreview();
                return;
            }

            if (string.IsNullOrEmpty(buffer))
            {
                Console.WriteLine("[PREVIEW] Buffer boş, önizleme gizleniyor");
                WriteToLogFile("[PREVIEW] Buffer boş, önizleme gizleniyor");
                HidePreview();
                return;
            }

            // ÖNCE KISAYOLLARI KONTROL ET (daha spesifik ve hızlı)
            // Buffer'ın son kısmını kontrol et (son 20 karakter)
            var checkBuffer = buffer.Length > 20 ? buffer.Substring(buffer.Length - 20) : buffer;

            foreach (var shortcut in Shortcuts.OrderByDescending(s => s.Key.Length))
            {
                if (checkBuffer.EndsWith(shortcut.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // Kısayolun tam olarak yazıldığını kontrol et
                    var beforeShortcut = checkBuffer.Length > shortcut.Key.Length ?
                        checkBuffer[checkBuffer.Length - shortcut.Key.Length - 1] : ' ';

                    if (char.IsWhiteSpace(beforeShortcut) || char.IsPunctuation(beforeShortcut) || checkBuffer.Length == shortcut.Key.Length)
                    {
                        // Önizleme metnini 150 karakterle sınırla
                        string previewText = shortcut.Expansion;
                        if (previewText.Length > 150)
                        {
                            previewText = previewText.Substring(0, 147) + "...";
                        }

                        // Kısayolu akıllı öneri olarak ayarla (Tab tuşu için)
                        _currentSuggestion = shortcut.Expansion;

                        // Akıllı öneriler listesini güncelle
                        var shortcutSuggestion = new SmartSuggestion
                        {
                            Text = shortcut.Expansion,
                            Confidence = 1.0, // Kısayollar %100 güvenilir
                            Type = SuggestionType.Phrase, // Kısayollar phrase olarak kabul edilir
                            Frequency = 1,
                            Context = shortcut.Key
                        };

                        _currentSmartSuggestions.Clear();
                        _currentSmartSuggestions.Add(shortcutSuggestion);

                        // UI'da akıllı öneriler listesini güncelle
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SmartSuggestions.Clear();
                            SmartSuggestions.Add(shortcutSuggestion);
                        });

                        SafeSetPreviewText($"→ {previewText}");
                        Console.WriteLine($"[PREVIEW] Kısayol gösteriliyor ve akıllı öneriler güncellendi: {previewText}");
                        return;
                    }
                }
            }

            // SONRA AKILLI ÖNERİLERİ KONTROL ET
            if (IsSmartSuggestionsEnabled && !string.IsNullOrEmpty(_currentSuggestion))
            {
                Console.WriteLine($"[PREVIEW] Mevcut akıllı öneri var: {_currentSuggestion}");

                // Akıllı öneriyi preview'da göster
                var previewText = $"💡 {_currentSuggestion}";
                if (_currentSmartSuggestions.Count > 0)
                {
                    // Confidence bilgisini kaldırdık - sadece temiz öneri
                    previewText = $"💡 {_currentSuggestion}";
                }

                SafeSetPreviewText(previewText);
                Console.WriteLine($"[PREVIEW] Akıllı öneri gösteriliyor: {previewText}");
                return;
            }

            Console.WriteLine($"[PREVIEW] Hiçbir öneri bulunamadı, buffer: '{buffer}', akıllı öneri: '{_currentSuggestion}'");

            // Akıllı öneriler varsa onları göster, yoksa test preview'ı göster
            if (_currentSmartSuggestions.Count > 0)
            {
                var suggestion = _currentSmartSuggestions[0];
                SafeSetPreviewText($"💡 {suggestion.Text} (Ctrl+Space ile kabul et - {suggestion.Confidence:P0})");
                Console.WriteLine($"[PREVIEW] Akıllı öneri gösteriliyor: {suggestion.Text}");
                WriteToLogFile($"[PREVIEW] Akıllı öneri gösteriliyor: {suggestion.Text}");
                return;
            }

            // Test preview'ı kaldırdık - sadece gerçek akıllı öneriler gösterilecek

            // ÖNEMLİ: Sadece tahmin göster - yazdıkları görünmesin!
            if (!string.IsNullOrEmpty(buffer.Trim()))
            {
                var words = buffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    var previewText = "";

                    // SADECE TAHMİN GÖSTER - YAZDIKLARINI GÖSTERME!
                    if (!string.IsNullOrEmpty(_currentSuggestion))
                    {
                        // Kelime tamamlama mı yoksa sonraki kelime tahmini mi?
                        if (!buffer.EndsWith(" ") && words.Length > 0)
                        {
                            var lastWord = words.Last();
                            if (_currentSuggestion.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                            {
                                // Kelime tamamlama - sadece tamamlanmış halini göster
                                previewText = $"🔤 {_currentSuggestion}";
                            }
                            else
                            {
                                // Sonraki kelime tahmini - sadece tahmini göster
                                previewText = $"🔮 {_currentSuggestion}";
                            }
                        }
                        else
                        {
                            // Boşluk sonrası - sadece sonraki kelime tahmini
                            previewText = $"🔮 {_currentSuggestion}";
                        }
                    }
                    else
                    {
                        // Tahmin yok - önizlemeyi gizle
                        Console.WriteLine("[PREVIEW] Tahmin yok, önizleme gizleniyor");
                        WriteToLogFile("[PREVIEW] Tahmin yok, önizleme gizleniyor");
                        HidePreview();
                        return;
                    }

                    SafeSetPreviewText(previewText);
                    Console.WriteLine($"[PREVIEW] Sadece tahmin gösteriliyor: {previewText}");
                    WriteToLogFile($"[PREVIEW] Sadece tahmin gösteriliyor: {previewText}");
                }
            }
            else
            {
                // Buffer boş - önizlemeyi gizle
                Console.WriteLine("[PREVIEW] Buffer boş, önizleme gizleniyor");
                WriteToLogFile("[PREVIEW] Buffer boş, önizleme gizleniyor");
                HidePreview();
                _currentSmartSuggestions.Clear();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SmartSuggestions.Clear();
                });
            }
        }

        private async void OnWordCompleted(string word)
        {
            _contextBuffer += word + " "; // Context için boşluk ekle
            if (_contextBuffer.Length > 200)
            {
                _contextBuffer = _contextBuffer.Substring(_contextBuffer.Length - 200);
            }

            // Kelimeyi temizle
            var cleanWord = word.Trim();

            // Kısayol genişletme kontrolü (sadece izin verilen pencerelerde)
            if (WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled) &&
                !string.IsNullOrEmpty(cleanWord) &&
                _shortcutService.TryExpandShortcut(cleanWord, out string expansion))
            {
                SafeSetPreviewText($"🔄 Kısayol genişletildi: '{cleanWord}' → '{expansion}'");
            }
        }

        private async void OnSentenceCompleted(string sentence)
        {
            Console.WriteLine($"[SMART SUGGESTIONS] Cümle tamamlandı: '{sentence}'");
            WriteToLogFile($"[SMART SUGGESTIONS] Cümle tamamlandı: '{sentence}'");

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
                return;

            // Cümle temizle - noktalama işaretlerini kaldır
            var cleanSentence = CleanSentence(sentence);
            Console.WriteLine($"[SMART SUGGESTIONS] Temizlenmiş cümle: '{cleanSentence}'");
            WriteToLogFile($"[SMART SUGGESTIONS] Temizlenmiş cümle: '{cleanSentence}'");

            // GELİŞMİŞ ÖĞRENME: Hem kelimeleri hem de kelime çiftlerini öğren
            if (IsSmartSuggestionsEnabled && !string.IsNullOrWhiteSpace(cleanSentence))
            {
                LearnSimpleWords(cleanSentence);
                LearnWordPairs(cleanSentence); // Yeni: Kelime çiftlerini öğren
                Console.WriteLine($"[SMART SUGGESTIONS] Cümle öğrenildi: '{cleanSentence}'");
                WriteToLogFile($"[SMART SUGGESTIONS] Cümle öğrenildi: '{cleanSentence}'");

                // Akıllı öneriler servisine de öğret
                try
                {
                    await _smartSuggestionsService.LearnFromTextAsync(cleanSentence);
                    Console.WriteLine($"[SMART SUGGESTIONS] Cümle akıllı servise öğretildi: '{cleanSentence}'");
                    WriteToLogFile($"[SMART SUGGESTIONS] Cümle akıllı servise öğretildi: '{cleanSentence}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Akıllı servise öğretme hatası: {ex.Message}");
                    WriteToLogFile($"[ERROR] Akıllı servise öğretme hatası: {ex.Message}");
                }

                // Log'a ekle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddToLearningLog(cleanSentence);
                });
            }
        }

        // BASİT ÖĞRENME FONKSİYONU
        private void LearnSimpleWords(string sentence)
        {
            try
            {
                Console.WriteLine($"[DEBUG] LearnSimpleWords çağrıldı: '{sentence}'");
                WriteToLogFile($"[DEBUG] LearnSimpleWords çağrıldı: '{sentence}'");

                // Cümleyi kelimelere ayır
                var words = sentence.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    var cleanWord = word.Trim().ToLower();

                    // En az 3 karakter olan kelimeleri öğren
                    if (cleanWord.Length >= 3 && !_learnedWords.Contains(cleanWord))
                    {
                        _learnedWords.Add(cleanWord);
                        Console.WriteLine($"[DEBUG] Yeni kelime öğrenildi: '{cleanWord}'");
                        WriteToLogFile($"[DEBUG] Yeni kelime öğrenildi: '{cleanWord}'");
                    }
                }

                Console.WriteLine($"[DEBUG] Toplam öğrenilen kelime sayısı: {_learnedWords.Count}");
                WriteToLogFile($"[DEBUG] Toplam öğrenilen kelime sayısı: {_learnedWords.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LearnSimpleWords hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] LearnSimpleWords hatası: {ex.Message}");
            }
        }

        // KELİME ÇİFTLERİNİ ÖĞRENME FONKSİYONU
        private readonly Dictionary<string, List<string>> _learnedWordPairs = new Dictionary<string, List<string>>();

        private void LearnWordPairs(string sentence)
        {
            try
            {
                Console.WriteLine($"[DEBUG] LearnWordPairs çağrıldı: '{sentence}'");
                WriteToLogFile($"[DEBUG] LearnWordPairs çağrıldı: '{sentence}'");

                // Cümleyi kelimelere ayır
                var words = sentence.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Kelime çiftlerini öğren (bigram)
                for (int i = 0; i < words.Length - 1; i++)
                {
                    var firstWord = words[i].Trim().ToLower();
                    var secondWord = words[i + 1].Trim().ToLower();

                    if (firstWord.Length >= 2 && secondWord.Length >= 2)
                    {
                        if (!_learnedWordPairs.ContainsKey(firstWord))
                        {
                            _learnedWordPairs[firstWord] = new List<string>();
                        }

                        if (!_learnedWordPairs[firstWord].Contains(secondWord))
                        {
                            _learnedWordPairs[firstWord].Add(secondWord);
                            Console.WriteLine($"[DEBUG] Yeni kelime çifti öğrenildi: '{firstWord}' → '{secondWord}'");
                            WriteToLogFile($"[DEBUG] Yeni kelime çifti öğrenildi: '{firstWord}' → '{secondWord}'");
                        }
                    }
                }

                Console.WriteLine($"[DEBUG] Toplam öğrenilen kelime çifti sayısı: {_learnedWordPairs.Count}");
                WriteToLogFile($"[DEBUG] Toplam öğrenilen kelime çifti sayısı: {_learnedWordPairs.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LearnWordPairs hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] LearnWordPairs hatası: {ex.Message}");
            }
        }

        private bool IsSentenceEnding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var sentenceEnders = new char[] { '.', '!', '?', ';', ':', ',' };
            return sentenceEnders.Any(ender => text.TrimEnd().EndsWith(ender));
        }

        private string CleanSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return string.Empty;

            // Cümle sonundaki noktalama işaretlerini kaldır ve temizle
            return sentence.Trim().TrimEnd('.', '!', '?', ';', ':', ',', '-').Trim();
        }

        private string _currentSuggestion = "";

        private async Task ShowSmartSuggestionPreview(string buffer)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ShowSmartSuggestionPreview çağrıldı, buffer: '{buffer}', öneri sayısı: {_currentSmartSuggestions.Count}");

                // Aktif pencere kontrolü
                if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
                {
                    Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle preview gösterilmiyor");
                    return;
                }

                if (_currentSmartSuggestions.Count > 0)
                {
                    var suggestion = _currentSmartSuggestions[0];
                    _currentSuggestion = suggestion.Text;

                    // Preview'da akıllı öneriyi göster
                    var previewText = $"💡 {suggestion.Text}";
                    Console.WriteLine($"[SMART SUGGESTIONS] Preview gösteriliyor: {previewText}");

                    // Preview overlay'de göster - UI thread'de çalıştır
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            SafeSetPreviewText(previewText);

                            // UI'da akıllı öneriler listesini güncelle
                            SmartSuggestions.Clear();
                            foreach (var smartSuggestion in _currentSmartSuggestions.Take(5))
                            {
                                SmartSuggestions.Add(smartSuggestion);
                            }

                            Console.WriteLine($"[DEBUG] Preview overlay SetText çağrıldı: {previewText}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Preview overlay SetText hatası: {ex.Message}");
                        }
                    });
                }
                else
                {
                    _currentSuggestion = "";
                    Console.WriteLine($"[SMART SUGGESTIONS] Öneri yok");

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SmartSuggestions.Clear();

                        // Öneri yok - önizlemeyi gizle
                        Console.WriteLine($"[SMART SUGGESTIONS] Öneri yok, önizleme gizleniyor");
                        HidePreview();
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowSmartSuggestionPreview hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private async void OnCtrlSpacePressed()
        {
            Console.WriteLine("[DEBUG] *** Ctrl+Space basıldı ***");

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
            {
                Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle Ctrl+Space işlemi atlandı");
                return;
            }

            // İlk akıllı öneriyi uygula
            if (_currentSmartSuggestions.Count > 0)
            {
                var firstSuggestion = _currentSmartSuggestions[0];
                Console.WriteLine($"[DEBUG] *** Ctrl+Space ile öneri uygulanıyor: {firstSuggestion.Text} ***");

                // Öneriyi klavye ile yaz
                await SendTextToActiveWindow(firstSuggestion.Text);

                // Preview'ı gizleme, hemen yeni tahmin yap
                // SafeSetPreviewText mesajı kaldırıldı - sistem hemen yeni tahmin yapacak

                // Öneriyi temizle
                _currentSmartSuggestions.Clear();
                _currentSuggestion = "";

                // UI'da akıllı öneriler listesini temizle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SmartSuggestions.Clear();
                });
            }
            else
            {
                Console.WriteLine($"[DEBUG] Öneri yok. Count: {_currentSmartSuggestions.Count}");
            }
        }

        private bool OnTabPressed()
        {
            Console.WriteLine("[DEBUG] *** Tab tuşu basıldı ***");
            WriteToLogFile("[DEBUG] *** Tab tuşu basıldı ***");

            // Eğer pencere filtrelerine uymuyorsa Tab'ı engelleme
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
            {
                Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle Tab işlemi atlandı - normal Tab işlevi");
                WriteToLogFile("[DEBUG] Pencere filtreleri nedeniyle Tab işlemi atlandı - normal Tab işlevi");
                return false; // Tab'ı engelleme
            }

            // Öneri yoksa Tab tuşunu engelleme - normal Tab işlevine izin ver
            if (_currentSmartSuggestions.Count == 0 && string.IsNullOrEmpty(_currentSuggestion))
            {
                Console.WriteLine("[DEBUG] Öneri yok - Tab tuşunun normal işlevine izin ver");
                WriteToLogFile("[DEBUG] Öneri yok - Tab tuşunun normal işlevine izin ver");
                return false; // Tab'ı engelleme - normal işlevine izin ver
            }

            // Öneri var - Tab'ı işle ve engelle
            Console.WriteLine("[DEBUG] Öneri var - Tab'ı metin tamamlama için kullan");
            WriteToLogFile("[DEBUG] Öneri var - Tab'ı metin tamamlama için kullan");

            // Async işlemi başlat
            _ = Task.Run(async () => await ProcessTabForTextCompletion());

            return true; // Tab'ı engelle - metin tamamlama için kullanıldı
        }

        private async Task ProcessTabForTextCompletion()
        {
            try
            {
                // Geçerli bir akıllı öneri var mı?
                if (_currentSmartSuggestions.Count > 0)
                {
                    var suggestion = _currentSmartSuggestions[0];
                    Console.WriteLine($"[DEBUG] *** Tab ile öneri kabul ediliyor: {suggestion.Text} (Type: {suggestion.Type}) ***");
                    WriteToLogFile($"[DEBUG] *** Tab ile öneri kabul ediliyor: {suggestion.Text} (Type: {suggestion.Type}) ***");

                    try
                    {
                        // Öneriyi servis tarafında kabul et (istatistik tutmak için)
                        if (_smartSuggestionsService != null)
                        {
                            await _smartSuggestionsService.AcceptSuggestionAsync(suggestion, _contextBuffer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] AcceptSuggestionAsync hatası: {ex.Message}");
                        WriteToLogFile($"[ERROR] AcceptSuggestionAsync hatası: {ex.Message}");
                    }

                    // Önizleme açık kalsın - sadece önerileri temizle
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Önizleme açık kalsın - sadece önerileri temizle
                        SmartSuggestions.Clear();
                    });

                    // Öneriyi kullanıcı metnine uygula
                    switch (suggestion.Type)
                    {
                        case SuggestionType.WordCompletion:
                            // Mevcut kelimeyi seçip tam kelime ile değiştir
                            await ApplyWordCompletionAsync(suggestion.Text);
                            break;

                        default:
                            // Sonraki kelime veya kelime grubu → başına boşluk ekleyerek ekle
                            await ApplySuggestionTextAsync(suggestion.Text);
                            break;
                    }

                    // Öneri kabul edildikten sonra context buffer'ı temizle
                    _contextBuffer = "";
                    _currentSmartSuggestions.Clear();
                    _currentSuggestion = "";

                    Console.WriteLine("[DEBUG] Tab ile öneri kabul edildi ve temizlendi");
                    WriteToLogFile("[DEBUG] Tab ile öneri kabul edildi ve temizlendi");
                }
                else if (!string.IsNullOrEmpty(_currentSuggestion))
                {
                    // Güvenli tarafta kalmak için (edge-case) – öneri listesi boş ama string dolu
                    Console.WriteLine($"[DEBUG] *** Tab ile string bazlı öneri kabul ediliyor: {_currentSuggestion} ***");
                    WriteToLogFile($"[DEBUG] *** Tab ile string bazlı öneri kabul ediliyor: {_currentSuggestion} ***");

                    // Önizleme açık kalsın - işlem mesajı göster
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SafeSetPreviewText("🔄 Öneri uygulanıyor...");
                    });

                    await ApplySuggestionTextAsync(_currentSuggestion);
                    _currentSuggestion = "";
                }

                // Uygulama tamamlandıktan sonra arayüz ve durum temizliği
                // Önizleme açık kalsın - sadece önerileri temizle
                _currentSmartSuggestions.Clear();
                _currentSuggestion = string.Empty;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Önizlemeyi gizle
                    HidePreview();
                    SmartSuggestions.Clear();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ProcessTabForTextCompletion hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] ProcessTabForTextCompletion hatası: {ex.Message}");
            }
        }

        private async void OnSpacePressed(string currentBuffer)
        {
            Console.WriteLine($"[DEBUG] *** Boşluk tuşu basıldı, buffer: '{currentBuffer}' ***");

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
            {
                Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle boşluk işlemi atlandı");
                return;
            }

            // Akıllı öneriler etkinse sonraki kelime tahmini yap
            if (IsSmartSuggestionsEnabled && !string.IsNullOrWhiteSpace(currentBuffer))
            {
                Console.WriteLine($"[DEBUG] *** Sonraki kelime tahmini yapılıyor: '{currentBuffer}' ***");

                try
                {
                    // Son 2-3 kelimeyi al (context için) - daha spesifik tahmin için
                    var words = currentBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (words.Length >= 2)
                    {
                        // Son 2 kelimeyi kullan (bigram tabanlı tahmin)
                        var context = string.Join(" ", words.TakeLast(2));
                        Console.WriteLine($"[DEBUG] Bigram context: '{context}'");

                        // Sonraki kelime önerilerini al
                        Console.WriteLine($"[DEBUG] SmartSuggestionsService çağrılıyor: '{context}'");
                        var suggestions = await _smartSuggestionsService.GetSuggestionsAsync(context, 3);
                        Console.WriteLine($"[DEBUG] SmartSuggestionsService sonucu: {suggestions.Count} öneri");

                        if (suggestions.Any())
                        {
                            _currentSmartSuggestions = suggestions;
                            _currentSuggestion = suggestions.First().Text;

                            Console.WriteLine($"[DEBUG] *** Sonraki kelime önerisi: '{_currentSuggestion}' ***");

                            // Preview'da göster
                            var previewText = $"💡 {_currentSuggestion}";
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SafeSetPreviewText(previewText);

                                // UI'da akıllı öneriler listesini güncelle
                                SmartSuggestions.Clear();
                                foreach (var suggestion in suggestions.Take(5))
                                {
                                    SmartSuggestions.Add(suggestion);
                                }
                            });
                            Console.WriteLine($"[SMART SUGGESTIONS] Boşluk sonrası öneri gösteriliyor: {previewText}");
                            return;
                        }
                    }

                    // Eğer bigram bulunamazsa, son kelimeyi kullan
                    if (words.Length >= 1)
                    {
                        var lastWord = words.Last();
                        Console.WriteLine($"[DEBUG] Single word context: '{lastWord}'");

                        Console.WriteLine($"[DEBUG] SmartSuggestionsService tek kelime çağrılıyor: '{lastWord}'");
                        var suggestions = await _smartSuggestionsService.GetSuggestionsAsync(lastWord, 3);
                        Console.WriteLine($"[DEBUG] SmartSuggestionsService tek kelime sonucu: {suggestions.Count} öneri");

                        if (suggestions.Any())
                        {
                            _currentSmartSuggestions = suggestions;
                            _currentSuggestion = suggestions.First().Text;

                            Console.WriteLine($"[DEBUG] *** Tek kelime önerisi: '{_currentSuggestion}' ***");

                            var previewText = $"💡 {_currentSuggestion}";
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SafeSetPreviewText(previewText);

                                // UI'da akıllı öneriler listesini güncelle
                                SmartSuggestions.Clear();
                                foreach (var suggestion in suggestions.Take(5))
                                {
                                    SmartSuggestions.Add(suggestion);
                                }
                            });
                            Console.WriteLine($"[SMART SUGGESTIONS] Tek kelime önerisi gösteriliyor: {previewText}");
                            return;
                        }
                    }

                    // Akıllı servis sonuç vermedi - öğrendiği verileri kullan
                    Console.WriteLine("[DEBUG] Akıllı servis sonuç vermedi, öğrendiği verilerle tahmin yapılıyor");
                    var bufferWords = currentBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (bufferWords.Length > 0)
                    {
                        var lastWord = bufferWords.Last().ToLower();
                        Console.WriteLine($"[DEBUG] Öğrendiği verilerle tahmin: '{lastWord}'");

                        var learnedPredictions = GetSimpleNextWordPredictions(lastWord);
                        if (learnedPredictions.Any())
                        {
                            _currentSmartSuggestions = learnedPredictions;
                            _currentSuggestion = learnedPredictions.First().Text;

                            Console.WriteLine($"[DEBUG] Öğrendiği verilerden tahmin bulundu: '{_currentSuggestion}'");

                            var previewText = $"🔮 {_currentSuggestion}";
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SafeSetPreviewText(previewText);
                                SmartSuggestions.Clear();
                                foreach (var suggestion in learnedPredictions.Take(5))
                                {
                                    SmartSuggestions.Add(suggestion);
                                }
                            });
                            return;
                        }
                    }

                    // Hiç tahmin bulunamadı - test metni göster
                    Console.WriteLine("[DEBUG] Hiç tahmin bulunamadı - test metni gösteriliyor");
                    _currentSuggestion = "";
                    _currentSmartSuggestions.Clear();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Test metni göster
                        var testText = "";
                        SafeSetPreviewText(testText);
                        SmartSuggestions.Clear();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Sonraki kelime tahmini hatası: {ex.Message}");
                }
            }
        }

        private async Task SendTextToActiveWindow(string text)
        {
            try
            {
                Console.WriteLine($"[DEBUG] SendTextToActiveWindow (Advanced): '{text}'");

                // AdvancedInputService kullanarak metin gönder
                bool success = await _advancedInputService.SendTextAsync(text);

                if (success)
                {
                    Console.WriteLine($"[DEBUG] Metin başarıyla gönderildi: '{text}'");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Metin gönderilemedi: '{text}'");

                    // Fallback: eski yöntem
                    Console.WriteLine("[DEBUG] Fallback yöntemi deneniyor...");
                    await SendTextToActiveWindowFallback(text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendTextToActiveWindow hatası: {ex.Message}");

                // Fallback: eski yöntem
                try
                {
                    Console.WriteLine("[DEBUG] Exception fallback yöntemi deneniyor...");
                    await SendTextToActiveWindowFallback(text);
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"[ERROR] Fallback yöntemi de başarısız: {fallbackEx.Message}");
                }
            }
        }

        private async Task SendTextToActiveWindowFallback(string text)
        {
            try
            {
                Console.WriteLine($"[DEBUG] SendTextToActiveWindowFallback: '{text}'");

                // Eski clipboard yöntemi
                await Task.Run(() =>
                {
                    // Mevcut clipboard içeriğini kaydet
                    string? originalClipboard = null;
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            originalClipboard = System.Windows.Clipboard.GetText();
                        }
                    }
                    catch { }

                    // Metni clipboard'a kopyala
                    System.Windows.Clipboard.SetText(text);

                    // Kısa bir bekleme
                    Thread.Sleep(50);

                    // Ctrl+V ile yapıştır
                    System.Windows.Forms.SendKeys.SendWait("^v");

                    // Kısa bir bekleme
                    Thread.Sleep(50);

                    // Orijinal clipboard içeriğini geri yükle
                    if (originalClipboard != null)
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(originalClipboard);
                        }
                        catch { }
                    }
                });

                Console.WriteLine($"[DEBUG] Fallback metin gönderildi: '{text}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendTextToActiveWindowFallback hatası: {ex.Message}");
            }
        }

        private async Task SendTabCharacterToActiveWindow()
        {
            try
            {
                Console.WriteLine($"[DEBUG] SendTabCharacterToActiveWindow başlıyor");
                WriteToLogFile($"[DEBUG] SendTabCharacterToActiveWindow başlıyor");

                // AdvancedInputService kullanarak Tab tuşunu simüle et
                const ushort VK_TAB = 0x09;
                bool success = await _advancedInputService.SimulateKeyPressAsync(VK_TAB);

                if (success)
                {
                    Console.WriteLine($"[DEBUG] Tab karakteri başarıyla gönderildi");
                    WriteToLogFile($"[DEBUG] Tab karakteri başarıyla gönderildi");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Tab karakteri gönderilemedi");
                    WriteToLogFile($"[ERROR] Tab karakteri gönderilemedi");

                    // Fallback: SendKeys kullan
                    await Task.Run(() =>
                    {
                        System.Windows.Forms.SendKeys.SendWait("{TAB}");
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendTabCharacterToActiveWindow hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] SendTabCharacterToActiveWindow hatası: {ex.Message}");
            }
        }

        private async Task AcceptSmartSuggestion()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentSuggestion))
                {
                    Console.WriteLine($"[SMART SUGGESTIONS] Tab ile öneri kabul ediliyor: {_currentSuggestion}");

                    // Öneriyi klavye ile yaz (şimdilik console'a yazdır)
                    Console.WriteLine($"[SMART SUGGESTIONS] Öneri kabul edildi: {_currentSuggestion}");

                    // Öneriyi temizle
                    _currentSuggestion = "";

                    // Preview'ı gizleme - hemen yeni tahmin yap
                    // SafeSetPreviewText mesajı kaldırıldı - sistem hemen yeni tahmin yapacak
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AcceptSmartSuggestion hatası: {ex.Message}");
            }
        }

        private void FilterShortcuts()
        {
            FilteredShortcuts.Clear();
            
            var filtered = string.IsNullOrEmpty(ShortcutFilter)
                ? Shortcuts
                : Shortcuts.Where(s => 
                    s.Key.Contains(ShortcutFilter, StringComparison.OrdinalIgnoreCase) ||
                    s.Expansion.Contains(ShortcutFilter, StringComparison.OrdinalIgnoreCase));

            foreach (var shortcut in filtered)
            {
                FilteredShortcuts.Add(shortcut);
            }
        }



        private async void AddShortcut()
        {
            try
            {
                Console.WriteLine("[DEBUG] AddShortcut called");

                var dialog = new Views.ShortcutDialog();
                Console.WriteLine("[DEBUG] ShortcutDialog created");

                if (dialog.ShowDialog() == true)
                {
                    Console.WriteLine($"[DEBUG] Dialog result: OK, Key='{dialog.ShortcutKey}', Text='{dialog.ExpansionText}'");

                    if (_shortcutService.ShortcutExists(dialog.ShortcutKey))
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"'{dialog.ShortcutKey}' kısayolu zaten mevcut. Üzerine yazmak istiyor musunuz?",
                            "Kısayol Mevcut",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result != System.Windows.MessageBoxResult.Yes)
                            return;
                    }

                    _shortcutService.AddShortcut(dialog.ShortcutKey, dialog.ExpansionText);
                    await _shortcutService.SaveShortcutsAsync();

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(Shortcuts));
                    FilterShortcuts();

                    UpdateStats();
                    UpdateAnalytics();
                    UpdateShortcutPreviewPanel();

                    Console.WriteLine("[DEBUG] AddShortcut completed successfully");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Dialog result: Cancel");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AddShortcut failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");

                System.Windows.MessageBox.Show(
                    $"Kısayol eklenirken hata oluştu:\n\n{ex.Message}\n\nLütfen tekrar deneyin.",
                    "Hata",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void EditShortcut()
        {
            try
            {
                if (SelectedShortcut == null)
                {
                    Console.WriteLine("[DEBUG] EditShortcut: No shortcut selected");
                    return;
                }

                Console.WriteLine($"[DEBUG] EditShortcut called for: {SelectedShortcut.Key}");

                var dialog = new Views.ShortcutDialog(SelectedShortcut);
                Console.WriteLine("[DEBUG] ShortcutDialog created for editing");

                if (dialog.ShowDialog() == true)
                {
                    Console.WriteLine($"[DEBUG] Edit dialog result: OK, Key='{dialog.ShortcutKey}', Text='{dialog.ExpansionText}'");

                    // Eğer kısayol değiştiyse ve yeni kısayol mevcutsa kontrol et
                    if (dialog.ShortcutKey != SelectedShortcut.Key &&
                        _shortcutService.ShortcutExists(dialog.ShortcutKey))
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"'{dialog.ShortcutKey}' kısayolu zaten mevcut. Üzerine yazmak istiyor musunuz?",
                            "Kısayol Mevcut",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (result != System.Windows.MessageBoxResult.Yes)
                            return;
                    }

                    // Eski kısayolu sil
                    _shortcutService.RemoveShortcut(SelectedShortcut.Key);

                    // Yeni kısayolu ekle
                    _shortcutService.AddShortcut(dialog.ShortcutKey, dialog.ExpansionText);

                    await _shortcutService.SaveShortcutsAsync();

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(Shortcuts));
                    FilterShortcuts();

                    UpdateStats();
                    UpdateAnalytics();
                    UpdateShortcutPreviewPanel();

                    Console.WriteLine("[DEBUG] EditShortcut completed successfully");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Edit dialog result: Cancel");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] EditShortcut failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");

                System.Windows.MessageBox.Show(
                    $"Kısayol düzenlenirken hata oluştu:\n\n{ex.Message}\n\nLütfen tekrar deneyin.",
                    "Hata",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void DeleteShortcut()
        {
            if (SelectedShortcut != null)
            {
                _shortcutService.RemoveShortcut(SelectedShortcut.Key);
                await _shortcutService.SaveShortcutsAsync();

                // UI'yi güncelle
                OnPropertyChanged(nameof(Shortcuts));
                FilterShortcuts();

                UpdateStats();
                UpdateAnalytics();
                UpdateShortcutPreviewPanel();
            }
        }



        private void OpenSettings()
        {
            // Bu method View'da settings dialog açacak
        }

        public async Task SaveAllAsync()
        {
            await _shortcutService.SaveShortcutsAsync();
            await _settingsService.SaveSettingsAsync();
        }

        public async Task LoadShortcutsAsync()
        {
            await _shortcutService.LoadShortcutsAsync();
            FilterShortcuts();

            // Kısayol önizleme panelini güncelle
            UpdateShortcutPreviewPanel();

            Console.WriteLine($"[DEBUG] Kısayollar yüklendi: {Shortcuts.Count} adet");
        }



        public void HidePreview()
        {
            try
            {
                // Önizleme gizleme işlemi
                Console.WriteLine("[DEBUG] HidePreview çağrıldı");
                WriteToLogFile("[DEBUG] HidePreview çağrıldı");

                // Thread safety check
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => HidePreview());
                    return;
                }

                // Null check
                if (_previewOverlay == null)
                {
                    Console.WriteLine("[PREVIEW] PreviewOverlay null, gizleme işlemi atlanıyor");
                    return;
                }

                _previewOverlay.HidePreview();
                Console.WriteLine("[PREVIEW] Önizleme gizlendi");
                WriteToLogFile("[PREVIEW] Önizleme gizlendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] HidePreview hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] HidePreview hatası: {ex.Message}");
            }
        }

        // BASİT VE MANTIKLI AKILLI ÖNERİ SİSTEMİ
        private readonly List<string> _learnedWords = new List<string>();

        // Yazı yazma durumu takibi için
        private DateTime _lastKeyPressTime = DateTime.MinValue;
        // Preview otomatik kapanma timer'ı (3 saniye)
        private System.Timers.Timer? _previewAutoHideTimer;

        private async Task UpdateWordCompletionAsync(string partialWord, string fullContext)
        {
            Console.WriteLine($"[DEBUG] *** UpdateWordCompletionAsync çağrıldı ***");
            WriteToLogFile($"[DEBUG] *** UpdateWordCompletionAsync çağrıldı ***");
            Console.WriteLine($"[DEBUG] Partial word: '{partialWord}'");
            WriteToLogFile($"[DEBUG] Partial word: '{partialWord}'");
            Console.WriteLine($"[DEBUG] Full context: '{fullContext}'");
            WriteToLogFile($"[DEBUG] Full context: '{fullContext}'");

            try
            {
                Console.WriteLine($"[DEBUG] Try bloğuna girdi");
                WriteToLogFile($"[DEBUG] Try bloğuna girdi");

                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions çağrılacak...");
                WriteToLogFile($"[DEBUG] GetSimpleWordCompletions çağrılacak...");

                // BASİT YAKLAŞIM: Öğrenilen kelimelerden eşleşenleri bul
                var suggestions = GetSimpleWordCompletions(partialWord);

                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions tamamlandı");
                WriteToLogFile($"[DEBUG] GetSimpleWordCompletions tamamlandı");

                Console.WriteLine($"[DEBUG] Basit kelime tamamlama önerileri: {suggestions.Count} adet");
                WriteToLogFile($"[DEBUG] Basit kelime tamamlama önerileri: {suggestions.Count} adet");

                // Mevcut önerileri güncelle
                _currentSmartSuggestions.Clear();
                _currentSmartSuggestions.AddRange(suggestions);

                // UI'ı güncelle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SmartSuggestions.Clear();
                    foreach (var suggestion in suggestions)
                    {
                        SmartSuggestions.Add(suggestion);
                        Console.WriteLine($"[DEBUG] UI'a eklenen öneri: '{suggestion.Text}'");
                        WriteToLogFile($"[DEBUG] UI'a eklenen öneri: '{suggestion.Text}'");
                    }
                });

                // İlk öneriyi mevcut öneri olarak ayarla
                if (suggestions.Count > 0)
                {
                    _currentSuggestion = suggestions[0].Text;
                    Console.WriteLine($"[DEBUG] Mevcut öneri ayarlandı: '{_currentSuggestion}'");
                    WriteToLogFile($"[DEBUG] Mevcut öneri ayarlandı: '{_currentSuggestion}'");
                }
                else
                {
                    _currentSuggestion = "";
                    Console.WriteLine($"[DEBUG] Öneri bulunamadı, mevcut öneri temizlendi");
                    WriteToLogFile($"[DEBUG] Öneri bulunamadı, mevcut öneri temizlendi");
                }

                Console.WriteLine($"[DEBUG] UpdateWordCompletionAsync tamamlandı, {suggestions.Count} öneri eklendi");
                WriteToLogFile($"[DEBUG] UpdateWordCompletionAsync tamamlandı, {suggestions.Count} öneri eklendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateWordCompletionAsync hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] UpdateWordCompletionAsync hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                WriteToLogFile($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        // BASİT KELİME TAMAMLAMA FONKSİYONU
        private List<SmartSuggestion> GetSimpleWordCompletions(string partialWord)
        {
            var suggestions = new List<SmartSuggestion>();

            try
            {
                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions çağrıldı: '{partialWord}'");
                WriteToLogFile($"[DEBUG] GetSimpleWordCompletions çağrıldı: '{partialWord}'");

                // Öğrenilen kelimeler listesinde eşleşenleri bul
                var matches = _learnedWords
                    .Where(word => word.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase) && word.Length > partialWord.Length)
                    .Distinct()
                    .Take(5)
                    .ToList();

                Console.WriteLine($"[DEBUG] Eşleşen kelimeler: {matches.Count} adet");
                WriteToLogFile($"[DEBUG] Eşleşen kelimeler: {matches.Count} adet");

                foreach (var match in matches)
                {
                    suggestions.Add(new SmartSuggestion
                    {
                        Text = match,
                        Type = SuggestionType.WordCompletion,
                        Confidence = 0.8,
                        Context = partialWord,
                        Frequency = 1,
                        LastUsed = DateTime.Now
                    });

                    Console.WriteLine($"[DEBUG] Öneri eklendi: '{match}'");
                    WriteToLogFile($"[DEBUG] Öneri eklendi: '{match}'");
                }

                // Eğer öğrenilen kelimelerden bulamazsa, varsayılan öneriler ekle
                if (suggestions.Count == 0)
                {
                    var defaultSuggestions = GetDefaultWordCompletions(partialWord);
                    suggestions.AddRange(defaultSuggestions);

                    Console.WriteLine($"[DEBUG] Varsayılan öneriler eklendi: {defaultSuggestions.Count} adet");
                    WriteToLogFile($"[DEBUG] Varsayılan öneriler eklendi: {defaultSuggestions.Count} adet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetSimpleWordCompletions hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] GetSimpleWordCompletions hatası: {ex.Message}");
            }

            return suggestions;
        }

        // BASİT SONRAKI KELİME TAHMİNLERİ
        private List<SmartSuggestion> GetSimpleNextWordPredictions(string lastWord)
        {
            var suggestions = new List<SmartSuggestion>();

            try
            {
                Console.WriteLine($"[SIMPLE PREDICTION] GetSimpleNextWordPredictions çağrıldı: '{lastWord}'");
                WriteToLogFile($"[SIMPLE PREDICTION] GetSimpleNextWordPredictions çağrıldı: '{lastWord}'");

                // Debug: Öğrenilen kelime çiftlerini listele
                Console.WriteLine($"[DEBUG] Toplam öğrenilen kelime çifti sayısı: {_learnedWordPairs.Count}");
                WriteToLogFile($"[DEBUG] Toplam öğrenilen kelime çifti sayısı: {_learnedWordPairs.Count}");
                foreach (var pair in _learnedWordPairs.Take(10))
                {
                    Console.WriteLine($"[DEBUG] Kelime çifti: '{pair.Key}' → [{string.Join(", ", pair.Value)}]");
                    WriteToLogFile($"[DEBUG] Kelime çifti: '{pair.Key}' → [{string.Join(", ", pair.Value)}]");
                }

                // DEBUG: Basit test verileri ekle (geçici)
                var testPairs = new Dictionary<string, List<string>>
                {
                    {"merhaba", new List<string> {"nasılsın", "arkadaş", "dostum"}},
                    {"senin", new List<string> {"sorunun", "adın", "işin"}},
                    {"sorunun", new List<string> {"nedir", "ne", "var"}},
                    {"nasılsın", new List<string> {"bugün", "neler", "iyi"}},
                    {"ben", new List<string> {"iyiyim", "çok", "de"}},
                    {"bugün", new List<string> {"nasıl", "ne", "çok"}},
                };

                Console.WriteLine($"[DEBUG] Test verileri eklendi: {testPairs.Count} kelime çifti");
                WriteToLogFile($"[DEBUG] Test verileri eklendi: {testPairs.Count} kelime çifti");

                // Önce öğrenilen kelime çiftlerinden tahmin yap (daha güvenilir)
                // Case-insensitive arama yap
                var learnedKey = _learnedWordPairs.Keys.FirstOrDefault(k => k.Equals(lastWord, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(learnedKey))
                {
                    var learnedPredictions = _learnedWordPairs[learnedKey];
                    Console.WriteLine($"[LEARNED PREDICTION] '{lastWord}' (key: '{learnedKey}') için {learnedPredictions.Count} öğrenilmiş tahmin bulundu");
                    WriteToLogFile($"[LEARNED PREDICTION] '{lastWord}' (key: '{learnedKey}') için {learnedPredictions.Count} öğrenilmiş tahmin bulundu");

                    foreach (var prediction in learnedPredictions.Take(3))
                    {
                        suggestions.Add(new SmartSuggestion
                        {
                            Text = prediction,
                            Type = SuggestionType.NextWord,
                            Confidence = 0.9, // Öğrenilmiş tahminler %90 güven
                            Context = lastWord,
                            Frequency = 1,
                            LastUsed = DateTime.Now
                        });

                        Console.WriteLine($"[LEARNED PREDICTION] Öğrenilmiş tahmin eklendi: '{prediction}'");
                        WriteToLogFile($"[LEARNED PREDICTION] Öğrenilmiş tahmin eklendi: '{prediction}'");
                    }
                }

                // Test verilerinden de tahmin yap (geçici debug)
                if (testPairs.ContainsKey(lastWord))
                {
                    var testPredictions = testPairs[lastWord];
                    Console.WriteLine($"[TEST PREDICTION] '{lastWord}' için {testPredictions.Count} test tahmin bulundu");
                    WriteToLogFile($"[TEST PREDICTION] '{lastWord}' için {testPredictions.Count} test tahmin bulundu");

                    foreach (var prediction in testPredictions.Take(2))
                    {
                        // Zaten eklenmişse tekrar ekleme
                        if (!suggestions.Any(s => s.Text.Equals(prediction, StringComparison.OrdinalIgnoreCase)))
                        {
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = prediction,
                                Type = SuggestionType.NextWord,
                                Confidence = 0.8, // Test tahminler %80 güven
                                Context = lastWord,
                                Frequency = 1,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[TEST PREDICTION] Test tahmin eklendi: '{prediction}'");
                            WriteToLogFile($"[TEST PREDICTION] Test tahmin eklendi: '{prediction}'");
                        }
                    }
                }

                // Test verilerinden de tahmin yap (geçici debug)
                if (testPairs.ContainsKey(lastWord))
                {
                    var testPredictions = testPairs[lastWord];
                    Console.WriteLine($"[TEST PREDICTION] '{lastWord}' için {testPredictions.Count} test tahmin bulundu");
                    WriteToLogFile($"[TEST PREDICTION] '{lastWord}' için {testPredictions.Count} test tahmin bulundu");

                    foreach (var prediction in testPredictions.Take(2))
                    {
                        // Zaten eklenmişse tekrar ekleme
                        if (!suggestions.Any(s => s.Text.Equals(prediction, StringComparison.OrdinalIgnoreCase)))
                        {
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = prediction,
                                Type = SuggestionType.NextWord,
                                Confidence = 0.8, // Test tahminler %80 güven
                                Context = lastWord,
                                Frequency = 1,
                                LastUsed = DateTime.Now
                            });

                            Console.WriteLine($"[TEST PREDICTION] Test tahmin eklendi: '{prediction}'");
                            WriteToLogFile($"[TEST PREDICTION] Test tahmin eklendi: '{prediction}'");
                        }
                    }
                }

                if (suggestions.Count == 0)
                {
                    Console.WriteLine($"[SIMPLE PREDICTION] '{lastWord}' için hiç tahmin bulunamadı");
                    WriteToLogFile($"[SIMPLE PREDICTION] '{lastWord}' için hiç tahmin bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetSimpleNextWordPredictions hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] GetSimpleNextWordPredictions hatası: {ex.Message}");
            }

            return suggestions;
        }

        // VARSAYILAN KELİME ÖNERİLERİ
        private List<SmartSuggestion> GetDefaultWordCompletions(string partialWord)
        {
            var suggestions = new List<SmartSuggestion>();

            // Basit Türkçe kelime önerileri
            var defaultWords = new Dictionary<string, List<string>>
            {
                { "mer", new List<string> { "merhaba", "merkez", "merdiven" } },
                { "nas", new List<string> { "nasıl", "nasılsın" } },
                { "ne", new List<string> { "neler", "nerede", "ne" } },
                { "ya", new List<string> { "yapıyor", "yapıyorsun", "yardım" } },
                { "bu", new List<string> { "bugün", "burada", "bunlar" } },
                { "ça", new List<string> { "çalışıyor", "çalışma", "çay" } },
                { "gel", new List<string> { "geliyor", "geldi", "gelmek" } },
                { "git", new List<string> { "gidiyor", "gitti", "gitmek" } }
            };

            var lowerPartial = partialWord.ToLower();
            foreach (var kvp in defaultWords)
            {
                if (kvp.Key.StartsWith(lowerPartial))
                {
                    foreach (var word in kvp.Value)
                    {
                        if (word.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add(new SmartSuggestion
                            {
                                Text = word,
                                Type = SuggestionType.WordCompletion,
                                Confidence = 0.6,
                                Context = partialWord,
                                Frequency = 1,
                                LastUsed = DateTime.Now
                            });
                        }
                    }
                }
            }

            return suggestions.Take(3).ToList();
        }

        private async Task UpdateSmartSuggestionsAsync(string context)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** UpdateSmartSuggestionsAsync çağrıldı ***");
                Console.WriteLine($"[DEBUG] Context: '{context}'");
                Console.WriteLine($"[DEBUG] SmartSuggestionsService null mu: {_smartSuggestionsService == null}");
                Console.WriteLine($"[DEBUG] MaxSmartSuggestions: {_settingsService.Settings.MaxSmartSuggestions}");

                if (_smartSuggestionsService == null)
                {
                    Console.WriteLine("[ERROR] SmartSuggestionsService null!");
                    return;
                }

                var suggestions = await _smartSuggestionsService.GetSuggestionsAsync(context, _settingsService.Settings.MaxSmartSuggestions);
                _currentSmartSuggestions = suggestions;

                Console.WriteLine($"[SMART SUGGESTIONS] *** {suggestions.Count} öneri bulundu, context: '{context}' ***");

                // Her öneriyi logla
                for (int i = 0; i < suggestions.Count; i++)
                {
                    var sug = suggestions[i];
                    Console.WriteLine($"[SMART SUGGESTIONS] Öneri {i + 1}: '{sug.Text}' (Confidence: {sug.Confidence:P0}, Context: '{sug.Context}', Frequency: {sug.Frequency})");
                }

                // Mevcut öneriyi ayarla ve UI'ı güncelle
                if (suggestions.Count > 0)
                {
                    var firstSuggestion = suggestions[0];
                    _currentSuggestion = firstSuggestion.Text; // Mevcut öneriyi kaydet

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // UI'da akıllı öneriler listesini güncelle
                        SmartSuggestions.Clear();
                        foreach (var suggestion in suggestions.Take(5))
                        {
                            SmartSuggestions.Add(suggestion);
                        }
                    });

                    Console.WriteLine($"[SMART SUGGESTIONS] Öneri ayarlandı: {firstSuggestion.Text}");
                }
                else
                {
                    Console.WriteLine($"[SMART SUGGESTIONS] Öneri bulunamadı, context: '{context}'");
                    _currentSuggestion = ""; // Mevcut öneriyi temizle

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SmartSuggestions.Clear();
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMART SUGGESTIONS] Akıllı öneri güncelleme hatası: {ex.Message}");
            }
        }

        private void OnSmartSuggestionsUpdated(List<SmartSuggestion> suggestions)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SmartSuggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    SmartSuggestions.Add(suggestion);
                }


            });
        }

        private void OnSmartSuggestionAccepted(SmartSuggestion suggestion)
        {
            // Kabul edilen öneri için istatistik güncelle
            Console.WriteLine($"Akıllı öneri kabul edildi: {suggestion.Text}");
        }

        private void OnSettingsChanged(AppSettings settings)
        {
            Console.WriteLine($"[DEBUG] Settings changed. SmartSuggestionsEnabled: {settings.SmartSuggestionsEnabled}");

            // Smart Suggestions ayarları değiştiğinde UI'ı güncelle
            OnPropertyChanged(nameof(IsSmartSuggestionsEnabled));
            OnPropertyChanged(nameof(SmartSuggestionsStatusText));
            OnPropertyChanged(nameof(SmartSuggestionsStatusColor));



            // Pencere filtreleme ayarları değiştiğinde UI'ı güncelle
            OnPropertyChanged(nameof(IsWindowFilteringEnabled));
            OnPropertyChanged(nameof(WindowFilteringStatusText));
            OnPropertyChanged(nameof(WindowFilteringStatusColor));
            OnPropertyChanged(nameof(WindowFilters));
            OnPropertyChanged(nameof(WindowFilterMode));

            // SmartSuggestionsService'e ayar değişikliğini bildir
            if (_smartSuggestionsService != null)
            {
                // Service'in ayarları güncellemesini sağla
                Console.WriteLine($"[DEBUG] SmartSuggestionsService ayarları güncelleniyor");
            }
        }



        private async Task ApplySmartSuggestionAsync(SmartSuggestion suggestion)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ApplySmartSuggestionAsync başlıyor: {suggestion.Text}");

                // Öneriyi kabul et
                await _smartSuggestionsService.AcceptSuggestionAsync(suggestion, _contextBuffer);

                // Kelime tamamlama için mevcut kelimeyi sil ve tam kelimeyi yaz
                if (suggestion.Type == SuggestionType.WordCompletion)
                {
                    await ApplyWordCompletionAsync(suggestion.Text);
                }
                else
                {
                    // Sonraki kelime önerisi için sadece ekle
                    await ApplySuggestionTextAsync(suggestion.Text);
                }

                // Preview'ı gizleme - hemen yeni tahmin yap
                // SafeSetPreviewText mesajı kaldırıldı - sistem hemen yeni tahmin yapacak

                Console.WriteLine($"[DEBUG] Akıllı öneri uygulandı: {suggestion.Text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Akıllı öneri uygulama hatası: {ex.Message}");
            }
        }

        private async Task ApplyWordCompletionAsync(string fullWord)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** ApplyWordCompletionAsync başlıyor: {fullWord} ***");
                WriteToLogFile($"[DEBUG] *** ApplyWordCompletionAsync başlıyor: {fullWord} ***");

                // Kullanıcının yazdığı kısmı hesapla
                string userTypedPart = GetCurrentTypedWord();
                Console.WriteLine($"[DEBUG] Kullanıcının yazdığı kısım: '{userTypedPart}'");
                WriteToLogFile($"[DEBUG] Kullanıcının yazdığı kısım: '{userTypedPart}'");

                // Sadece eksik kısmı hesapla
                string remainingPart = "";
                if (!string.IsNullOrEmpty(userTypedPart) && fullWord.StartsWith(userTypedPart, StringComparison.OrdinalIgnoreCase))
                {
                    remainingPart = fullWord.Substring(userTypedPart.Length);
                    Console.WriteLine($"[DEBUG] Eksik kısım: '{remainingPart}'");
                    WriteToLogFile($"[DEBUG] Eksik kısım: '{remainingPart}'");
                }
                else
                {
                    // Eğer eşleşme yoksa tam kelimeyi kullan
                    remainingPart = fullWord;
                    Console.WriteLine($"[DEBUG] Eşleşme yok, tam kelime kullanılıyor: '{remainingPart}'");
                    WriteToLogFile($"[DEBUG] Eşleşme yok, tam kelime kullanılıyor: '{remainingPart}'");
                }

                // Eğer eksik kısım yoksa hiçbir şey yapma
                if (string.IsNullOrEmpty(remainingPart))
                {
                    Console.WriteLine($"[DEBUG] Eksik kısım yok, işlem atlandı");
                    WriteToLogFile($"[DEBUG] Eksik kısım yok, işlem atlandı");
                    return;
                }

                // UI thread'de çalıştır (STA gerekli)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Sadece eksik kısım yazılıyor: '{remainingPart}'");
                        WriteToLogFile($"[DEBUG] Sadece eksik kısım yazılıyor: '{remainingPart}'");

                        // Clipboard'ı geçici olarak kaydet
                        string originalClipboard = string.Empty;
                        try
                        {
                            originalClipboard = System.Windows.Clipboard.GetText();
                            Console.WriteLine($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                            WriteToLogFile($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                            WriteToLogFile($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                        }

                        // Sadece eksik kısmı clipboard'a koy
                        Console.WriteLine($"[DEBUG] Clipboard'a eksik kısım yazılıyor: '{remainingPart}'");
                        WriteToLogFile($"[DEBUG] Clipboard'a eksik kısım yazılıyor: '{remainingPart}'");
                        System.Windows.Clipboard.SetText(remainingPart);
                        Thread.Sleep(10);

                        // Ctrl+V ile yapıştır (eksik kısmı ekler)
                        Console.WriteLine($"[DEBUG] Ctrl+V gönderiliyor...");
                        WriteToLogFile($"[DEBUG] Ctrl+V gönderiliyor...");
                        SendCtrlV();

                        // Kelime tamamlandıktan sonra boşluk ekle
                        Thread.Sleep(50); // Kısa bekleme
                        Console.WriteLine($"[DEBUG] Kelime tamamlandı, boşluk ekleniyor...");
                        WriteToLogFile($"[DEBUG] Kelime tamamlandı, boşluk ekleniyor...");
                        SendSpace();

                        Console.WriteLine($"[DEBUG] *** ApplyWordCompletionAsync tamamlandı: {fullWord} + boşluk ***");
                        WriteToLogFile($"[DEBUG] *** ApplyWordCompletionAsync tamamlandı: {fullWord} + boşluk ***");

                        // Kelime tamamlandıktan sonra hemen yeni context ile sonraki kelimeyi tahmin et
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Kısa bekleme - metin işlensin
                                await Task.Delay(150);

                                // Yeni context oluştur (tamamlanan kelime + boşluk)
                                var newContext = _contextBuffer + fullWord + " ";
                                Console.WriteLine($"[DEBUG] *** TAB SONRASI YENİ TAHMİN *** Context: '{newContext}'");
                                WriteToLogFile($"[DEBUG] *** TAB SONRASI YENİ TAHMİN *** Context: '{newContext}'");

                                // Context buffer'ı güncelle
                                _contextBuffer = newContext;

                                // Hemen yeni tahmin yap
                                await ProcessSmartSuggestionsAsync(newContext);

                                // Önizlemeyi hemen güncelle
                                if (!string.IsNullOrEmpty(_currentSuggestion))
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        var words = newContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                        var previewText = $"🔮 {_currentSuggestion}";
                                        SafeSetPreviewText(previewText);
                                        Console.WriteLine($"[DEBUG] Tab sonrası önizleme güncellendi: {previewText}");
                                        WriteToLogFile($"[DEBUG] Tab sonrası önizleme güncellendi: {previewText}");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Kelime sonrası tahmin hatası: {ex.Message}");
                                WriteToLogFile($"[ERROR] Kelime sonrası tahmin hatası: {ex.Message}");
                            }
                        });

                        // Orijinal clipboard'ı geri yükle
                        Task.Run(async () =>
                        {
                            await Task.Delay(200);
                            try
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (!string.IsNullOrEmpty(originalClipboard))
                                    {
                                        System.Windows.Clipboard.SetText(originalClipboard);
                                        Console.WriteLine($"[DEBUG] Orijinal clipboard geri yüklendi");
                                        WriteToLogFile($"[DEBUG] Orijinal clipboard geri yüklendi");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                                WriteToLogFile($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Kelime tamamlama iç hatası: {ex.Message}");
                        WriteToLogFile($"[ERROR] Kelime tamamlama iç hatası: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kelime tamamlama hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] Kelime tamamlama hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                WriteToLogFile($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private async Task ApplySuggestionTextAsync(string suggestionText)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsync başlıyor: {suggestionText} ***");
                WriteToLogFile($"[DEBUG] *** ApplySuggestionTextAsync başlıyor: {suggestionText} ***");

                // UI thread'de çalıştır (STA gerekli)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Metin ekleme: {suggestionText}");
                        WriteToLogFile($"[DEBUG] Metin ekleme: {suggestionText}");

                        // Clipboard'ı geçici olarak kaydet
                        string originalClipboard = string.Empty;
                        try
                        {
                            originalClipboard = System.Windows.Clipboard.GetText();
                            Console.WriteLine($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                            WriteToLogFile($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                            WriteToLogFile($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                        }

                        // Öneri metnini clipboard'a koy (boşluk ekleme!)
                        Console.WriteLine($"[DEBUG] Clipboard'a metin yazılıyor: '{suggestionText}'");
                        WriteToLogFile($"[DEBUG] Clipboard'a metin yazılıyor: '{suggestionText}'");
                        System.Windows.Clipboard.SetText(suggestionText);

                        // Kısa bekleme
                        Thread.Sleep(10);

                        // Ctrl+V ile yapıştır
                        Console.WriteLine($"[DEBUG] Ctrl+V gönderiliyor...");
                        WriteToLogFile($"[DEBUG] Ctrl+V gönderiliyor...");
                        SendCtrlV();

                        // Öneri eklendikten sonra boşluk ekle
                        Thread.Sleep(50); // Kısa bekleme
                        Console.WriteLine($"[DEBUG] Öneri eklendi, boşluk ekleniyor...");
                        WriteToLogFile($"[DEBUG] Öneri eklendi, boşluk ekleniyor...");
                        SendSpace();

                        Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsync tamamlandı: {suggestionText} + boşluk ***");
                        WriteToLogFile($"[DEBUG] *** ApplySuggestionTextAsync tamamlandı: {suggestionText} + boşluk ***");

                        // ÖNEMLİ: Öneri eklendikten sonra HEMEN yeni context ile sonraki kelimeyi tahmin et
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Kısa bekleme - metin işlensin
                                await Task.Delay(100);

                                // Yeni context oluştur (eklenen öneri + boşluk)
                                var newContext = _contextBuffer + suggestionText + " ";
                                Console.WriteLine($"[DEBUG] *** TAB SONRASI YENİ TAHMİN BAŞLIYOR *** Context: '{newContext}'");
                                WriteToLogFile($"[DEBUG] *** TAB SONRASI YENİ TAHMİN BAŞLIYOR *** Context: '{newContext}'");

                                // Context buffer'ı güncelle
                                _contextBuffer = newContext;

                                // Kelimeleri ayır ve analiz et
                                var words = newContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                Console.WriteLine($"[DEBUG] TAB SONRASI - Kelime sayısı: {words.Length}, Son kelime: '{(words.Length > 0 ? words.Last() : "YOK")}'");
                                WriteToLogFile($"[DEBUG] TAB SONRASI - Kelime sayısı: {words.Length}, Son kelime: '{(words.Length > 0 ? words.Last() : "YOK")}'");

                                // HEMEN yeni tahmin yap - hem akıllı servis hem de öğrendiği verilerle
                                await ProcessSmartSuggestionsAsync(newContext);

                                // Eğer akıllı servis sonuç vermezse, direkt öğrendiği verilerle tahmin yap
                                if (_currentSmartSuggestions.Count == 0 && words.Length > 0)
                                {
                                    Console.WriteLine($"[DEBUG] TAB SONRASI - Akıllı servis sonuç vermedi, öğrendiği verilerle tahmin yapılıyor");
                                    WriteToLogFile($"[DEBUG] TAB SONRASI - Akıllı servis sonuç vermedi, öğrendiği verilerle tahmin yapılıyor");

                                    var lastWord = words.Last().ToLower();
                                    var simplePredictions = GetSimpleNextWordPredictions(lastWord);

                                    if (simplePredictions.Any())
                                    {
                                        _currentSuggestion = simplePredictions.First().Text;
                                        _currentSmartSuggestions.Clear();
                                        _currentSmartSuggestions.AddRange(simplePredictions);

                                        Console.WriteLine($"[DEBUG] TAB SONRASI - Basit tahmin bulundu: '{_currentSuggestion}'");
                                        WriteToLogFile($"[DEBUG] TAB SONRASI - Basit tahmin bulundu: '{_currentSuggestion}'");

                                        // UI'ı güncelle
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            SmartSuggestions.Clear();
                                            foreach (var suggestion in simplePredictions.Take(3))
                                            {
                                                SmartSuggestions.Add(suggestion);
                                            }
                                        });

                                        // Önizlemeyi güncelle
                                        ShowPreview(newContext);
                                    }
                                }

                                // Önizlemeyi hemen güncelle
                                if (!string.IsNullOrEmpty(_currentSuggestion))
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        var words = newContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                        var previewText = $"🔮 {_currentSuggestion}";
                                        SafeSetPreviewText(previewText);
                                        Console.WriteLine($"[DEBUG] Tab sonrası önizleme güncellendi: {previewText}");
                                        WriteToLogFile($"[DEBUG] Tab sonrası önizleme güncellendi: {previewText}");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Öneri sonrası tahmin hatası: {ex.Message}");
                                WriteToLogFile($"[ERROR] Öneri sonrası tahmin hatası: {ex.Message}");
                            }
                        });

                        // Orijinal clipboard'ı geri yükle
                        Task.Run(async () =>
                        {
                            await Task.Delay(200);
                            try
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (!string.IsNullOrEmpty(originalClipboard))
                                    {
                                        System.Windows.Clipboard.SetText(originalClipboard);
                                        Console.WriteLine($"[DEBUG] Orijinal clipboard geri yüklendi");
                                        WriteToLogFile($"[DEBUG] Orijinal clipboard geri yüklendi");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                                WriteToLogFile($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Metin ekleme iç hatası: {ex.Message}");
                        WriteToLogFile($"[ERROR] Metin ekleme iç hatası: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Metin uygulama hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] Metin uygulama hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                WriteToLogFile($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private void SendCtrlV()
        {
            // Win32 API kullanarak Ctrl+V gönder
            const byte VK_CONTROL = 0x11;
            const byte VK_V = 0x56;
            const uint KEYEVENTF_KEYUP = 0x0002;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

            // Ctrl tuşunu bas
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            // V tuşunu bas
            keybd_event(VK_V, 0, 0, System.UIntPtr.Zero);
            // V tuşunu bırak
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Ctrl tuşunu bırak
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        private void SendSpace()
        {
            // Win32 API kullanarak Space tuşu gönder
            const byte VK_SPACE = 0x20;
            const uint KEYEVENTF_KEYUP = 0x0002;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

            // Space tuşunu bas
            keybd_event(VK_SPACE, 0, 0, System.UIntPtr.Zero);
            // Space tuşunu bırak
            keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        private string GetCurrentTypedWord()
        {
            try
            {
                // Context buffer'dan son kelimeyi al
                if (!string.IsNullOrEmpty(_contextBuffer))
                {
                    var words = _contextBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                    {
                        var lastWord = words[words.Length - 1];
                        Console.WriteLine($"[DEBUG] Context buffer'dan son kelime: '{lastWord}'");
                        WriteToLogFile($"[DEBUG] Context buffer'dan son kelime: '{lastWord}'");
                        return lastWord;
                    }
                }

                // Eğer context buffer boşsa, keyboard hook'tan gelen son kelimeyi kullan
                // Bu kısım için KeyboardHookService'den son yazılan kelimeyi almamız gerekiyor
                // Şimdilik basit bir yaklaşım kullanalım
                Console.WriteLine($"[DEBUG] Context buffer boş, boş string döndürülüyor");
                WriteToLogFile($"[DEBUG] Context buffer boş, boş string döndürülüyor");
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetCurrentTypedWord hatası: {ex.Message}");
                WriteToLogFile($"[ERROR] GetCurrentTypedWord hatası: {ex.Message}");
                return "";
            }
        }

        private void SendCtrlLeft()
        {
            // Win32 API kullanarak Ctrl+Left gönder (kelime başına git)
            const byte VK_CONTROL = 0x11;
            const byte VK_LEFT = 0x25;
            const uint KEYEVENTF_KEYUP = 0x0002;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

            // Ctrl tuşunu bas
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            // Left tuşunu bas
            keybd_event(VK_LEFT, 0, 0, System.UIntPtr.Zero);
            // Left tuşunu bırak
            keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Ctrl tuşunu bırak
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        private void SendCtrlShiftRight()
        {
            // Win32 API kullanarak Ctrl+Shift+Right gönder (kelimeyi seç)
            const byte VK_CONTROL = 0x11;
            const byte VK_SHIFT = 0x10;
            const byte VK_RIGHT = 0x27;
            const uint KEYEVENTF_KEYUP = 0x0002;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

            // Ctrl tuşunu bas
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            // Shift tuşunu bas
            keybd_event(VK_SHIFT, 0, 0, System.UIntPtr.Zero);
            // Right tuşunu bas
            keybd_event(VK_RIGHT, 0, 0, System.UIntPtr.Zero);
            // Right tuşunu bırak
            keybd_event(VK_RIGHT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Shift tuşunu bırak
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Ctrl tuşunu bırak
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }

        private void SendCtrlShiftLeft()
        {
            // Win32 API kullanarak Ctrl+Shift+Left gönder (kelimeyi seç)
            const byte VK_CONTROL = 0x11;
            const byte VK_SHIFT = 0x10;
            const byte VK_LEFT = 0x25;
            const uint KEYEVENTF_KEYUP = 0x0002;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

            // Ctrl tuşunu bas
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            // Shift tuşunu bas
            keybd_event(VK_SHIFT, 0, 0, System.UIntPtr.Zero);
            // Left tuşunu bas
            keybd_event(VK_LEFT, 0, 0, System.UIntPtr.Zero);
            // Left tuşunu bırak
            keybd_event(VK_LEFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Shift tuşunu bırak
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            // Ctrl tuşunu bırak
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
        }



        public async Task<LearningStatistics> GetSmartSuggestionsStatisticsAsync()
        {
            return await _smartSuggestionsService.GetStatisticsAsync();
        }

        public async Task RefreshSmartSuggestionsDataAsync()
        {
            try
            {
                Console.WriteLine("[DEBUG] RefreshSmartSuggestionsDataAsync başlıyor...");

                if (_smartSuggestionsService == null)
                {
                    Console.WriteLine("[DEBUG] SmartSuggestionsService null, çıkılıyor");
                    return;
                }

                var stats = await _smartSuggestionsService.GetStatisticsAsync();
                Console.WriteLine($"[DEBUG] İstatistikler alındı: {stats.TotalUniqueWords} kelime");

                // Update basic statistics
                TotalLearnedWords = stats.TotalUniqueWords;
                TotalSuggestionsGiven = stats.TotalSuggestionsGiven;
                AcceptedSuggestions = stats.TotalSuggestionsAccepted;
                AccuracyRate = stats.AccuracyScore;

                // Update progress indicators
                VocabularyProgress = Math.Min(100, (stats.TotalUniqueWords / 1000.0) * 100);
                VocabularyProgressText = $"{stats.TotalUniqueWords} / 1000 kelime";

                PredictionAccuracy = stats.AccuracyScore * 100;
                PredictionAccuracyText = $"{stats.AccuracyScore:P1}";

                // Learning speed calculation (basit)
                LearningSpeed = stats.TotalUniqueWords > 100 ? 75 : stats.TotalUniqueWords > 50 ? 50 : 25;
                LearningSpeedText = LearningSpeed > 60 ? "Hızlı" : LearningSpeed > 30 ? "Orta" : "Yavaş";

                // Update most used words
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MostUsedWords.Clear();
                    if (stats.MostCommonWords.Count > 0)
                    {
                        var maxCount = stats.MostCommonWords.FirstOrDefault().Count;
                        for (int i = 0; i < Math.Min(10, stats.MostCommonWords.Count); i++)
                        {
                            var word = stats.MostCommonWords[i];
                            MostUsedWords.Add(new WordUsageStatistic
                            {
                                Rank = i + 1,
                                Word = word.Word,
                                Count = word.Count,
                                Percentage = maxCount > 0 ? (word.Count / (double)maxCount) * 100 : 0
                            });
                        }
                    }
                });

                // Update N-gram data
                await UpdateNGramDataAsync();

                // Notify property changes
                OnPropertyChanged(nameof(TotalLearnedWords));
                OnPropertyChanged(nameof(TotalSuggestionsGiven));
                OnPropertyChanged(nameof(AcceptedSuggestions));
                OnPropertyChanged(nameof(AccuracyRate));
                OnPropertyChanged(nameof(VocabularyProgress));
                OnPropertyChanged(nameof(VocabularyProgressText));
                OnPropertyChanged(nameof(PredictionAccuracy));
                OnPropertyChanged(nameof(PredictionAccuracyText));
                OnPropertyChanged(nameof(LearningSpeed));
                OnPropertyChanged(nameof(LearningSpeedText));
                OnPropertyChanged(nameof(SmartSuggestionsStatusText));
                OnPropertyChanged(nameof(SmartSuggestionsStatusColor));

                Console.WriteLine($"[SMART SUGGESTIONS] Dashboard verileri güncellendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] RefreshSmartSuggestionsDataAsync hatası: {ex.Message}");
            }
        }

        private async Task UpdateNGramDataAsync()
        {
            try
            {
                Console.WriteLine("[DEBUG] UpdateNGramDataAsync başlıyor...");

                if (_smartSuggestionsService == null)
                {
                    Console.WriteLine("[DEBUG] SmartSuggestionsService null, N-gram güncelleme atlandı");
                    return;
                }

                // TextLearningEngine'den N-gram verilerini al
                var learningData = await _smartSuggestionsService.GetLearningDataAsync();
                Console.WriteLine($"[DEBUG] N-gram verileri alındı: {learningData.TotalBigrams} bigram, {learningData.TotalTrigrams} trigram");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Most Used Words güncelle
                    MostUsedWords.Clear();
                    var topWords = learningData.WordsByFrequency
                        .Where(x => x.Value >= NGramMinFrequency)
                        .OrderByDescending(x => x.Value)
                        .Take(NGramDisplayCount)
                        .ToList();

                    var maxWordCount = topWords.FirstOrDefault().Value;
                    for (int i = 0; i < topWords.Count(); i++)
                    {
                        var word = topWords[i];
                        MostUsedWords.Add(new WordUsageStatistic
                        {
                            Rank = i + 1,
                            Word = word.Key,
                            Count = word.Value,
                            Percentage = maxWordCount > 0 ? (word.Value / (double)maxWordCount) * 100 : 0
                        });
                    }

                    // Bigrams güncelle
                    TopBigrams.Clear();
                    var topBigrams = learningData.BigramsByFrequency
                        .Where(x => x.Value >= NGramMinFrequency)
                        .OrderByDescending(x => x.Value)
                        .Take(NGramDisplayCount)
                        .ToList();

                    var maxBigramCount = topBigrams.FirstOrDefault().Value;
                    for (int i = 0; i < topBigrams.Count; i++)
                    {
                        var bigram = topBigrams[i];
                        TopBigrams.Add(new NGramStatistic
                        {
                            Rank = i + 1,
                            NGram = bigram.Key,
                            Count = bigram.Value,
                            Percentage = maxBigramCount > 0 ? (bigram.Value / (double)maxBigramCount) * 100 : 0,
                            Type = "Bigram"
                        });
                    }

                    // Trigrams güncelle
                    TopTrigrams.Clear();
                    var topTrigrams = learningData.TrigramsByFrequency
                        .Where(x => x.Value >= NGramMinFrequency)
                        .OrderByDescending(x => x.Value)
                        .Take(NGramDisplayCount)
                        .ToList();

                    var maxTrigramCount = topTrigrams.FirstOrDefault().Value;
                    for (int i = 0; i < topTrigrams.Count; i++)
                    {
                        var trigram = topTrigrams[i];
                        TopTrigrams.Add(new NGramStatistic
                        {
                            Rank = i + 1,
                            NGram = trigram.Key,
                            Count = trigram.Value,
                            Percentage = maxTrigramCount > 0 ? (trigram.Value / (double)maxTrigramCount) * 100 : 0,
                            Type = "Trigram"
                        });
                    }
                });

                Console.WriteLine($"[SMART SUGGESTIONS] N-gram verileri güncellendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMART SUGGESTIONS] N-gram güncelleme hatası: {ex.Message}");
        }
        }

        public void ClearLearningLog()
        {
            LearningLog = "Öğrenme logu temizlendi...\n";
            LearningLogEntries.Clear();
            TotalLearnedSentences = 0;
            SaveLearningLogToFile();
        }

        public void AddToLearningLog(string sentence)
        {
            var timestamp = DateTime.Now;
            var logEntry = new LearningLogEntry(sentence, timestamp)
            {
                Id = TotalLearnedSentences + 1
            };

            // Tablo formatına ekle (en yeni en üste)
            LearningLogEntries.Insert(0, logEntry);

            // En son 50 girdiyi tut
            while (LearningLogEntries.Count > 50)
            {
                LearningLogEntries.RemoveAt(LearningLogEntries.Count - 1);
            }

            // Eski format için de güncelle (geriye uyumluluk)
            var timestampStr = timestamp.ToString("HH:mm:ss");
            var textLogEntry = $"[{timestampStr}] Öğrenildi: \"{sentence}\"";

            // Mevcut logları satırlara böl
            var lines = LearningLog.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            // Varsayılan mesajı kaldır
            if (lines.Count == 1 && (lines[0].Contains("Öğrenme logu burada görünecek") ||
                                     lines[0].Contains("Henüz öğrenilen cümle yok") ||
                                     lines[0].Contains("Öğrenme logu yüklenirken hata")))
            {
                lines.Clear();
            }

            // Yeni girdiyi en üste ekle
            lines.Insert(0, textLogEntry);

            // En son 50 satırı tut (en yeniler üstte)
            if (lines.Count > 50)
            {
                lines = lines.Take(50).ToList();
            }

            LearningLog = string.Join("\n", lines) + "\n";

            TotalLearnedSentences++;

            // Öğrenme logunu dosyaya kaydet
            SaveLearningLogToFile();
        }

        private void SaveLearningLogToFile()
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "learning_log.txt");
                var logData = new
                {
                    LearningLog = LearningLog,
                    TotalLearnedSentences = TotalLearnedSentences,
                    LastUpdated = DateTime.Now
                };

                string json = System.Text.Json.JsonSerializer.Serialize(logData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(logPath, json);
                Console.WriteLine($"[DEBUG] Öğrenme logu dosyaya kaydedildi: {logPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Öğrenme logu kaydetme hatası: {ex.Message}");
            }
        }

        private void LoadLearningLogFromFile()
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "learning_log.txt");
                if (File.Exists(logPath))
                {
                    string json = File.ReadAllText(logPath);
                    using (var document = System.Text.Json.JsonDocument.Parse(json))
                    {
                        var root = document.RootElement;

                        if (root.TryGetProperty("LearningLog", out var learningLogProp))
                        {
                            string savedLog = learningLogProp.GetString() ?? "Öğrenme logu burada görünecek...\n";
                            LearningLog = savedLog;

                            // Eski formatı tablo formatına çevir
                            ConvertTextLogToTableFormat(savedLog);
                        }

                        if (root.TryGetProperty("TotalLearnedSentences", out var totalProp))
                        {
                            TotalLearnedSentences = totalProp.GetInt32();
                        }

                        Console.WriteLine($"[DEBUG] Öğrenme logu dosyadan yüklendi: {TotalLearnedSentences} cümle");
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] Öğrenme logu dosyası bulunamadı, varsayılan değerler kullanılıyor");
                    LearningLog = "Henüz öğrenilen cümle yok.\n";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Öğrenme logu yükleme hatası: {ex.Message}");
                LearningLog = "Öğrenme logu yüklenirken hata oluştu.\n";
            }
        }

        private void ConvertTextLogToTableFormat(string textLog)
        {
            try
            {
                LearningLogEntries.Clear();
                var lines = textLog.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
                int id = 1;

                foreach (var line in lines)
                {
                    // [HH:mm:ss] Öğrenildi: "cümle" formatını parse et
                    if (line.Contains("Öğrenildi:") && line.Contains("[") && line.Contains("]"))
                    {
                        try
                        {
                            var timeStart = line.IndexOf('[') + 1;
                            var timeEnd = line.IndexOf(']');
                            var timeStr = line.Substring(timeStart, timeEnd - timeStart);

                            var sentenceStart = line.IndexOf('"') + 1;
                            var sentenceEnd = line.LastIndexOf('"');
                            var sentence = line.Substring(sentenceStart, sentenceEnd - sentenceStart);

                            // Bugünün tarihi ile timestamp oluştur
                            var today = DateTime.Today;
                            if (TimeSpan.TryParse(timeStr, out var time))
                            {
                                var timestamp = today.Add(time);
                                var entry = new LearningLogEntry(sentence, timestamp) { Id = id++ };
                                LearningLogEntries.Add(entry);
                            }
                        }
                        catch
                        {
                            // Parse hatası olursa bu satırı atla
                            continue;
                        }
                    }
                }

                // En yeniler üstte olacak şekilde sırala
                var sortedEntries = LearningLogEntries.OrderByDescending(e => e.Timestamp).ToList();
                LearningLogEntries.Clear();
                foreach (var entry in sortedEntries)
                {
                    LearningLogEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Text log to table conversion error: {ex.Message}");
            }
        }

        public async Task UpdateNGramDisplayCountAsync(int count)
        {
            NGramDisplayCount = count;
            await UpdateNGramDataAsync();
        }

        public async Task UpdateNGramMinFrequencyAsync(int minFrequency)
        {
            NGramMinFrequency = minFrequency;
            await UpdateNGramDataAsync();
        }

        public async Task RefreshNGramDataAsync()
        {
            await UpdateNGramDataAsync();
        }

        // Veri Yönetimi Metodları
        public async Task<bool> UpdateWordAsync(string oldWord, string newWord, int newCount)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.UpdateWordAsync(oldWord, newWord, newCount);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateWordAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteWordAsync(string word)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.DeleteWordAsync(word);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteWordAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateBigramAsync(string oldBigram, string newBigram, int newCount)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.UpdateBigramAsync(oldBigram, newBigram, newCount);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateBigramAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteBigramAsync(string bigram)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.DeleteBigramAsync(bigram);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteBigramAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateTrigramAsync(string oldTrigram, string newTrigram, int newCount)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.UpdateTrigramAsync(oldTrigram, newTrigram, newCount);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateTrigramAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteTrigramAsync(string trigram)
        {
            if (_smartSuggestionsService == null) return false;

            try
            {
                bool success = await _smartSuggestionsService.DeleteTrigramAsync(trigram);
                if (success)
                {
                    await _smartSuggestionsService.SaveDataAsync();
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DeleteTrigramAsync: {ex.Message}");
                return false;
            }
        }

        #region Kısayol Önizleme Paneli Yönetimi

        public void ToggleShortcutPreviewPanel()
        {
            if (IsShortcutPreviewPanelVisible)
            {
                HideShortcutPreviewPanel();
            }
            else
            {
                ShowShortcutPreviewPanel();
            }
        }

        public void ShowShortcutPreviewPanel()
        {
            try
            {
                if (_shortcutPreviewWindow == null)
                {
                    _shortcutPreviewWindow = new Views.ShortcutPreviewWindow(_settingsService);
                    _shortcutPreviewWindow.CloseRequested += (s, e) => HideShortcutPreviewPanel();
                    _shortcutPreviewWindow.Closed += (s, e) => _shortcutPreviewWindow = null;
                }

                // Kısayolları güncelle - her zaman en güncel listeyi göster
                Console.WriteLine($"[DEBUG] Kısayol önizleme paneline {Shortcuts.Count} kısayol gönderiliyor");
                _shortcutPreviewWindow.UpdateShortcuts(Shortcuts);
                _shortcutPreviewWindow.Show();

                // Ayarlarda görünürlüğü true yap
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelVisible = true;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();

                // UI güncellemesi için property changed event'i tetikle
                OnPropertyChanged(nameof(IsShortcutPreviewPanelVisible));
                OnPropertyChanged(nameof(ShortcutPreviewPanelStatusText));
                OnPropertyChanged(nameof(ShortcutPreviewPanelStatusColor));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol önizleme paneli gösterilirken hata: {ex.Message}");
            }
        }

        public void HideShortcutPreviewPanel()
        {
            try
            {
                _shortcutPreviewWindow?.Close();
                _shortcutPreviewWindow = null;

                // Ayarlarda görünürlüğü false yap
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelVisible = false;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();

                // UI güncellemesi için property changed event'i tetikle
                OnPropertyChanged(nameof(IsShortcutPreviewPanelVisible));
                OnPropertyChanged(nameof(ShortcutPreviewPanelStatusText));
                OnPropertyChanged(nameof(ShortcutPreviewPanelStatusColor));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol önizleme paneli gizlenirken hata: {ex.Message}");
            }
        }

        public void UpdateShortcutPreviewPanel()
        {
            if (_shortcutPreviewWindow != null && IsShortcutPreviewPanelVisible)
            {
                _shortcutPreviewWindow.UpdateShortcuts(Shortcuts);
            }
        }

        #endregion

        public void Dispose()
        {
            // Event'leri temizle
            if (_settingsService != null)
                _settingsService.SettingsChanged -= OnSettingsChanged;

            if (_smartSuggestionsService != null)
            {
                _smartSuggestionsService.SuggestionsUpdated -= OnSmartSuggestionsUpdated;
                _smartSuggestionsService.SuggestionAccepted -= OnSmartSuggestionAccepted;
                _smartSuggestionsService.Dispose();
            }

            if (_keyboardHookService != null)
            {
                _keyboardHookService.KeyPressed -= OnKeyPressed;
                _keyboardHookService.WordCompleted -= OnWordCompleted;
                _keyboardHookService.SentenceCompleted -= OnSentenceCompleted;
                _keyboardHookService.CtrlSpacePressed -= OnCtrlSpacePressed;
                _keyboardHookService.TabPressed -= OnTabPressed;
                _keyboardHookService.SpacePressed -= OnSpacePressed;
                _keyboardHookService.Dispose();
            }

            _previewOverlay?.Close();
            _shortcutPreviewWindow?.Close();

            // Preview otomatik kapanma timer'ını temizle
            if (_previewAutoHideTimer != null)
            {
                _previewAutoHideTimer.Stop();
                _previewAutoHideTimer.Dispose();
                _previewAutoHideTimer = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Tour Methods

        private void CheckAndStartFirstRunTour()
        {
            try
            {
                // UI tamamen yüklendikten sonra tur kontrolü yap
                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    await Task.Delay(2000); // UI'nin tamamen yüklenmesini bekle

                    if (_tourService.IsFirstRun)
                    {
                        Console.WriteLine("[TOUR] İlk çalıştırma algılandı - tur başlatılıyor");
                        await StartTourAsync();
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] İlk çalıştırma tur kontrolü hatası: {ex.Message}");
            }
        }

        private void StartTour()
        {
            _ = StartTourAsync();
        }

        private async Task StartTourAsync()
        {
            try
            {
                Console.WriteLine("[TOUR] Tur başlatılıyor...");

                // Tur overlay'ini oluştur ve göster
                var tourOverlay = new Views.TourOverlay(_tourService);
                tourOverlay.Show();

                // Tur servisini başlat
                await _tourService.StartTourAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tur başlatılırken hata: {ex.Message}");
            }
        }

        #endregion

    }
}
