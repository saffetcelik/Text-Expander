using OtomatikMetinGenisletici.Models;
using System.Text.Json;
using System.IO;

namespace OtomatikMetinGenisletici.Services
{
    public class SmartSuggestionsService : ISmartSuggestionsService, IDisposable
    {
        private readonly TextLearningEngine _learningEngine;
        private readonly ISettingsService _settingsService;
        private bool _isEnabled;
        private AppSettings _settings;

        // Basit cache mekanizması
        private readonly Dictionary<string, (List<SmartSuggestion> suggestions, DateTime timestamp)> _suggestionCache = new();
        private readonly object _cacheLock = new object();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5); // 5 saniye cache

        // Recent suggestion paste tracking - yapıştırılan önerileri takip et
        private readonly Dictionary<string, DateTime> _recentSuggestionPastes = new();
        private readonly object _pasteLock = new object();
        private readonly TimeSpan _pasteTrackingDuration = TimeSpan.FromSeconds(3); // 3 saniye takip

        // Tab ile kabul edilen öneriler için tracking
        private readonly Dictionary<string, DateTime> _recentTabAcceptedSuggestions = new();
        private readonly object _tabAcceptLock = new object();
        private readonly TimeSpan _tabAcceptTrackingDuration = TimeSpan.FromSeconds(5); // 5 saniye takip

        public bool IsEnabled => _isEnabled;

        public event Action<List<SmartSuggestion>>? SuggestionsUpdated;
        public event Action<SmartSuggestion>? SuggestionAccepted;
        public event Action<string>? TabAcceptedTextLearned;

        public SmartSuggestionsService(ISettingsService settingsService, ShortcutService? shortcutService = null)
        {
            try
            {
                Console.WriteLine("[DEBUG] SmartSuggestionsService constructor başlıyor...");

                _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
                _settings = _settingsService.Settings;

                Console.WriteLine("[DEBUG] TextLearningEngine oluşturuluyor...");
                _learningEngine = new TextLearningEngine("smart_suggestions_data.json", shortcutService);
                _isEnabled = _settings.SmartSuggestionsEnabled;

                Console.WriteLine("[DEBUG] Event'ler bağlanıyor...");
                // Ayar değişikliklerini dinle
                _settingsService.SettingsChanged += OnSettingsChanged;

                Console.WriteLine("[DEBUG] SmartSuggestionsService constructor tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SmartSuggestionsService constructor hatası: {ex}");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("[SMART SUGGESTIONS] InitializeAsync başlıyor...");

                // TextLearningEngine'in verilerinin yüklendiğinden emin ol
                // Engine constructor'da zaten LoadLearningData() çağrılıyor ama
                // async olmadığı için burada kontrol edelim

                // İstatistikleri al ve logla
                var stats = await GetStatisticsAsync();
                Console.WriteLine($"[SMART SUGGESTIONS] Başlangıç istatistikleri:");
                Console.WriteLine($"[SMART SUGGESTIONS] - Toplam kelime: {stats.TotalUniqueWords}");
                Console.WriteLine($"[SMART SUGGESTIONS] - Toplam bigram: {stats.TotalBigrams}");
                Console.WriteLine($"[SMART SUGGESTIONS] - Toplam trigram: {stats.TotalTrigrams}");
                Console.WriteLine($"[SMART SUGGESTIONS] - Son güncelleme: {stats.LastLearningSession}");

                // Yakın zamanda yapıştırılmış öneri kontrolü için callback set et
                _learningEngine.SetRecentlyPastedSuggestionCheck(IsRecentlyPastedSuggestion);
                Console.WriteLine("[SMART SUGGESTIONS] Yapıştırılmış öneri kontrolü callback'i ayarlandı");

                // Tab ile kabul edilen öneri kontrolü için callback set et
                _learningEngine.SetRecentlyTabAcceptedSuggestionCheck(IsRecentlyTabAcceptedSuggestion);
                Console.WriteLine("[SMART SUGGESTIONS] Tab ile kabul edilen öneri kontrolü callback'i ayarlandı");

                Console.WriteLine("[SMART SUGGESTIONS] InitializeAsync tamamlandı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SmartSuggestionsService InitializeAsync hatası: {ex.Message}");
            }
        }

        public async Task<List<SmartSuggestion>> GetSuggestionsAsync(string context, int maxSuggestions = 5)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(context))
                return new List<SmartSuggestion>();

            // Cache kontrolü
            var cacheKey = $"{context}_{maxSuggestions}";
            lock (_cacheLock)
            {
                if (_suggestionCache.TryGetValue(cacheKey, out var cached))
                {
                    if (DateTime.Now - cached.timestamp < _cacheExpiry)
                    {
                        Console.WriteLine($"[CACHE] Cache'den öneri döndürülüyor: {context}");
                        return new List<SmartSuggestion>(cached.suggestions);
                    }
                    else
                    {
                        // Eski cache'i temizle
                        _suggestionCache.Remove(cacheKey);
                    }
                }
            }

            try
            {
                var suggestions = await _learningEngine.GetSuggestionsAsync(context, maxSuggestions);

                // Minimum kelime uzunluğu filtresi
                suggestions = suggestions
                    .Where(s => s.Text.Length >= _settings.MinWordLength)
                    .ToList();

                // Cache'e kaydet
                lock (_cacheLock)
                {
                    _suggestionCache[cacheKey] = (new List<SmartSuggestion>(suggestions), DateTime.Now);

                    // Cache temizliği - 200'den fazla entry varsa eski olanları temizle (performans için artırıldı)
                    if (_suggestionCache.Count > 200)
                    {
                        var oldEntries = _suggestionCache
                            .Where(kvp => DateTime.Now - kvp.Value.timestamp > _cacheExpiry)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var oldKey in oldEntries)
                        {
                            _suggestionCache.Remove(oldKey);
                        }
                    }
                }

                SuggestionsUpdated?.Invoke(suggestions);
                return suggestions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öneri alma hatası: {ex.Message}");
                return new List<SmartSuggestion>();
            }
        }

        public async Task LearnFromTextAsync(string text)
        {
            Console.WriteLine($"[SMART SUGGESTIONS] LearnFromTextAsync çağrıldı: '{text}'");
            Console.WriteLine($"[SMART SUGGESTIONS] _isEnabled: {_isEnabled}");
            Console.WriteLine($"[SMART SUGGESTIONS] _settings.LearningEnabled: {_settings.LearningEnabled}");
            Console.WriteLine($"[SMART SUGGESTIONS] text boş mu: {string.IsNullOrWhiteSpace(text)}");

            if (!_isEnabled || !_settings.LearningEnabled || string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"[SMART SUGGESTIONS] Öğrenme atlandı - Enabled: {_isEnabled}, LearningEnabled: {_settings.LearningEnabled}, Text: '{text}'");
                return;
            }

            try
            {
                Console.WriteLine($"[SMART SUGGESTIONS] TextLearningEngine'e öğrenme gönderiliyor...");
                await _learningEngine.LearnFromTextAsync(text);
                Console.WriteLine($"[SMART SUGGESTIONS] Öğrenme başarılı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMART SUGGESTIONS] Öğrenme hatası: {ex.Message}");
            }
        }

        public async Task AcceptSuggestionAsync(SmartSuggestion suggestion, string context)
        {
            if (!_isEnabled)
                return;

            try
            {
                // Tab ile kabul edilen öneriyi işaretle
                MarkSuggestionAsTabAccepted(suggestion.Text);

                await _learningEngine.AcceptSuggestionAsync(suggestion, context);
                SuggestionAccepted?.Invoke(suggestion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öneri kabul etme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Tab ile kabul edilen metinler için özel öğrenme metodu
        /// Paste tracking sistemini tamamen bypass eder
        /// </summary>
        public async Task LearnFromTabAcceptedTextAsync(string text)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Console.WriteLine($"[TAB_LEARNING] *** Tab özel öğrenme başlıyor: '{text}' ***");

                // Tab metni için özel işaretleme - paste kontrolünden muaf
                lock (_tabAcceptLock)
                {
                    var normalizedText = text.Trim().ToLowerInvariant();
                    _recentTabAcceptedSuggestions[normalizedText] = DateTime.Now;
                    Console.WriteLine($"[TAB_LEARNING] Tab metni özel olarak işaretlendi: '{text}'");
                }

                // Direkt öğrenme sistemine gönder
                await _learningEngine.LearnFromTextAsync(text);
                Console.WriteLine($"[TAB_LEARNING] *** Tab metni başarıyla öğrenildi: '{text}' ***");

                // Event'i tetikle - MainViewModel pending listesine ekleyecek
                // Pending metinler sadece cümle tamamlandığında (nokta veya Enter ile) öğrenme loglarına eklenecek
                TabAcceptedTextLearned?.Invoke(text);
                Console.WriteLine($"[TAB_LEARNING] *** TabAcceptedTextLearned event tetiklendi (pending): '{text}' ***");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tab özel öğrenme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Tab ile kabul edilen öneriyi işaretler
        /// </summary>
        public void MarkSuggestionAsTabAccepted(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            lock (_tabAcceptLock)
            {
                var normalizedText = text.Trim().ToLowerInvariant();
                _recentTabAcceptedSuggestions[normalizedText] = DateTime.Now;
                Console.WriteLine($"[TAB_ACCEPT] *** Öneri tab ile kabul edildi olarak işaretlendi: '{text}' ***");
                Console.WriteLine($"[TAB_ACCEPT] Toplam tab kabul edilen öneri sayısı: {_recentTabAcceptedSuggestions.Count}");

                // Eski kayıtları temizle
                var expiredKeys = _recentTabAcceptedSuggestions
                    .Where(kvp => DateTime.Now - kvp.Value > _tabAcceptTrackingDuration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _recentTabAcceptedSuggestions.Remove(key);
                    Console.WriteLine($"[TAB_ACCEPT] Eski tab kabul kaydı temizlendi: '{key}'");
                }
            }
        }

        /// <summary>
        /// Bir metnin tab ile kabul edilmiş bir öneri olup olmadığını kontrol eder
        /// </summary>
        public bool IsRecentlyTabAcceptedSuggestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            lock (_tabAcceptLock)
            {
                var normalizedText = text.Trim().ToLowerInvariant();
                Console.WriteLine($"[TAB_ACCEPT] Tab kabul kontrolü yapılıyor: '{text}' (normalized: '{normalizedText}')");

                if (_recentTabAcceptedSuggestions.TryGetValue(normalizedText, out var acceptTime))
                {
                    var timeSinceAccept = DateTime.Now - acceptTime;
                    if (timeSinceAccept <= _tabAcceptTrackingDuration)
                    {
                        Console.WriteLine($"[TAB_ACCEPT] *** Tab ile kabul edilmiş öneri algılandı: '{text}' (geçen süre: {timeSinceAccept.TotalSeconds:F1}s) ***");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[TAB_ACCEPT] Tab kabul süresi dolmuş: '{text}' (geçen süre: {timeSinceAccept.TotalSeconds:F1}s)");
                    }
                }
                else
                {
                    Console.WriteLine($"[TAB_ACCEPT] Tab kabul kaydı bulunamadı: '{text}'");
                    Console.WriteLine($"[TAB_ACCEPT] Mevcut tab kabul kayıtları: {string.Join(", ", _recentTabAcceptedSuggestions.Keys)}");
                }

                return false;
            }
        }

        public async Task RejectSuggestionAsync(SmartSuggestion suggestion, string context)
        {
            if (!_isEnabled)
                return;

            try
            {
                await _learningEngine.RejectSuggestionAsync(suggestion, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öneri reddetme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Bir öneri metninin yakın zamanda yapıştırıldığını kaydet
        /// </summary>
        public void MarkSuggestionAsPasted(string suggestionText)
        {
            if (string.IsNullOrWhiteSpace(suggestionText))
                return;

            lock (_pasteLock)
            {
                var normalizedText = suggestionText.Trim().ToLowerInvariant();
                _recentSuggestionPastes[normalizedText] = DateTime.Now;
                Console.WriteLine($"[SUGGESTION_PASTE] Öneri yapıştırıldı olarak işaretlendi: '{suggestionText}'");

                // Eski kayıtları temizle
                CleanupOldPasteRecords();
            }
        }

        /// <summary>
        /// Bir metnin yakın zamanda yapıştırılmış bir öneri olup olmadığını kontrol et
        /// NOT: Tab ile kabul edilen öneriler artık typing ile yazıldığı için bu kontrolden muaf
        /// </summary>
        public bool IsRecentlyPastedSuggestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            lock (_pasteLock)
            {
                var normalizedText = text.Trim().ToLowerInvariant();

                if (_recentSuggestionPastes.TryGetValue(normalizedText, out var pasteTime))
                {
                    var timeSincePaste = DateTime.Now - pasteTime;
                    if (timeSincePaste <= _pasteTrackingDuration)
                    {
                        Console.WriteLine($"[SUGGESTION_PASTE] Yakın zamanda yapıştırılmış öneri algılandı: '{text}' (geçen süre: {timeSincePaste.TotalSeconds:F1}s)");
                        Console.WriteLine($"[SUGGESTION_PASTE] NOT: Tab ile kabul edilen öneriler artık typing ile yazıldığı için öğrenme sistemine girecek");
                        return true;
                    }
                }

                return false;
            }
        }

        private void CleanupOldPasteRecords()
        {
            var cutoffTime = DateTime.Now - _pasteTrackingDuration;
            var keysToRemove = _recentSuggestionPastes
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _recentSuggestionPastes.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                Console.WriteLine($"[SUGGESTION_PASTE] {keysToRemove.Count} eski yapıştırma kaydı temizlendi");
            }
        }

        public async Task<LearningStatistics> GetStatisticsAsync()
        {
            return await Task.Run(() => _learningEngine.GetStatistics());
        }

        public async Task<DetailedStatistics> GetLearningDataAsync()
        {
            return await _learningEngine.GetDetailedStatisticsAsync();
        }

        public async Task ExportLearningDataAsync(string filePath)
        {
            try
            {
                var statistics = await GetStatisticsAsync();
                var exportData = new
                {
                    ExportDate = DateTime.Now,
                    Statistics = statistics,
                    Settings = new
                    {
                        _settings.SmartSuggestionsEnabled,
                        _settings.MaxSmartSuggestions,
                        _settings.MinWordLength,
                        _settings.LearningEnabled,
                        _settings.LearningWeight
                    }
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Veri dışa aktarma hatası: {ex.Message}", ex);
            }
        }

        public async Task ImportLearningDataAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("İçe aktarılacak dosya bulunamadı.");

                // Mevcut verileri yedekle
                var backupPath = $"smart_suggestions_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                await ExportLearningDataAsync(backupPath);

                // Yeni verileri yükle (bu kısım daha detaylı implementasyon gerektirir)
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Veri içe aktarma hatası: {ex.Message}", ex);
            }
        }

        public async Task ResetLearningDataAsync()
        {
            try
            {
                await _learningEngine.ResetLearningDataAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Veri sıfırlama hatası: {ex.Message}", ex);
            }
        }

        public void Enable()
        {
            _isEnabled = true;
        }

        public void Disable()
        {
            _isEnabled = false;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
            _isEnabled = settings.SmartSuggestionsEnabled;
        }

        private void OnSettingsChanged(AppSettings settings)
        {
            UpdateSettings(settings);
        }

        public void Dispose()
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _learningEngine?.Dispose();

            // Cache'i temizle
            lock (_cacheLock)
            {
                _suggestionCache.Clear();
            }
        }

        // Veri Yönetimi Fonksiyonları
        public async Task<bool> UpdateWordAsync(string oldWord, string newWord, int newCount)
        {
            return await Task.Run(() => _learningEngine.UpdateWordFrequency(oldWord, newWord, newCount));
        }

        public async Task<bool> DeleteWordAsync(string word)
        {
            return await Task.Run(() => _learningEngine.DeleteWord(word));
        }

        public async Task<bool> UpdateBigramAsync(string oldBigram, string newBigram, int newCount)
        {
            return await Task.Run(() => _learningEngine.UpdateBigram(oldBigram, newBigram, newCount));
        }

        public async Task<bool> DeleteBigramAsync(string bigram)
        {
            return await Task.Run(() => _learningEngine.DeleteBigram(bigram));
        }

        public async Task<bool> UpdateTrigramAsync(string oldTrigram, string newTrigram, int newCount)
        {
            return await Task.Run(() => _learningEngine.UpdateTrigram(oldTrigram, newTrigram, newCount));
        }

        public async Task<bool> DeleteTrigramAsync(string trigram)
        {
            return await Task.Run(() => _learningEngine.DeleteTrigram(trigram));
        }

        public async Task<List<(string Word, int Count)>> SearchWordsAsync(string searchTerm, int maxResults = 50)
        {
            return await Task.Run(() => _learningEngine.SearchWords(searchTerm, maxResults));
        }

        public async Task<List<(string Bigram, int Count)>> SearchBigramsAsync(string searchTerm, int maxResults = 50)
        {
            return await Task.Run(() => _learningEngine.SearchBigrams(searchTerm, maxResults));
        }

        public async Task<List<(string Trigram, int Count)>> SearchTrigramsAsync(string searchTerm, int maxResults = 50)
        {
            return await Task.Run(() => _learningEngine.SearchTrigrams(searchTerm, maxResults));
        }

        public async Task SaveDataAsync()
        {
            await Task.Run(() => _learningEngine.SaveLearningData());
        }
    }
}
