using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
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
        private readonly IUdfEditorTrackingService _udfEditorTrackingService;
        private PreviewOverlay? _previewOverlay;
        private Views.ShortcutPreviewWindow? _shortcutPreviewWindow;
        private bool _isPreviewSyncWithMainWindowEnabled = false;

        private string _shortcutFilter = string.Empty;

        private string _contextBuffer = string.Empty;
        private Shortcut? _selectedShortcut;
        private List<SmartSuggestion> _currentSmartSuggestions = new();
        private string _lastActiveWindow = string.Empty;

        // Basitleştirilmiş Tab işleme sistemi
        private volatile bool _isProcessingTab = false;
        private readonly object _contextBufferLock = new object();
        private readonly object _tabProcessingLock = new object();

        // Tab ile kabul edilen metinleri geçici olarak sakla - sadece cümle tamamlandığında öğrenme loglarına ekle
        private readonly List<string> _pendingTabAcceptedTexts = new List<string>();
        private readonly object _pendingTabTextsLock = new object();

        // UI güncelleme debounce sistemi
        private System.Timers.Timer? _uiUpdateTimer;
        private List<SmartSuggestion> _pendingUISuggestions = new();
        private readonly object _uiUpdateLock = new object();



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
        public ObservableCollection<NGramStatistic> TopFourGrams { get; } = new();

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
        public string ShortcutPreviewButtonText => IsShortcutPreviewPanelVisible ? "🔗 Önizleme (Gizle)" : "🔗 Önizleme (Göster)";

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
            ITourService tourService,
            IUdfEditorTrackingService udfEditorTrackingService)
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
                _udfEditorTrackingService = udfEditorTrackingService ?? throw new ArgumentNullException(nameof(udfEditorTrackingService));

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

                // UDF editörü tracking servisini başlat (gerçek UDF pencere takibi)
                _udfEditorTrackingService.UdfEditorVisibilityChanged += OnUdfEditorTrackingChanged;
                _udfEditorTrackingService.StartTracking();

                // PreviewOverlay event'ini devre dışı bırak - artık gerçek UDF tracking kullanıyoruz
                // Views.PreviewOverlay.UdfEditorVisibilityChanged += OnUdfEditorVisibilityChangedHandler;

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

                // Tab ile kabul edilen metinlerin öğrenme loglarına eklenmesi için event'i dinle
                _smartSuggestionsService.TabAcceptedTextLearned += OnTabAcceptedTextLearned;
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

                // Yeni tuş kombinasyonu event'lerini bağla
                _keyboardHookService.EnterPressed += OnEnterPressed;
                _keyboardHookService.ShiftSpacePressed += OnShiftSpacePressed;
                _keyboardHookService.AltSpacePressed += OnAltSpacePressed;
                _keyboardHookService.CtrlEnterPressed += OnCtrlEnterPressed;
                _keyboardHookService.ShiftEnterPressed += OnShiftEnterPressed;
                _keyboardHookService.AltEnterPressed += OnAltEnterPressed;

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

                // Window focus monitoring başlat ve event'e subscribe ol
                WindowHelper.StartWindowFocusMonitoring();
                WindowHelper.WindowFocusChanged += OnWindowFocusChanged;

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

        private string _lastPreviewText = string.Empty;

        private void SafeSetPreviewText(string text)
        {
            try
            {
                // Aynı text için tekrar işlem yapma - performance optimization
                if (_lastPreviewText == text)
                {
                    return;
                }

                _lastPreviewText = text;

                // SENKRON İŞLEM - Maximum hız için
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    // Zaten UI thread'deyiz - direkt çalıştır
                    SetPreviewTextDirect(text);
                }
                else
                {
                    // UI thread'de değiliz - senkron invoke
                    Application.Current.Dispatcher.Invoke(() => SetPreviewTextDirect(text));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SafeSetPreviewText hatası: {ex.Message}");
            }
        }

        private void SetPreviewTextDirect(string text)
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
                Console.WriteLine($"[ERROR] SetPreviewTextDirect hatası: {ex.Message}");
            }
        }

        private void TestPreviewOverlay()
        {
            try
            {
                Console.WriteLine("[DEBUG] TestPreviewOverlay başlıyor...");
                if (_previewOverlay == null)
                {
                    Console.WriteLine("[ERROR] _previewOverlay null, test edilemiyor");
                    return;
                }

                Console.WriteLine("[DEBUG] _previewOverlay mevcut, test başlatılıyor...");
                // WindowHelper durumunu test et
                bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive();
                Console.WriteLine($"[DEBUG] ShouldTextExpansionBeActive: {shouldBeActive}");
                // Smart Suggestions durumunu test et
                bool smartEnabled = IsSmartSuggestionsEnabled;
                Console.WriteLine($"[DEBUG] IsSmartSuggestionsEnabled: {smartEnabled}");
                // İlk açılışta önizlemeyi gizle (sadece yazı yazarken görünecek)
                HidePreview();
                Console.WriteLine("[DEBUG] İlk açılışta önizleme gizlendi (sadece yazı yazarken görünecek)");
                // AYAR DEBUG - Başlangıçta ayarları kontrol et
                Console.WriteLine($"[AYAR DEBUG] Constructor'da SmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
                // Akıllı öneriler durumunu test et
                TestSmartSuggestions();

                Console.WriteLine("[DEBUG] TestPreviewOverlay tamamlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TestPreviewOverlay hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
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

        public void UpdateStats()
        {
            OnPropertyChanged(nameof(TotalShortcuts));
            OnPropertyChanged(nameof(TotalExpansions));
            OnPropertyChanged(nameof(IsListening));
        }

        public void UpdateAnalytics()
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
            // Yazı yazma zamanını güncelle
            _lastKeyPressTime = DateTime.Now;

            // Preview otomatik kapanma timer'ını yeniden başlat
            RestartPreviewAutoHideTimer();

            // Aktif pencere değişikliği kontrolü
            string currentActiveWindow = WindowHelper.GetActiveWindowTitle();
            if (!string.IsNullOrEmpty(_lastActiveWindow) && _lastActiveWindow != currentActiveWindow)
            {
                Console.WriteLine($"[FOCUS] Pencere değişti: '{_lastActiveWindow}' -> '{currentActiveWindow}'");
                // Pencere değiştiğinde ön izleme penceresini kapat
                Console.WriteLine("[FOCUS] Pencere değişti, ön izleme penceresi kapatılıyor");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HidePreview();
                });
            }
            _lastActiveWindow = currentActiveWindow;

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled, WindowFilterMode);
            Console.WriteLine($"[KEYPRESS] ShouldTextExpansionBeActive: {shouldBeActive}");
            if (!shouldBeActive)
            {
                Console.WriteLine($"[KEYPRESS] OnKeyPressed: Pencere filtreleri nedeniyle işlem yapılmıyor");
                return;
            }

            _contextBuffer = buffer;
            Console.WriteLine($"[KEYPRESS] Context buffer güncellendi: '{_contextBuffer}'");
            Console.WriteLine($"[DEBUG] IsSmartSuggestionsEnabled: {IsSmartSuggestionsEnabled}");
            Console.WriteLine($"[DEBUG] Buffer boş mu: {string.IsNullOrWhiteSpace(buffer)}");
            // Akıllı öneriler etkinse kapsamlı öneri kontrolü yap
            if (IsSmartSuggestionsEnabled && !string.IsNullOrWhiteSpace(buffer))
            {
                Console.WriteLine($"[DEBUG] Akıllı öneriler etkin, ProcessSmartSuggestionsAsync çağrılıyor...");
                await ProcessSmartSuggestionsAsync(buffer);
            }
            else
            {
                Console.WriteLine($"[DEBUG] Akıllı öneriler atlandı - Enabled: {IsSmartSuggestionsEnabled}, Buffer boş: {string.IsNullOrWhiteSpace(buffer)}");
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
                var words = buffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"[SMART SUGGESTIONS] Words count: {words.Length}");
                Console.WriteLine($"[SMART SUGGESTIONS] Buffer ends with space: {buffer.EndsWith(" ")}");
                // 1. ÖNCE KELİME TAMAMLAMA KONTROL ET (henüz tamamlanmamış kelime varsa)
                if (words.Length > 0 && !buffer.EndsWith(" "))
                {
                    var lastWord = words.Last();
                    Console.WriteLine($"[SMART SUGGESTIONS] Last word: '{lastWord}', length: {lastWord.Length}");
                    if (lastWord.Length >= 2)
                    {
                        Console.WriteLine($"[SMART SUGGESTIONS] Kelime tamamlama kontrol ediliyor: '{lastWord}'");
                        UpdateWordCompletionAsync(lastWord, buffer);

                        // Kelime tamamlama önerisi bulunduysa log
                        if (_currentSmartSuggestions.Count > 0)
                        {
                            Console.WriteLine($"[SMART SUGGESTIONS] Kelime tamamlama önerisi bulundu: {_currentSmartSuggestions.Count} öneri");
                        }
                    }
                }

                // 2. SÜREKLI SONRAKI KELİME TAHMİNİ (cümle yapısına göre)
                // HER DURUMDA sonraki kelimeyi tahmin et - sürekli çalışsın
                Console.WriteLine($"[SMART SUGGESTIONS] *** SÜREKLİ SONRAKI KELİME TAHMİNİ BAŞLIYOR ***");
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
                Console.WriteLine($"[NEXT WORD] Kelime sayısı: {words.Length}, Buffer: '{buffer}'");
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
                    return;
                }

                Console.WriteLine($"[NEXT WORD] {analysisType} analizi yapılıyor: '{context}'");

                // SENKRON ÇAĞRI - Maximum hız için
                var suggestions = await _smartSuggestionsService.GetSuggestionsAsync(context, 5);

                if (suggestions.Any())
                {
                    Console.WriteLine($"[NEXT WORD] {suggestions.Count} sonraki kelime önerisi bulundu");
                    // En iyi öneriyi seç
                    var bestSuggestion = suggestions.First();
                    _currentSuggestion = bestSuggestion.Text;
                    _currentSmartSuggestions.Clear();
                    _currentSmartSuggestions.AddRange(suggestions);

                    Console.WriteLine($"[NEXT WORD] En iyi sonraki kelime önerisi: '{bestSuggestion.Text}' (Güven: {bestSuggestion.Confidence:P0})");

                    // UI'ı debounced güncelle - Performans için
                    UpdateSmartSuggestionsUIDebounced(suggestions);
                }
                else
                {
                    Console.WriteLine($"[NEXT WORD] {analysisType} için sonraki kelime önerisi bulunamadı");
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
            }
        }

        private void UpdateSmartSuggestionsUI(List<SmartSuggestion> suggestions)
        {
            try
            {
                SmartSuggestions.Clear();
                foreach (var suggestion in suggestions.Take(5))
                {
                    SmartSuggestions.Add(suggestion);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateSmartSuggestionsUI hatası: {ex.Message}");
            }
        }

        private void UpdateSmartSuggestionsUIDebounced(List<SmartSuggestion> suggestions)
        {
            lock (_uiUpdateLock)
            {
                _pendingUISuggestions = new List<SmartSuggestion>(suggestions);

                // Timer'ı yeniden başlat
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer?.Dispose();

                _uiUpdateTimer = new System.Timers.Timer(150); // 150ms debounce - performans için artırıldı
                _uiUpdateTimer.Elapsed += (sender, e) =>
                {
                    _uiUpdateTimer?.Stop();

                    // UI thread'de güncelle
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            lock (_uiUpdateLock)
                            {
                                UpdateSmartSuggestionsUI(_pendingUISuggestions);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Debounced UI güncelleme hatası: {ex.Message}");
                        }
                    });
                };
                _uiUpdateTimer.Start();
            }
        }

        // BASİT TAHMİN SİSTEMİ (fallback)
        private Task TrySimplePrediction(string[] words)
        {
            try
            {
                Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin sistemi başlıyor");
                // Son kelimeye göre basit tahmin
                var lastWord = words.Last().ToLower();
                var simplePredictions = GetSimpleNextWordPredictions(lastWord);

                if (simplePredictions.Any())
                {
                    _currentSuggestion = simplePredictions.First().Text;
                    _currentSmartSuggestions.Clear();
                    _currentSmartSuggestions.AddRange(simplePredictions);

                    Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin bulundu: '{_currentSuggestion}'");

                    // UI'ı debounced güncelle
                    UpdateSmartSuggestionsUIDebounced(simplePredictions);
                }
                else
                {
                    Console.WriteLine($"[SIMPLE PREDICTION] Basit tahmin de bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] TrySimplePrediction hatası: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private void ShowPreview(string buffer)
        {
            try
            {
                Console.WriteLine($"[PREVIEW] *** ShowPreview çağrıldı, buffer: '{buffer}' ***");
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
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ShowPreview başlangıç hatası: {ex.Message}");
                return;
            }

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa önizleme gösterme
            bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled, WindowFilterMode);
            Console.WriteLine($"[PREVIEW] ShouldTextExpansionBeActive: {shouldBeActive}");
            if (!shouldBeActive)
            {
                Console.WriteLine("[PREVIEW] Pencere filtreleri nedeniyle metin genişletme duraklatıldı");
                // Uyarı mesajı gösterme, sadece önizlemeyi gizle
                HidePreview();
                return;
            }

            if (string.IsNullOrEmpty(buffer))
            {
                Console.WriteLine("[PREVIEW] Buffer boş, önizleme gizleniyor");
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
                        HidePreview();
                        return;
                    }

                    SafeSetPreviewText(previewText);
                    Console.WriteLine($"[PREVIEW] Sadece tahmin gösteriliyor: {previewText}");
                }
            }
            else
            {
                // Buffer boş - önizlemeyi gizle
                Console.WriteLine("[PREVIEW] Buffer boş, önizleme gizleniyor");
                HidePreview();
                _currentSmartSuggestions.Clear();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SmartSuggestions.Clear();
                });
            }
        }

        private void OnWordCompleted(string word)
        {
            // NOT: Context buffer artık OnKeyPressed'de güncelleniyor
            // Bu metod sadece kelime tamamlama event'i için kullanılıyor
            // Çift ekleme sorununu önlemek için context buffer güncellemesi kaldırıldı

            Console.WriteLine($"[WORD_COMPLETED] Kelime tamamlandı: '{word}' (Context buffer OnKeyPressed'de güncelleniyor)");

            // Kelimeyi temizle
            var cleanWord = word.Trim();

            // NOT: Kısayol genişletme artık OnWordCompleted'de yapılmıyor
            // Sadece seçili tuş kombinasyonu event'lerinde yapılacak
        }

        private async void OnSentenceCompleted(string sentence)
        {
            Console.WriteLine($"[SMART SUGGESTIONS] Cümle tamamlandı: '{sentence}'");
            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
                return;

            // Cümle temizle - noktalama işaretlerini kaldır
            var cleanSentence = CleanSentence(sentence);
            Console.WriteLine($"[SMART SUGGESTIONS] Temizlenmiş cümle: '{cleanSentence}'");
            // GELİŞMİŞ ÖĞRENME: Hem kelimeleri hem de kelime çiftlerini öğren
            if (IsSmartSuggestionsEnabled && !string.IsNullOrWhiteSpace(cleanSentence))
            {
                LearnSimpleWords(cleanSentence);
                LearnWordPairs(cleanSentence); // Yeni: Kelime çiftlerini öğren
                Console.WriteLine($"[SMART SUGGESTIONS] Cümle öğrenildi: '{cleanSentence}'");
                // Akıllı öneriler servisine de öğret
                try
                {
                    await _smartSuggestionsService.LearnFromTextAsync(cleanSentence);
                    Console.WriteLine($"[SMART SUGGESTIONS] Cümle akıllı servise öğretildi: '{cleanSentence}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Akıllı servise öğretme hatası: {ex.Message}");
                }

                // Log'a ekle
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddToLearningLog(cleanSentence);

                    // Cümle tamamlandığında pending tab metinlerini de öğrenme loglarına ekle
                    AddPendingTabTextsToLearningLog();
                });
            }
        }

        // BASİT ÖĞRENME FONKSİYONU
        private void LearnSimpleWords(string sentence)
        {
            try
            {
                Console.WriteLine($"[DEBUG] LearnSimpleWords çağrıldı: '{sentence}'");
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
                    }
                }

                Console.WriteLine($"[DEBUG] Toplam öğrenilen kelime sayısı: {_learnedWords.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LearnSimpleWords hatası: {ex.Message}");
            }
        }

        // KELİME ÇİFTLERİNİ ÖĞRENME FONKSİYONU
        private readonly Dictionary<string, List<string>> _learnedWordPairs = new Dictionary<string, List<string>>();

        private void LearnWordPairs(string sentence)
        {
            try
            {
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] LearnWordPairs hatası: {ex.Message}");
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

            // Eğer Ctrl+Space expansion trigger olarak seçilmişse, expansion yap
            if (_settingsService.Settings.ExpansionTriggerKey == ExpansionTriggerKey.CtrlSpace)
            {
                HandleExpansionTrigger(ExpansionTriggerKey.CtrlSpace, _contextBuffer);
                return;
            }

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

            // Eğer pencere filtrelerine uymuyorsa Tab'ı engelleme
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
            {
                Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle Tab işlemi atlandı - normal Tab işlevi");
                return false; // Tab'ı engelleme
            }

            // Öneri yoksa Tab tuşunu engelleme - normal Tab işlevine izin ver
            if (_currentSmartSuggestions.Count == 0 && string.IsNullOrEmpty(_currentSuggestion))
            {
                Console.WriteLine("[DEBUG] Öneri yok - Tab tuşunun normal işlevine izin ver");
                return false; // Tab'ı engelleme - normal işlevine izin ver
            }

            // Eğer zaten tab işleniyor ise, yeni tab'ı engelle
            lock (_tabProcessingLock)
            {
                if (_isProcessingTab)
                {
                    Console.WriteLine("[DEBUG] Tab zaten işleniyor, yeni tab engellendi");
                    return true; // Tab'ı engelle ama işleme
                }
                _isProcessingTab = true;
            }

            // Öneri var - direkt işle
            Console.WriteLine("[DEBUG] Öneri var - direkt işleniyor");
            _ = Task.Run(async () => await ProcessTabDirectly());

            return true; // Tab'ı engelle - metin tamamlama için kullanıldı
        }

        private async Task ProcessTabDirectly()
        {
            try
            {
                Console.WriteLine("[DEBUG] Tab direkt işleme başlıyor");

                SmartSuggestion? suggestionToApply = null;
                string contextToUse = "";

                // Mevcut öneri ve context'i güvenli şekilde al
                lock (_contextBufferLock)
                {
                    Console.WriteLine($"[DEBUG] Mevcut context buffer: '{_contextBuffer}'");
                    Console.WriteLine($"[DEBUG] Mevcut öneri sayısı: {_currentSmartSuggestions.Count}");

                    if (_currentSmartSuggestions.Count > 0)
                    {
                        suggestionToApply = _currentSmartSuggestions[0];
                        contextToUse = _contextBuffer;
                        Console.WriteLine($"[DEBUG] Uygulanacak öneri: '{suggestionToApply.Text}' (Type: {suggestionToApply.Type})");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Hiç öneri yok!");
                    }
                }

                if (suggestionToApply != null)
                {
                    Console.WriteLine($"[DEBUG] *** Tab ile öneri uygulanıyor: {suggestionToApply.Text} ***");

                    // Öneriyi servis tarafında kabul et (istatistik tutmak için)
                    try
                    {
                        if (_smartSuggestionsService != null)
                        {
                            await _smartSuggestionsService.AcceptSuggestionAsync(suggestionToApply, contextToUse);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] AcceptSuggestionAsync hatası: {ex.Message}");
                    }

                    // Öneriyi TYPING ile uygula (öğrenme sistemine girmesi için)
                    // NOT: Öğrenme sistemi artık ApplySmartSuggestionAsTyping içinde çağrılıyor
                    await ApplySmartSuggestionAsTyping(suggestionToApply, contextToUse);

                    Console.WriteLine("[DEBUG] Tab ile öneri başarıyla uygulandı (typing mode)");

                    // ANINDA yeni context ile öneri ara - delay yok
                    Console.WriteLine("[DEBUG] *** ANINDA yeni öneriler aranıyor ***");

                    string currentContext;
                    lock (_contextBufferLock)
                    {
                        currentContext = _contextBuffer;
                        Console.WriteLine($"[DEBUG] Güncellenmiş context buffer: '{currentContext}'");
                    }

                    // Hemen yeni öneriler ara - async olmadan
                    try
                    {
                        Console.WriteLine($"[DEBUG] ANINDA yeni context ile öneri aranıyor: '{currentContext}'");
                        await ProcessSmartSuggestionsAsync(currentContext);

                        // Preview'ı hemen güncelle
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (!string.IsNullOrEmpty(_currentSuggestion))
                            {
                                ShowPreview(currentContext);
                                Console.WriteLine($"[DEBUG] Preview güncellendi: '{_currentSuggestion}'");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] ANINDA öneri arama hatası: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] Tab işleme sırasında öneri bulunamadı");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ProcessTabDirectly hatası: {ex.Message}");
            }
            finally
            {
                // Tab işleme flag'ini serbest bırak
                lock (_tabProcessingLock)
                {
                    _isProcessingTab = false;
                }
                Console.WriteLine("[DEBUG] Tab işleme tamamlandı");
            }
        }

        /// <summary>
        /// Tab ile kabul edilen önerileri typing ile uygular - öğrenme sistemine girmesi için
        /// </summary>
        private async Task ApplySmartSuggestionAsTyping(SmartSuggestion suggestion, string originalContext)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** Typing ile öneri uygulanıyor: {suggestion.Text} (Type: {suggestion.Type}) ***");

                // UI'ı güncelle - önerileri temizle
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SmartSuggestions.Clear();
                });

                // Öneriyi kullanıcı metnine uygula
                switch (suggestion.Type)
                {
                    case SuggestionType.WordCompletion:
                        // Mevcut kelimeyi seçip tam kelime ile değiştir
                        await ApplyWordCompletionAsTyping(suggestion.Text);
                        break;

                    default:
                        // Sonraki kelime veya kelime grubu → başına boşluk ekleyerek ekle
                        await ApplySuggestionTextAsTyping(suggestion.Text);
                        break;
                }

                // Context buffer'ı güncelle - AGRESIF GÜNCELLEME
                lock (_contextBufferLock)
                {
                    Console.WriteLine($"[DEBUG] Context buffer güncelleme öncesi: '{_contextBuffer}'");

                    // Önce mevcut güncelleme metodunu çağır
                    UpdateContextBufferAfterSuggestionDirect(suggestion, originalContext);

                    // Sonra manuel olarak da güncelle - çifte güvence
                    string textToAdd = suggestion.Type == SuggestionType.WordCompletion ?
                        GetRemainingPartOfWord(suggestion.Text) :
                        " " + suggestion.Text;

                    if (!string.IsNullOrEmpty(textToAdd))
                    {
                        _contextBuffer += textToAdd;
                        if (_contextBuffer.Length > 200)
                        {
                            _contextBuffer = _contextBuffer.Substring(_contextBuffer.Length - 200);
                        }
                    }

                    Console.WriteLine($"[DEBUG] Context buffer güncelleme sonrası: '{_contextBuffer}'");
                }

                // Mevcut önerileri temizle
                _currentSmartSuggestions.Clear();
                _currentSuggestion = "";

                Console.WriteLine("[DEBUG] Typing ile öneri başarıyla uygulandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ApplySmartSuggestionAsTyping hatası: {ex.Message}");
            }
        }

        private async Task ApplySmartSuggestionDirectly(SmartSuggestion suggestion, string originalContext)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** Direkt öneri uygulanıyor: {suggestion.Text} (Type: {suggestion.Type}) ***");

                // UI'ı güncelle - önerileri temizle
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
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

                // Context buffer'ı güncelle
                lock (_contextBufferLock)
                {
                    UpdateContextBufferAfterSuggestionDirect(suggestion, originalContext);
                }

                // Mevcut önerileri temizle
                _currentSmartSuggestions.Clear();
                _currentSuggestion = "";

                Console.WriteLine("[DEBUG] Direkt öneri başarıyla uygulandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ApplySmartSuggestionDirectly hatası: {ex.Message}");
            }
        }

        private void UpdateContextBufferAfterSuggestion(SmartSuggestion suggestion, string originalContext)
        {
            try
            {
                if (suggestion.Type == SuggestionType.WordCompletion)
                {
                    // Kelime tamamlama: son kelimeyi tam kelime ile değiştir
                    var words = originalContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                    {
                        words[words.Length - 1] = suggestion.Text;
                        _contextBuffer = string.Join(" ", words) + " ";
                    }
                }
                else
                {
                    // Yeni kelime ekleme: boşluk ile ekle
                    _contextBuffer = originalContext + " " + suggestion.Text;
                }

                // Buffer overflow kontrolü - uzun metinler için artırıldı
                if (_contextBuffer.Length > 500)
                {
                    var words = _contextBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    _contextBuffer = string.Join(" ", words.TakeLast(25)) + " ";
                }

                Console.WriteLine($"[DEBUG] Context buffer güncellendi: '{_contextBuffer}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateContextBufferAfterSuggestion hatası: {ex.Message}");
            }
        }

        private void UpdateContextBufferAfterSuggestionDirect(SmartSuggestion suggestion, string originalContext)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Context buffer güncelleniyor - Öneri: '{suggestion.Text}', Orijinal: '{originalContext}'");

                string newBuffer = "";

                if (suggestion.Type == SuggestionType.WordCompletion)
                {
                    // Kelime tamamlama: son kelimeyi tam kelime ile değiştir
                    var words = originalContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                    {
                        words[words.Length - 1] = suggestion.Text;
                        newBuffer = string.Join(" ", words) + " ";
                    }
                    else
                    {
                        newBuffer = suggestion.Text + " ";
                    }
                }
                else
                {
                    // Yeni kelime ekleme: duplicate kontrolü ile
                    var originalWords = originalContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var suggestionWords = suggestion.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // Son kelimeler ile öneri kelimeleri arasında overlap var mı kontrol et
                    bool hasOverlap = false;
                    if (originalWords.Length > 0 && suggestionWords.Length > 0)
                    {
                        // Son birkaç kelimeyi kontrol et
                        int checkCount = Math.Min(originalWords.Length, suggestionWords.Length);
                        for (int i = 1; i <= checkCount; i++)
                        {
                            var lastWords = originalWords.TakeLast(i).ToArray();
                            var firstWords = suggestionWords.Take(i).ToArray();

                            if (lastWords.SequenceEqual(firstWords, StringComparer.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[DEBUG] Overlap detected: {string.Join(" ", lastWords)}");
                                hasOverlap = true;

                                // Overlap'i kaldır
                                var remainingSuggestion = string.Join(" ", suggestionWords.Skip(i));
                                if (!string.IsNullOrEmpty(remainingSuggestion))
                                {
                                    newBuffer = originalContext.TrimEnd() + " " + remainingSuggestion + " ";
                                }
                                else
                                {
                                    newBuffer = originalContext.TrimEnd() + " ";
                                }
                                break;
                            }
                        }
                    }

                    if (!hasOverlap)
                    {
                        // Normal ekleme
                        if (originalContext.EndsWith(" "))
                        {
                            newBuffer = originalContext + suggestion.Text + " ";
                        }
                        else
                        {
                            newBuffer = originalContext + " " + suggestion.Text + " ";
                        }
                    }
                }

                _contextBuffer = newBuffer;

                // Buffer overflow kontrolü - uzun metinler için optimize edildi
                if (_contextBuffer.Length > 500)
                {
                    var words = _contextBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    _contextBuffer = string.Join(" ", words.TakeLast(25)) + " ";
                }

                Console.WriteLine($"[DEBUG] Context buffer direkt güncellendi: '{_contextBuffer}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateContextBufferAfterSuggestionDirect hatası: {ex.Message}");
                // Fallback: basit ekleme
                _contextBuffer = originalContext + " " + suggestion.Text + " ";
            }
        }



        private async void OnSpacePressed(string currentBuffer)
        {
            Console.WriteLine($"[DEBUG] *** Boşluk tuşu basıldı, buffer: '{currentBuffer}' ***");

            // Eğer Space expansion trigger olarak seçilmişse, expansion yap
            if (_settingsService.Settings.ExpansionTriggerKey == ExpansionTriggerKey.Space)
            {
                HandleExpansionTrigger(ExpansionTriggerKey.Space, currentBuffer);
            }

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

        /// <summary>
        /// Kelime tamamlamayı clipboard ile yapar - Türkçe karakter desteği için
        /// </summary>
        private async Task ApplyWordCompletionAsTyping(string fullWord)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** ApplyWordCompletionAsTyping (Clipboard Mode) başlıyor: {fullWord} ***");

                // Kullanıcının yazdığı kısmı hesapla
                string userTypedPart = GetCurrentTypedWord();
                Console.WriteLine($"[DEBUG] Kullanıcının yazdığı kısım: '{userTypedPart}'");

                // Sadece eksik kısmı hesapla
                string remainingPart = "";
                if (!string.IsNullOrEmpty(userTypedPart) && fullWord.StartsWith(userTypedPart, StringComparison.OrdinalIgnoreCase))
                {
                    remainingPart = fullWord.Substring(userTypedPart.Length);
                    Console.WriteLine($"[DEBUG] Eksik kısım: '{remainingPart}'");
                }
                else
                {
                    // Eğer eşleşme yoksa tam kelimeyi yaz
                    remainingPart = fullWord;
                    Console.WriteLine($"[DEBUG] Tam kelime yazılacak: '{remainingPart}'");
                }

                if (!string.IsNullOrEmpty(remainingPart))
                {
                    // TAB İLE KABUL EDİLEN METNİ ÖZEL ÖĞRENME SİSTEMİNE GÖNDER
                    // PASTE TRACKING SİSTEMİNİ TAMAMEN BYPASS ET
                    try
                    {
                        Console.WriteLine($"[TAB_LEARNING] *** Tab ile kabul edilen kelime ÖZEL öğrenme sistemine gönderiliyor: '{fullWord}' ***");

                        // Özel tab öğrenme metodunu kullan
                        if (_smartSuggestionsService != null)
                        {
                            await _smartSuggestionsService.LearnFromTabAcceptedTextAsync(fullWord);
                            Console.WriteLine($"[TAB_LEARNING] *** Tab kelime ÖZEL YÖNTEMLE öğrenildi: '{fullWord}' ***");
                        }

                        // Tab ile kabul edilen metni KeyboardHookService'in sentence buffer'ına ekle
                        _keyboardHookService?.AddTabCompletedTextToSentenceBuffer(remainingPart);
                        Console.WriteLine($"[TAB_LEARNING] *** Tab kelime KeyboardHookService sentence buffer'ına eklendi: '{remainingPart}' ***");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Tab kelime öğrenme hatası: {ex.Message}");
                    }

                    // Clipboard ile eksik kısmı yaz - Türkçe karakter desteği için
                    bool success = await _advancedInputService.SendTextAsync(remainingPart);

                    if (success)
                    {
                        Console.WriteLine($"[DEBUG] Kelime tamamlandı (clipboard): {fullWord}");

                        // Context buffer'ı güncelle
                        lock (_contextBufferLock)
                        {
                            _contextBuffer += remainingPart;
                            if (_contextBuffer.Length > 200)
                            {
                                _contextBuffer = _contextBuffer.Substring(_contextBuffer.Length - 200);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] Clipboard başarısız, fallback kullanılıyor");
                        // Fallback: eski yöntem
                        await ApplyWordCompletionAsync(fullWord);
                    }
                }

                Console.WriteLine($"[DEBUG] *** ApplyWordCompletionAsTyping tamamlandı: {fullWord} ***");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ApplyWordCompletionAsTyping hatası: {ex.Message}");
                // Fallback: eski yöntem
                await ApplyWordCompletionAsync(fullWord);
            }
        }

        /// <summary>
        /// Sonraki kelime önerisini clipboard ile yapar - Türkçe karakter desteği için
        /// </summary>
        private async Task ApplySuggestionTextAsTyping(string suggestionText)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsTyping (Clipboard Mode) başlıyor: {suggestionText} ***");

                // TAB İLE KABUL EDİLEN METNİ ÖZEL ÖĞRENME SİSTEMİNE GÖNDER
                // PASTE TRACKING SİSTEMİNİ TAMAMEN BYPASS ET
                try
                {
                    Console.WriteLine($"[TAB_LEARNING] *** Tab ile kabul edilen sonraki kelime ÖZEL öğrenme sistemine gönderiliyor: '{suggestionText}' ***");

                    // Özel tab öğrenme metodunu kullan
                    if (_smartSuggestionsService != null)
                    {
                        await _smartSuggestionsService.LearnFromTabAcceptedTextAsync(suggestionText);
                        Console.WriteLine($"[TAB_LEARNING] *** Tab sonraki kelime ÖZEL YÖNTEMLE öğrenildi: '{suggestionText}' ***");
                    }

                    // Tab ile kabul edilen metni KeyboardHookService'in sentence buffer'ına ekle
                    // Context buffer'ın sonunda boşluk var mı kontrol et
                    bool needsSpace = !_contextBuffer.EndsWith(" ");
                    var textToAdd = needsSpace ? " " + suggestionText : suggestionText;
                    _keyboardHookService?.AddTabCompletedTextToSentenceBuffer(textToAdd);
                    Console.WriteLine($"[TAB_LEARNING] *** Tab sonraki kelime KeyboardHookService sentence buffer'ına eklendi: '{textToAdd}' ***");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Tab sonraki kelime öğrenme hatası: {ex.Message}");
                }

                // Başına boşluk ekle
                string textToType = " " + suggestionText;

                // Clipboard ile yaz - Türkçe karakter desteği için
                bool success = await _advancedInputService.SendTextAsync(textToType);

                if (success)
                {
                    Console.WriteLine($"[DEBUG] Sonraki kelime eklendi (clipboard): {suggestionText}");

                    // Context buffer'ı güncelle
                    lock (_contextBufferLock)
                    {
                        _contextBuffer += textToType;
                        if (_contextBuffer.Length > 200)
                        {
                            _contextBuffer = _contextBuffer.Substring(_contextBuffer.Length - 200);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] Clipboard başarısız, fallback kullanılıyor");
                    // Fallback: eski yöntem
                    await ApplySuggestionTextAsync(suggestionText);
                }

                Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsTyping tamamlandı: {suggestionText} ***");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ApplySuggestionTextAsTyping hatası: {ex.Message}");
                // Fallback: eski yöntem
                await ApplySuggestionTextAsync(suggestionText);
            }
        }

        /// <summary>
        /// Kelime tamamlama için eksik kısmı hesaplar
        /// </summary>
        private string GetRemainingPartOfWord(string fullWord)
        {
            try
            {
                string userTypedPart = GetCurrentTypedWord();
                if (!string.IsNullOrEmpty(userTypedPart) && fullWord.StartsWith(userTypedPart, StringComparison.OrdinalIgnoreCase))
                {
                    return fullWord.Substring(userTypedPart.Length);
                }
                return fullWord;
            }
            catch
            {
                return fullWord;
            }
        }

        private async Task SendTabCharacterToActiveWindow()
        {
            try
            {
                Console.WriteLine($"[DEBUG] SendTabCharacterToActiveWindow başlıyor");
                // AdvancedInputService kullanarak Tab tuşunu simüle et
                const ushort VK_TAB = 0x09;
                bool success = await _advancedInputService.SimulateKeyPressAsync(VK_TAB);

                if (success)
                {
                    Console.WriteLine($"[DEBUG] Tab karakteri başarıyla gönderildi");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Tab karakteri gönderilemedi");
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
            }
        }

        private void AcceptSmartSuggestion()
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] HidePreview hatası: {ex.Message}");
            }
        }

        // BASİT VE MANTIKLI AKILLI ÖNERİ SİSTEMİ
        private readonly List<string> _learnedWords = new List<string>();

        // Yazı yazma durumu takibi için
        private DateTime _lastKeyPressTime = DateTime.MinValue;
        // Preview otomatik kapanma timer'ı (3 saniye)
        private System.Timers.Timer? _previewAutoHideTimer;

        private void UpdateWordCompletionAsync(string partialWord, string fullContext)
        {
            Console.WriteLine($"[DEBUG] *** UpdateWordCompletionAsync çağrıldı ***");
            Console.WriteLine($"[DEBUG] Partial word: '{partialWord}'");
            Console.WriteLine($"[DEBUG] Full context: '{fullContext}'");
            try
            {
                Console.WriteLine($"[DEBUG] Try bloğuna girdi");
                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions çağrılacak...");
                // BASİT YAKLAŞIM: Öğrenilen kelimelerden eşleşenleri bul
                var suggestions = GetSimpleWordCompletions(partialWord);

                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions tamamlandı");
                Console.WriteLine($"[DEBUG] Basit kelime tamamlama önerileri: {suggestions.Count} adet");
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
                    }
                });

                // İlk öneriyi mevcut öneri olarak ayarla
                if (suggestions.Count > 0)
                {
                    _currentSuggestion = suggestions[0].Text;
                    Console.WriteLine($"[DEBUG] Mevcut öneri ayarlandı: '{_currentSuggestion}'");
                }
                else
                {
                    _currentSuggestion = "";
                    Console.WriteLine($"[DEBUG] Öneri bulunamadı, mevcut öneri temizlendi");
                }

                Console.WriteLine($"[DEBUG] UpdateWordCompletionAsync tamamlandı, {suggestions.Count} öneri eklendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UpdateWordCompletionAsync hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        // BASİT KELİME TAMAMLAMA FONKSİYONU
        private List<SmartSuggestion> GetSimpleWordCompletions(string partialWord)
        {
            var suggestions = new List<SmartSuggestion>();

            try
            {
                Console.WriteLine($"[DEBUG] GetSimpleWordCompletions çağrıldı: '{partialWord}'");
                // Öğrenilen kelimeler listesinde eşleşenleri bul
                var matches = _learnedWords
                    .Where(word => word.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase) && word.Length > partialWord.Length)
                    .Distinct()
                    .Take(5)
                    .ToList();

                Console.WriteLine($"[DEBUG] Eşleşen kelimeler: {matches.Count} adet");
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
                }

                // Eğer öğrenilen kelimelerden bulamazsa, varsayılan öneriler ekle
                if (suggestions.Count == 0)
                {
                    var defaultSuggestions = GetDefaultWordCompletions(partialWord);
                    suggestions.AddRange(defaultSuggestions);

                    Console.WriteLine($"[DEBUG] Varsayılan öneriler eklendi: {defaultSuggestions.Count} adet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetSimpleWordCompletions hatası: {ex.Message}");
            }

            return suggestions;
        }

        // BASİT SONRAKI KELİME TAHMİNLERİ
        private List<SmartSuggestion> GetSimpleNextWordPredictions(string lastWord)
        {
            var suggestions = new List<SmartSuggestion>();

            try
            {

                // Öğrenilen kelime çiftlerinden tahmin yap
                var learnedKey = _learnedWordPairs.Keys.FirstOrDefault(k => k.Equals(lastWord, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(learnedKey))
                {
                    var learnedPredictions = _learnedWordPairs[learnedKey];

                    foreach (var prediction in learnedPredictions.Take(3))
                    {
                        suggestions.Add(new SmartSuggestion
                        {
                            Text = prediction,
                            Type = SuggestionType.NextWord,
                            Confidence = 0.9,
                            Context = lastWord,
                            Frequency = 1,
                            LastUsed = DateTime.Now
                        });
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetSimpleNextWordPredictions hatası: {ex.Message}");
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

                // Bu kelime tamamlamasını yakın zamanda yapıştırılacak olarak işaretle
                _smartSuggestionsService?.MarkSuggestionAsPasted(fullWord);
                // Kullanıcının yazdığı kısmı hesapla
                string userTypedPart = GetCurrentTypedWord();
                Console.WriteLine($"[DEBUG] Kullanıcının yazdığı kısım: '{userTypedPart}'");
                // Sadece eksik kısmı hesapla
                string remainingPart = "";
                if (!string.IsNullOrEmpty(userTypedPart) && fullWord.StartsWith(userTypedPart, StringComparison.OrdinalIgnoreCase))
                {
                    remainingPart = fullWord.Substring(userTypedPart.Length);
                    Console.WriteLine($"[DEBUG] Eksik kısım: '{remainingPart}'");
                }
                else
                {
                    // Eğer eşleşme yoksa tam kelimeyi kullan
                    remainingPart = fullWord;
                    Console.WriteLine($"[DEBUG] Eşleşme yok, tam kelime kullanılıyor: '{remainingPart}'");
                }

                // Eğer eksik kısım yoksa hiçbir şey yapma
                if (string.IsNullOrEmpty(remainingPart))
                {
                    Console.WriteLine($"[DEBUG] Eksik kısım yok, işlem atlandı");
                    return;
                }

                // UI thread'de çalıştır (STA gerekli)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Sadece eksik kısım yazılıyor: '{remainingPart}'");
                        // Clipboard'ı geçici olarak kaydet
                        string originalClipboard = string.Empty;
                        try
                        {
                            originalClipboard = System.Windows.Clipboard.GetText();
                            Console.WriteLine($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                        }

                        // Sadece eksik kısmı clipboard'a koy
                        Console.WriteLine($"[DEBUG] Clipboard'a eksik kısım yazılıyor: '{remainingPart}'");
                        System.Windows.Clipboard.SetText(remainingPart);
                        Thread.Sleep(10);

                        // Ctrl+V ile yapıştır (eksik kısmı ekler)
                        Console.WriteLine($"[DEBUG] Ctrl+V gönderiliyor...");
                        SendCtrlV();

                        // Kelime tamamlandı - boşluk eklenmez, kullanıcı isterse space tuşuna basabilir
                        Thread.Sleep(50); // Kısa bekleme
                        Console.WriteLine($"[DEBUG] Kelime tamamlandı - boşluk eklenmedi");
                        Console.WriteLine($"[DEBUG] *** ApplyWordCompletionAsync tamamlandı: {fullWord} (boşluk yok) ***");
                        // Kelime tamamlandıktan sonra hemen yeni context ile sonraki kelimeyi tahmin et
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Minimum bekleme - maximum hız için
                                await Task.Delay(10);

                                // Yeni context oluştur (sadece eksik kısım eklendi - boşluk yok)
                                var newContext = _contextBuffer + remainingPart;
                                Console.WriteLine($"[DEBUG] *** TAB SONRASI YENİ TAHMİN *** Context: '{newContext}'");
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
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Kelime sonrası tahmin hatası: {ex.Message}");
                            }
                        });

                        // Orijinal clipboard'ı geri yükle
                        Task.Run(async () =>
                        {
                            await Task.Delay(100); // Optimize edildi
                            try
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (!string.IsNullOrEmpty(originalClipboard))
                                    {
                                        System.Windows.Clipboard.SetText(originalClipboard);
                                        Console.WriteLine($"[DEBUG] Orijinal clipboard geri yüklendi");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Kelime tamamlama iç hatası: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kelime tamamlama hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private async Task ApplySuggestionTextAsync(string suggestionText)
        {
            try
            {
                Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsync başlıyor: {suggestionText} ***");

                // Bu öneriyi yakın zamanda yapıştırılacak olarak işaretle
                _smartSuggestionsService?.MarkSuggestionAsPasted(suggestionText);
                // UI thread'de çalıştır (STA gerekli)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Metin ekleme: {suggestionText}");
                        // Clipboard'ı geçici olarak kaydet
                        string originalClipboard = string.Empty;
                        try
                        {
                            originalClipboard = System.Windows.Clipboard.GetText();
                            Console.WriteLine($"[DEBUG] Orijinal clipboard kaydedildi: '{originalClipboard}'");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Clipboard okuma hatası: {ex.Message}");
                        }

                        // Context buffer'ın sonunda boşluk var mı kontrol et
                        bool needsSpace = !_contextBuffer.EndsWith(" ");

                        if (needsSpace)
                        {
                            Console.WriteLine($"[DEBUG] Context buffer sonunda boşluk yok, boşluk ekleniyor...");
                            SendSpace();
                            // Kısa bekleme
                            Thread.Sleep(50);
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Context buffer sonunda zaten boşluk var, ekstra boşluk eklenmiyor...");
                        }

                        // Öneri metnini clipboard'a koy
                        Console.WriteLine($"[DEBUG] Clipboard'a metin yazılıyor: '{suggestionText}'");
                        System.Windows.Clipboard.SetText(suggestionText);

                        // Kısa bekleme
                        Thread.Sleep(10);

                        // Ctrl+V ile yapıştır
                        Console.WriteLine($"[DEBUG] Ctrl+V gönderiliyor...");
                        SendCtrlV();

                        Console.WriteLine($"[DEBUG] *** ApplySuggestionTextAsync tamamlandı: {(needsSpace ? "boşluk + " : "")}{suggestionText} ***");
                        // ÖNEMLİ: Öneri eklendikten sonra HEMEN yeni context ile sonraki kelimeyi tahmin et
                        Task.Run(async () =>
                        {
                            try
                            {
                                // Minimum bekleme - maximum hız için
                                await Task.Delay(10);

                                // Yeni context oluştur - eğer context buffer sonunda boşluk yoksa ekle
                                var newContext = _contextBuffer.EndsWith(" ") ?
                                    _contextBuffer + suggestionText :
                                    _contextBuffer + " " + suggestionText;
                                Console.WriteLine($"[DEBUG] *** TAB SONRASI YENİ TAHMİN BAŞLIYOR *** Context: '{newContext}'");
                                // Context buffer'ı güncelle
                                _contextBuffer = newContext;

                                // Kelimeleri ayır ve analiz et
                                var words = newContext.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                Console.WriteLine($"[DEBUG] TAB SONRASI - Kelime sayısı: {words.Length}, Son kelime: '{(words.Length > 0 ? words.Last() : "YOK")}'");
                                // HEMEN yeni tahmin yap - hem akıllı servis hem de öğrendiği verilerle
                                await ProcessSmartSuggestionsAsync(newContext);

                                // Eğer akıllı servis sonuç vermezse, direkt öğrendiği verilerle tahmin yap
                                if (_currentSmartSuggestions.Count == 0 && words.Length > 0)
                                {
                                    Console.WriteLine($"[DEBUG] TAB SONRASI - Akıllı servis sonuç vermedi, öğrendiği verilerle tahmin yapılıyor");
                                    var lastWord = words.Last().ToLower();
                                    var simplePredictions = GetSimpleNextWordPredictions(lastWord);

                                    if (simplePredictions.Any())
                                    {
                                        _currentSuggestion = simplePredictions.First().Text;
                                        _currentSmartSuggestions.Clear();
                                        _currentSmartSuggestions.AddRange(simplePredictions);

                                        Console.WriteLine($"[DEBUG] TAB SONRASI - Basit tahmin bulundu: '{_currentSuggestion}'");
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
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Öneri sonrası tahmin hatası: {ex.Message}");
                            }
                        });

                        // Orijinal clipboard'ı geri yükle
                        Task.Run(async () =>
                        {
                            await Task.Delay(100); // Optimize edildi
                            try
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (!string.IsNullOrEmpty(originalClipboard))
                                    {
                                        System.Windows.Clipboard.SetText(originalClipboard);
                                        Console.WriteLine($"[DEBUG] Orijinal clipboard geri yüklendi");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[DEBUG] Clipboard geri yükleme hatası: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Metin ekleme iç hatası: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Metin uygulama hatası: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
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
                        return lastWord;
                    }
                }

                // Eğer context buffer boşsa, keyboard hook'tan gelen son kelimeyi kullan
                // Bu kısım için KeyboardHookService'den son yazılan kelimeyi almamız gerekiyor
                // Şimdilik basit bir yaklaşım kullanalım
                Console.WriteLine($"[DEBUG] Context buffer boş, boş string döndürülüyor");
                return "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetCurrentTypedWord hatası: {ex.Message}");
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

                    // 4-grams güncelle
                    TopFourGrams.Clear();
                    var topFourGrams = learningData.FourGramsByFrequency
                        .Where(x => x.Value >= NGramMinFrequency)
                        .OrderByDescending(x => x.Value)
                        .Take(NGramDisplayCount)
                        .ToList();

                    var maxFourGramCount = topFourGrams.FirstOrDefault().Value;
                    for (int i = 0; i < topFourGrams.Count; i++)
                    {
                        var fourgram = topFourGrams[i];
                        TopFourGrams.Add(new NGramStatistic
                        {
                            Rank = i + 1,
                            NGram = fourgram.Key,
                            Count = fourgram.Value,
                            Percentage = maxFourGramCount > 0 ? (fourgram.Value / (double)maxFourGramCount) * 100 : 0,
                            Type = "4-gram"
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

        public async Task ResetAllLearningDataAsync()
        {
            try
            {
                Console.WriteLine("[DEBUG] Tüm öğrenme verileri sıfırlanıyor...");

                // 1. SmartSuggestionsService'deki tüm öğrenme verilerini sıfırla
                if (_smartSuggestionsService != null)
                {
                    await _smartSuggestionsService.ResetLearningDataAsync();
                    Console.WriteLine("[DEBUG] SmartSuggestionsService verileri sıfırlandı");
                }

                // 2. Local öğrenme verilerini temizle
                _learnedWords.Clear();
                _learnedWordPairs.Clear();
                Console.WriteLine("[DEBUG] Local öğrenme verileri temizlendi");

                // 3. Öğrenme logunu temizle
                ClearLearningLog();
                Console.WriteLine("[DEBUG] Öğrenme logu temizlendi");

                // 4. UI'daki istatistikleri sıfırla
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Temel istatistikler
                    TotalLearnedWords = 0;
                    TotalSuggestionsGiven = 0;
                    AcceptedSuggestions = 0;
                    AccuracyRate = 0;

                    // Progress göstergeleri
                    VocabularyProgress = 0;
                    VocabularyProgressText = "0 / 1000 kelime";
                    PredictionAccuracy = 0;
                    PredictionAccuracyText = "0%";
                    LearningSpeed = 0;
                    LearningSpeedText = "Başlangıç";

                    // Listeler
                    MostUsedWords.Clear();
                    TopBigrams.Clear();
                    TopTrigrams.Clear();
                    TopFourGrams.Clear();
                    SmartSuggestions.Clear();
                });

                // 5. Öğrenme dosyalarını sil
                await DeleteLearningFilesAsync();

                Console.WriteLine("[DEBUG] Tüm öğrenme verileri başarıyla sıfırlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Öğrenme verileri sıfırlama hatası: {ex.Message}");
                throw;
            }
        }

        private async Task DeleteLearningFilesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // Silinecek dosyalar
                    string[] filesToDelete = {
                        Path.Combine(baseDirectory, "learning_log.txt"),
                        Path.Combine(baseDirectory, "smart_suggestions_data.json"),
                        Path.Combine(baseDirectory, "learning_data.json")
                    };

                    foreach (string filePath in filesToDelete)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            Console.WriteLine($"[DEBUG] Dosya silindi: {filePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Öğrenme dosyaları silme hatası: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Tab ile kabul edilen metinleri geçici olarak sakla - sadece cümle tamamlandığında öğrenme loglarına ekle
        /// </summary>
        private void OnTabAcceptedTextLearned(string text)
        {
            try
            {
                Console.WriteLine($"[TAB_LEARNING] *** OnTabAcceptedTextLearned event alındı: '{text}' ***");

                // Tab ile kabul edilen metni pending listesine ekle - hemen öğrenme loglarına ekleme
                lock (_pendingTabTextsLock)
                {
                    _pendingTabAcceptedTexts.Add(text);
                    Console.WriteLine($"[TAB_LEARNING] *** Tab metni pending listesine eklendi: '{text}' (Toplam pending: {_pendingTabAcceptedTexts.Count}) ***");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnTabAcceptedTextLearned hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Pending tab metinlerini öğrenme loglarına ekle ve listeyi temizle
        /// </summary>
        private void AddPendingTabTextsToLearningLog()
        {
            try
            {
                List<string> textsToAdd;

                // Pending listesini kopyala ve temizle
                lock (_pendingTabTextsLock)
                {
                    if (_pendingTabAcceptedTexts.Count == 0)
                    {
                        Console.WriteLine("[TAB_LEARNING] Pending tab metni yok, işlem atlandı");
                        return;
                    }

                    textsToAdd = new List<string>(_pendingTabAcceptedTexts);
                    _pendingTabAcceptedTexts.Clear();
                    Console.WriteLine($"[TAB_LEARNING] *** {textsToAdd.Count} pending tab metni öğrenme loglarına eklenecek ***");
                }

                // NOT: Pending tab metinlerini öğrenme loglarına ekleme
                // Ana cümle zaten öğrenme loglarına ekleniyor, tab ile kabul edilen metinler o cümlenin parçaları
                // Aynı cümleyi iki kez loglamak gereksiz
                Console.WriteLine($"[TAB_LEARNING] *** {textsToAdd.Count} pending tab metni temizlendi (öğrenme loglarına eklenmedi): [{string.Join(", ", textsToAdd)}] ***");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] AddPendingTabTextsToLearningLog hatası: {ex.Message}");
            }
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
                    _shortcutPreviewWindow.SyncWithMainWindowChanged += OnPreviewSyncWithMainWindowChanged;
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
                OnPropertyChanged(nameof(ShortcutPreviewButtonText));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol önizleme paneli gösterilirken hata: {ex.Message}");
            }
        }

        public void ResetShortcutPreviewPanelPosition()
        {
            try
            {
                Console.WriteLine("[DEBUG] Kısayol önizleme paneli pozisyonu sıfırlanıyor...");

                // Ayarlarda pozisyonu -1 yap (otomatik pozisyon)
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelLeft = -1;
                settings.ShortcutPreviewPanelTop = -1;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();

                // Eğer panel açıksa, kapatıp yeniden aç
                if (_shortcutPreviewWindow != null)
                {
                    Console.WriteLine("[DEBUG] Panel açık, kapatılıp yeniden açılıyor...");
                    HideShortcutPreviewPanel();

                    // Kısa bir bekleme sonrası yeniden aç
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ShowShortcutPreviewPanel();
                        });
                    });
                }

                Console.WriteLine("[DEBUG] Kısayol önizleme paneli pozisyonu sıfırlandı");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Kısayol önizleme paneli pozisyonu sıfırlanırken hata: {ex.Message}");
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
                OnPropertyChanged(nameof(ShortcutPreviewButtonText));
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
                _smartSuggestionsService.TabAcceptedTextLearned -= OnTabAcceptedTextLearned;
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

            // Window focus monitoring event'ini temizle
            WindowHelper.WindowFocusChanged -= OnWindowFocusChanged;
            WindowHelper.StopWindowFocusMonitoring();

            // Preview otomatik kapanma timer'ını temizle
            if (_previewAutoHideTimer != null)
            {
                _previewAutoHideTimer.Stop();
                _previewAutoHideTimer.Dispose();
                _previewAutoHideTimer = null;
            }

            // Tab işleme sistemini temizle
            try
            {
                lock (_tabProcessingLock)
                {
                    _isProcessingTab = false;
                }
                Console.WriteLine("[DEBUG] Tab işleme sistemi temizlendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tab işleme temizleme hatası: {ex.Message}");
            }

            // UI güncelleme timer'ını temizle
            try
            {
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer?.Dispose();
                Console.WriteLine("[DEBUG] UI güncelleme timer'ı temizlendi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UI timer temizleme hatası: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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

        #region Tuş Kombinasyonu Event Handlers

        private void OnEnterPressed(string currentBuffer)
        {
            // Enter tuşu ile cümle tamamlandı - öğrenme sistemine gönder
            try
            {
                Console.WriteLine($"[ENTER_LEARNING] Enter tuşu basıldı, cümle tamamlandı: '{currentBuffer}'");

                if (!string.IsNullOrWhiteSpace(currentBuffer) && currentBuffer.Trim().Length > 5)
                {
                    // Cümleyi öğrenme sistemine gönder
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _smartSuggestionsService.LearnFromTextAsync(currentBuffer.Trim());
                            Console.WriteLine($"[ENTER_LEARNING] Cümle başarıyla öğrenildi: '{currentBuffer.Trim()}'");

                            // Enter ile cümle tamamlandığında pending tab metinlerini de öğrenme loglarına ekle
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddPendingTabTextsToLearningLog();
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Enter öğrenme hatası: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnEnterPressed hatası: {ex.Message}");
            }

            HandleExpansionTrigger(ExpansionTriggerKey.Enter, currentBuffer);
        }

        private void OnShiftSpacePressed(string currentBuffer)
        {
            HandleExpansionTrigger(ExpansionTriggerKey.ShiftSpace, currentBuffer);
        }

        private void OnAltSpacePressed(string currentBuffer)
        {
            HandleExpansionTrigger(ExpansionTriggerKey.AltSpace, currentBuffer);
        }

        private void OnCtrlEnterPressed(string currentBuffer)
        {
            HandleExpansionTrigger(ExpansionTriggerKey.CtrlEnter, currentBuffer);
        }

        private void OnShiftEnterPressed(string currentBuffer)
        {
            HandleExpansionTrigger(ExpansionTriggerKey.ShiftEnter, currentBuffer);
        }

        private void OnAltEnterPressed(string currentBuffer)
        {
            HandleExpansionTrigger(ExpansionTriggerKey.AltEnter, currentBuffer);
        }

        private void HandleExpansionTrigger(ExpansionTriggerKey triggerKey, string currentBuffer)
        {
            Console.WriteLine($"[DEBUG] *** {ExpansionTriggerKeyHelper.GetDescription(triggerKey)} basıldı, buffer: '{currentBuffer}' ***");

            // Sadece seçili tuş kombinasyonu ise expansion yap
            if (_settingsService.Settings.ExpansionTriggerKey != triggerKey)
            {
                Console.WriteLine($"[DEBUG] Bu tuş kombinasyonu aktif değil. Aktif: {ExpansionTriggerKeyHelper.GetDescription(_settingsService.Settings.ExpansionTriggerKey)}");
                return;
            }

            // Aktif pencere bu uygulama ise veya pencere filtrelerine uymuyorsa işlem yapma
            if (!WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled))
            {
                Console.WriteLine("[DEBUG] Pencere filtreleri nedeniyle expansion işlemi atlandı");
                return;
            }

            // Son kelimeyi al ve expansion dene
            var words = currentBuffer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                var lastWord = words.Last().Trim();
                Console.WriteLine($"[DEBUG] Son kelime: '{lastWord}'");

                if (!string.IsNullOrEmpty(lastWord) &&
                    _shortcutService.TryExpandShortcut(lastWord, out string expansion))
                {
                    Console.WriteLine($"[DEBUG] Kısayol genişletildi: '{lastWord}' -> '{expansion}'");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] '{lastWord}' için kısayol bulunamadı");
                }
            }
        }

        #endregion

        #region Ana Pencere Senkronizasyon

        private void OnPreviewSyncWithMainWindowChanged(object? sender, bool isEnabled)
        {
            _isPreviewSyncWithMainWindowEnabled = isEnabled;
            Console.WriteLine($"[DEBUG] Önizleme penceresi senkronizasyon durumu: {isEnabled}");
        }

        private void OnUdfEditorVisibilityChangedHandler(object? sender, bool isVisible)
        {
            OnUdfEditorVisibilityChanged(isVisible);
        }

        private void OnUdfEditorTrackingChanged(object? sender, bool isVisible)
        {
            // UDF editörü tracking servisinden gelen event
            OnUdfEditorVisibilityChanged(isVisible);
        }

        /// <summary>
        /// Pencere odak değişikliği algılandığında çağrılır
        /// </summary>
        private void OnWindowFocusChanged(string newWindowTitle, string newProcessName)
        {
            try
            {
                Console.WriteLine($"[FOCUS] OnWindowFocusChanged: '{newWindowTitle}' (Process: {newProcessName})");

                // Eğer yeni pencere Döküman Editörü veya .UDF içeriyorsa ve önizleme senkronize ise
                if ((newWindowTitle.Contains("Döküman Editörü") || newWindowTitle.Contains(".UDF")) &&
                    _isPreviewSyncWithMainWindowEnabled)
                {
                    Console.WriteLine("[FOCUS] Uygun pencere açıldı, önizleme penceresi yeniden gösteriliyor");

                    // Metin genişletme aktif olmalı
                    bool shouldBeActive = WindowHelper.ShouldTextExpansionBeActive(WindowFilters, IsWindowFilteringEnabled, WindowFilterMode);
                    if (shouldBeActive && !string.IsNullOrEmpty(_contextBuffer))
                    {
                        // UI thread'de çalıştır
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Önizleme penceresini yeniden göster
                            ShowPreview(_contextBuffer);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] OnWindowFocusChanged hatası: {ex.Message}");
            }
        }

        public void OnUdfEditorVisibilityChanged(bool isVisible)
        {
            try
            {
                if (_isPreviewSyncWithMainWindowEnabled && _shortcutPreviewWindow != null)
                {
                    Console.WriteLine($"[DEBUG] UDF editörü görünürlük değişti: {isVisible}, senkronizasyon aktif");

                    if (isVisible)
                    {
                        // UDF editörü gösterildiğinde önizleme de göster (sadece gizliyse)
                        if (!_shortcutPreviewWindow.IsVisible)
                        {
                            _shortcutPreviewWindow.Show();
                            Console.WriteLine("[DEBUG] Önizleme penceresi UDF editörü ile birlikte gösterildi");
                        }
                    }
                    else
                    {
                        // UDF editörü gizlendiğinde önizleme de gizle (sadece görünürse)
                        if (_shortcutPreviewWindow.IsVisible)
                        {
                            _shortcutPreviewWindow.Hide();
                            Console.WriteLine("[DEBUG] Önizleme penceresi UDF editörü ile birlikte gizlendi");
                        }
                    }
                }
                else if (!_isPreviewSyncWithMainWindowEnabled)
                {
                    Console.WriteLine($"[DEBUG] UDF editörü görünürlük değişti: {isVisible}, ama senkronizasyon pasif");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UDF editörü senkronizasyon hatası: {ex.Message}");
            }
        }



        #endregion
    }
}