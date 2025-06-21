using System.Windows;
using System.Windows.Media;
using ModernWpf;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;
using OtomatikMetinGenisletici.Helpers;
using System.Windows.Threading;
using System.Collections.ObjectModel;

namespace OtomatikMetinGenisletici.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private AppSettings _settings;
        private DispatcherTimer? _activeWindowTimer;

        public SettingsWindow(ISettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _settings = _settingsService.GetCopy();

            LoadFontFamilies();
            LoadSettings();
            InitializeWindowFiltering();
            StartActiveWindowTimer();
        }

        private void LoadFontFamilies()
        {
            FontFamilyComboBox.Items.Clear();
            
            var fontFamilies = new[]
            {
                "Segoe UI", "Arial", "Calibri", "Times New Roman", 
                "Tahoma", "Verdana", "Georgia", "Comic Sans MS"
            };

            foreach (var fontFamily in fontFamilies)
            {
                FontFamilyComboBox.Items.Add(fontFamily);
            }
        }

        private void LoadSettings()
        {
            AutoStartCheckBox.IsChecked = _settings.AutoStart;
            NotificationsCheckBox.IsChecked = _settings.ShowNotifications;
            ExpansionDelayTextBox.Text = _settings.ExpansionDelay.ToString();
            FontFamilyComboBox.SelectedItem = _settings.FontFamily;
            FontSizeTextBox.Text = _settings.FontSize.ToString();

            // Smart Suggestions Settings
            SmartSuggestionsEnabledCheckBox.IsChecked = _settings.SmartSuggestionsEnabled;
            LearningEnabledCheckBox.IsChecked = _settings.LearningEnabled;
            PreviewAlwaysVisibleCheckBox.IsChecked = _settings.PreviewAlwaysVisible;
            MaxSmartSuggestionsTextBox.Text = _settings.MaxSmartSuggestions.ToString();
            MinWordLengthTextBox.Text = _settings.MinWordLength.ToString();
            LearningWeightSlider.Value = _settings.LearningWeight * 100;

            MinPhraseLengthTextBox.Text = _settings.MinPhraseLength.ToString();
            MaxPhraseLengthTextBox.Text = _settings.MaxPhraseLength.ToString();
            MinFrequencyTextBox.Text = _settings.MinFrequency.ToString();
            MaxSuggestionsTextBox.Text = _settings.MaxSuggestions.ToString();
            ContextWeightSlider.Value = _settings.ContextWeight * 100;

            // Window Filtering Settings
            WindowFilteringEnabledCheckBox.IsChecked = _settings.WindowFilteringEnabled;

            // Filter Mode Settings
            if (_settings.WindowFilterMode == WindowFilterMode.AllowList)
            {
                AllowListModeRadio.IsChecked = true;
            }
            else
            {
                BlockListModeRadio.IsChecked = true;
            }

            // Tema ayarı kaldırıldı - sadece Light tema kullanılıyor
        }

        private void InitializeWindowFiltering()
        {
            WindowFiltersDataGrid.ItemsSource = _settings.WindowFilters;
        }

        private void StartActiveWindowTimer()
        {
            _activeWindowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _activeWindowTimer.Tick += UpdateActiveWindowInfo;
            _activeWindowTimer.Start();
        }

        private void UpdateActiveWindowInfo(object? sender, EventArgs e)
        {
            try
            {
                var (title, processName, processPath) = WindowHelper.GetActiveWindowDetails();
                ActiveWindowInfoTextBlock.Text = $"Başlık: {title}\nProcess: {processName}";
            }
            catch
            {
                ActiveWindowInfoTextBlock.Text = "Bilgi alınamadı";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate and save settings
                _settings.AutoStart = AutoStartCheckBox.IsChecked ?? false;
                _settings.ShowNotifications = NotificationsCheckBox.IsChecked ?? true;
                if (int.TryParse(ExpansionDelayTextBox.Text, out int expansionDelay))
                    _settings.ExpansionDelay = Math.Max(0, expansionDelay);
                else
                    throw new ArgumentException("Geçersiz genişletme gecikmesi değeri");

                _settings.FontFamily = FontFamilyComboBox.SelectedItem?.ToString() ?? "Segoe UI";

                if (int.TryParse(FontSizeTextBox.Text, out int fontSize))
                    _settings.FontSize = Math.Max(8, Math.Min(24, fontSize));
                else
                    throw new ArgumentException("Geçersiz yazı boyutu değeri (8-24 arası olmalı)");

                // Smart Suggestions Settings
                _settings.SmartSuggestionsEnabled = SmartSuggestionsEnabledCheckBox.IsChecked ?? false;
                _settings.LearningEnabled = LearningEnabledCheckBox.IsChecked ?? true;
                _settings.PreviewAlwaysVisible = PreviewAlwaysVisibleCheckBox.IsChecked ?? true;

                if (int.TryParse(MaxSmartSuggestionsTextBox.Text, out int maxSmartSuggestions))
                    _settings.MaxSmartSuggestions = Math.Max(1, Math.Min(10, maxSmartSuggestions));
                else
                    throw new ArgumentException("Geçersiz maksimum akıllı öneri sayısı (1-10 arası olmalı)");

                if (int.TryParse(MinWordLengthTextBox.Text, out int minWordLength))
                    _settings.MinWordLength = Math.Max(1, Math.Min(10, minWordLength));
                else
                    throw new ArgumentException("Geçersiz minimum kelime uzunluğu (1-10 arası olmalı)");

                _settings.LearningWeight = LearningWeightSlider.Value / 100.0;

                if (int.TryParse(MinPhraseLengthTextBox.Text, out int minPhraseLength))
                    _settings.MinPhraseLength = Math.Max(1, Math.Min(10, minPhraseLength));
                else
                    throw new ArgumentException("Geçersiz minimum kelime sayısı (1-10 arası olmalı)");

                if (int.TryParse(MaxPhraseLengthTextBox.Text, out int maxPhraseLength))
                    _settings.MaxPhraseLength = Math.Max(5, Math.Min(30, maxPhraseLength));
                else
                    throw new ArgumentException("Geçersiz maksimum kelime sayısı (5-30 arası olmalı)");

                if (int.TryParse(MinFrequencyTextBox.Text, out int minFrequency))
                    _settings.MinFrequency = Math.Max(1, Math.Min(10, minFrequency));
                else
                    throw new ArgumentException("Geçersiz minimum kullanım sıklığı (1-10 arası olmalı)");

                if (int.TryParse(MaxSuggestionsTextBox.Text, out int maxSuggestions))
                    _settings.MaxSuggestions = Math.Max(5, Math.Min(50, maxSuggestions));
                else
                    throw new ArgumentException("Geçersiz maksimum öneri sayısı (5-50 arası olmalı)");
                _settings.ContextWeight = ContextWeightSlider.Value / 100.0;

                // Window Filtering Settings
                _settings.WindowFilteringEnabled = WindowFilteringEnabledCheckBox.IsChecked ?? true;
                _settings.WindowFilterMode = AllowListModeRadio.IsChecked == true
                    ? WindowFilterMode.AllowList
                    : WindowFilterMode.BlockList;

                // Validate ranges
                if (_settings.MaxPhraseLength <= _settings.MinPhraseLength)
                {
                    MessageBox.Show("Maksimum kelime sayısı, minimum kelime sayısından büyük olmalı!", 
                        "Geçersiz Değer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Tema ayarı kaldırıldı - sadece Light tema kullanılıyor

                // Save settings
                _settingsService.UpdateSettings(_settings);
                await _settingsService.SaveSettingsAsync();

                MessageBox.Show("Ayarlar başarıyla kaydedildi!", "Başarılı", 
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata oluştu: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void AddFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WindowFilterDialog();
            if (dialog.ShowDialog() == true)
            {
                _settings.WindowFilters.Add(dialog.WindowFilter);
            }
        }

        private void EditFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowFiltersDataGrid.SelectedItem is WindowFilter selectedFilter)
            {
                var dialog = new WindowFilterDialog(selectedFilter);
                if (dialog.ShowDialog() == true)
                {
                    // Değişiklikler otomatik olarak uygulanır (referans tipi)
                    WindowFiltersDataGrid.Items.Refresh();
                }
            }
            else
            {
                MessageBox.Show("Lütfen düzenlemek için bir filtre seçin.", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowFiltersDataGrid.SelectedItem is WindowFilter selectedFilter)
            {
                var result = MessageBox.Show($"'{selectedFilter.Name}' filtresini silmek istediğinizden emin misiniz?",
                    "Filtre Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _settings.WindowFilters.Remove(selectedFilter);
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek için bir filtre seçin.", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddCurrentWindowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (title, processName, processPath) = WindowHelper.GetActiveWindowDetails();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(processName))
                {
                    MessageBox.Show("Aktif pencere bilgisi alınamadı.", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var newFilter = new WindowFilter
                {
                    Name = $"{processName} - {title}",
                    TitlePattern = title,
                    ProcessName = processName,
                    FilterType = WindowFilterType.TitleContains,
                    IsEnabled = true
                };

                _settings.WindowFilters.Add(newFilter);

                MessageBox.Show($"'{newFilter.Name}' filtresi eklendi.", "Başarılı",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filtre eklenirken hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectFromOpenWindowsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentMode = AllowListModeRadio.IsChecked == true
                    ? WindowFilterMode.AllowList
                    : WindowFilterMode.BlockList;

                var dialog = new WindowSelectorDialog(currentMode);
                if (dialog.ShowDialog() == true)
                {
                    // Seçilen filtreleri ekle
                    foreach (var filter in dialog.SelectedFilters)
                    {
                        // Aynı isimde filtre varsa ekleme
                        if (!_settings.WindowFilters.Any(f => f.Name == filter.Name))
                        {
                            _settings.WindowFilters.Add(filter);
                        }
                    }

                    // Filtreleme modunu güncelle
                    if (dialog.SelectedFilterMode == WindowFilterMode.AllowList)
                    {
                        AllowListModeRadio.IsChecked = true;
                    }
                    else
                    {
                        BlockListModeRadio.IsChecked = true;
                    }

                    MessageBox.Show($"{dialog.SelectedFilters.Count} filtre eklendi.", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pencere seçimi sırasında hata oluştu: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _activeWindowTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
