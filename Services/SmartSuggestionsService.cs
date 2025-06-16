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

        public bool IsEnabled => _isEnabled;

        public event Action<List<SmartSuggestion>>? SuggestionsUpdated;
        public event Action<SmartSuggestion>? SuggestionAccepted;

        public SmartSuggestionsService(ISettingsService settingsService)
        {
            try
            {
                Console.WriteLine("[DEBUG] SmartSuggestionsService constructor başlıyor...");

                _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
                _settings = _settingsService.Settings;

                Console.WriteLine("[DEBUG] TextLearningEngine oluşturuluyor...");
                _learningEngine = new TextLearningEngine("smart_suggestions_data.json");
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
            // Başlangıç verilerini yükle
            await Task.CompletedTask;
        }

        public async Task<List<SmartSuggestion>> GetSuggestionsAsync(string context, int maxSuggestions = 5)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(context))
                return new List<SmartSuggestion>();

            try
            {
                var suggestions = await _learningEngine.GetSuggestionsAsync(context, maxSuggestions);
                
                // Minimum kelime uzunluğu filtresi
                suggestions = suggestions
                    .Where(s => s.Text.Length >= _settings.MinWordLength)
                    .ToList();

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
            if (!_isEnabled || !_settings.LearningEnabled || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                await _learningEngine.LearnFromTextAsync(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öğrenme hatası: {ex.Message}");
            }
        }

        public async Task AcceptSuggestionAsync(SmartSuggestion suggestion, string context)
        {
            if (!_isEnabled)
                return;

            try
            {
                await _learningEngine.AcceptSuggestionAsync(suggestion, context);
                SuggestionAccepted?.Invoke(suggestion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Öneri kabul etme hatası: {ex.Message}");
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
        }
    }
}
