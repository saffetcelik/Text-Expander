using System.IO;
using Newtonsoft.Json;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private AppSettings _settings = new();

        public AppSettings Settings => _settings;

        public event Action<AppSettings>? SettingsChanged;

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var json = await File.ReadAllTextAsync(SettingsFileName);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        _settings = settings;

                        // Migration: If ExpansionTriggerKey was Tab (old value 2), change it to CtrlSpace (default)
                        // Since we removed Tab from the enum, old value 2 would now be CtrlSpace
                        // We need to check if the loaded value makes sense
                        if (!Enum.IsDefined(typeof(ExpansionTriggerKey), _settings.ExpansionTriggerKey))
                        {
                            Console.WriteLine($"[DEBUG] Invalid ExpansionTriggerKey value {_settings.ExpansionTriggerKey}, resetting to CtrlSpace");
                            _settings.ExpansionTriggerKey = ExpansionTriggerKey.CtrlSpace;
                        }

                        Console.WriteLine("Ayarlar başarıyla yüklendi.");
                        Console.WriteLine($"[DEBUG] SmartSuggestionsEnabled yüklendi: {_settings.SmartSuggestionsEnabled}");
                        Console.WriteLine($"[DEBUG] ExpansionTriggerKey yüklendi: {_settings.ExpansionTriggerKey}");
                    }
                }
            }
            catch (Exception)
            {
                // Hata durumunda varsayılan ayarları kullan
                _settings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                await File.WriteAllTextAsync(SettingsFileName, json);
                SettingsChanged?.Invoke(_settings);
            }
            catch (Exception)
            {
                // Kaydetme hatalarını sessizce yoksay
            }
        }

        public void UpdateSettings(AppSettings newSettings)
        {
            _settings.ShowNotifications = newSettings.ShowNotifications;
            _settings.ExpansionDelay = newSettings.ExpansionDelay;
            _settings.FontFamily = newSettings.FontFamily;
            _settings.FontSize = newSettings.FontSize;
            _settings.MinPhraseLength = newSettings.MinPhraseLength;
            _settings.MaxPhraseLength = newSettings.MaxPhraseLength;
            _settings.MinFrequency = newSettings.MinFrequency;
            _settings.MaxSuggestions = newSettings.MaxSuggestions;
            _settings.ContextWeight = newSettings.ContextWeight;

            // Akıllı Öneriler Ayarları
            _settings.SmartSuggestionsEnabled = newSettings.SmartSuggestionsEnabled;
            _settings.MaxSmartSuggestions = newSettings.MaxSmartSuggestions;
            _settings.MinWordLength = newSettings.MinWordLength;
            _settings.LearningEnabled = newSettings.LearningEnabled;
            _settings.LearningWeight = newSettings.LearningWeight;

            // Tuş Yönetimi Ayarları
            _settings.ExpansionTriggerKey = newSettings.ExpansionTriggerKey;

            // Kısayol Önizleme Paneli Ayarları
            _settings.ShortcutPreviewPanelVisible = newSettings.ShortcutPreviewPanelVisible;
            _settings.ShortcutPreviewPanelOpacity = newSettings.ShortcutPreviewPanelOpacity;
            _settings.ShortcutPreviewPanelWidth = newSettings.ShortcutPreviewPanelWidth;
            _settings.ShortcutPreviewPanelHeight = newSettings.ShortcutPreviewPanelHeight;
            _settings.ShortcutPreviewPanelLeft = newSettings.ShortcutPreviewPanelLeft;
            _settings.ShortcutPreviewPanelTop = newSettings.ShortcutPreviewPanelTop;

            // Pencere Filtreleme Ayarları
            _settings.WindowFilteringEnabled = newSettings.WindowFilteringEnabled;
            _settings.WindowFilterMode = newSettings.WindowFilterMode;
            _settings.WindowFilters.Clear();
            foreach (var filter in newSettings.WindowFilters)
            {
                _settings.WindowFilters.Add(filter);
            }
        }

        public AppSettings GetCopy()
        {
            return new AppSettings
            {
                ShowNotifications = _settings.ShowNotifications,
                ExpansionDelay = _settings.ExpansionDelay,
                FontFamily = _settings.FontFamily,
                FontSize = _settings.FontSize,
                MinPhraseLength = _settings.MinPhraseLength,
                MaxPhraseLength = _settings.MaxPhraseLength,
                MinFrequency = _settings.MinFrequency,
                MaxSuggestions = _settings.MaxSuggestions,
                ContextWeight = _settings.ContextWeight,

                // Akıllı Öneriler Ayarları
                SmartSuggestionsEnabled = _settings.SmartSuggestionsEnabled,
                MaxSmartSuggestions = _settings.MaxSmartSuggestions,
                MinWordLength = _settings.MinWordLength,
                LearningEnabled = _settings.LearningEnabled,
                LearningWeight = _settings.LearningWeight,

                // Tuş Yönetimi Ayarları
                ExpansionTriggerKey = _settings.ExpansionTriggerKey,

                // Kısayol Önizleme Paneli Ayarları
                ShortcutPreviewPanelVisible = _settings.ShortcutPreviewPanelVisible,
                ShortcutPreviewPanelOpacity = _settings.ShortcutPreviewPanelOpacity,
                ShortcutPreviewPanelWidth = _settings.ShortcutPreviewPanelWidth,
                ShortcutPreviewPanelHeight = _settings.ShortcutPreviewPanelHeight,
                ShortcutPreviewPanelLeft = _settings.ShortcutPreviewPanelLeft,
                ShortcutPreviewPanelTop = _settings.ShortcutPreviewPanelTop,

                // Pencere Filtreleme Ayarları
                WindowFilteringEnabled = _settings.WindowFilteringEnabled,
                WindowFilterMode = _settings.WindowFilterMode,
                WindowFilters = new System.Collections.ObjectModel.ObservableCollection<Models.WindowFilter>(_settings.WindowFilters)
            };
        }
    }
}
