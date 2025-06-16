using System.Windows;
using System.Windows.Media;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Views
{
    public partial class SettingsDialog : Window
    {
        public AppSettings Settings { get; private set; }

        public SettingsDialog()
        {
            InitializeComponent();
            Settings = new AppSettings();
            LoadFontFamilies();
            LoadSettings();
        }

        public SettingsDialog(AppSettings settings) : this()
        {
            Settings = settings;
            LoadSettings();
        }

        private void LoadFontFamilies()
        {
            FontFamilyComboBox.Items.Clear();
            
            var fontFamilies = new[]
            {
                "Arial", "Calibri", "Times New Roman", "Segoe UI", 
                "Tahoma", "Verdana", "Georgia", "Comic Sans MS"
            };

            foreach (var fontFamily in fontFamilies)
            {
                FontFamilyComboBox.Items.Add(fontFamily);
            }
        }

        private void LoadSettings()
        {
            AutoStartCheckBox.IsChecked = Settings.AutoStart;
            NotificationsCheckBox.IsChecked = Settings.ShowNotifications;
            ExpansionDelayTextBox.Text = Settings.ExpansionDelay.ToString();
            FontFamilyComboBox.SelectedItem = Settings.FontFamily;
            FontSizeTextBox.Text = Settings.FontSize.ToString();
            MinPhraseLengthTextBox.Text = Settings.MinPhraseLength.ToString();
            MaxPhraseLengthTextBox.Text = Settings.MaxPhraseLength.ToString();
            MinFrequencyTextBox.Text = Settings.MinFrequency.ToString();
            MaxSuggestionsTextBox.Text = Settings.MaxSuggestions.ToString();
            ContextWeightSlider.Value = Settings.ContextWeight * 100;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.AutoStart = AutoStartCheckBox.IsChecked ?? false;
                Settings.ShowNotifications = NotificationsCheckBox.IsChecked ?? true;
                
                if (int.TryParse(ExpansionDelayTextBox.Text, out int expansionDelay))
                    Settings.ExpansionDelay = Math.Max(0, expansionDelay);
                else
                    throw new ArgumentException("Geçersiz genişletme gecikmesi değeri");

                Settings.FontFamily = FontFamilyComboBox.SelectedItem?.ToString() ?? "Arial";
                
                if (int.TryParse(FontSizeTextBox.Text, out int fontSize))
                    Settings.FontSize = Math.Max(8, Math.Min(24, fontSize));
                else
                    throw new ArgumentException("Geçersiz yazı boyutu değeri (8-24 arası olmalı)");

                if (int.TryParse(MinPhraseLengthTextBox.Text, out int minPhraseLength))
                    Settings.MinPhraseLength = Math.Max(1, Math.Min(10, minPhraseLength));
                else
                    throw new ArgumentException("Geçersiz minimum kelime sayısı (1-10 arası olmalı)");

                if (int.TryParse(MaxPhraseLengthTextBox.Text, out int maxPhraseLength))
                    Settings.MaxPhraseLength = Math.Max(5, Math.Min(30, maxPhraseLength));
                else
                    throw new ArgumentException("Geçersiz maksimum kelime sayısı (5-30 arası olmalı)");

                if (Settings.MaxPhraseLength <= Settings.MinPhraseLength)
                    throw new ArgumentException("Maksimum kelime sayısı, minimum kelime sayısından büyük olmalı");

                if (int.TryParse(MinFrequencyTextBox.Text, out int minFrequency))
                    Settings.MinFrequency = Math.Max(1, Math.Min(10, minFrequency));
                else
                    throw new ArgumentException("Geçersiz minimum kullanım sıklığı (1-10 arası olmalı)");

                if (int.TryParse(MaxSuggestionsTextBox.Text, out int maxSuggestions))
                    Settings.MaxSuggestions = Math.Max(5, Math.Min(50, maxSuggestions));
                else
                    throw new ArgumentException("Geçersiz maksimum öneri sayısı (5-50 arası olmalı)");

                Settings.ContextWeight = ContextWeightSlider.Value / 100.0;

                DialogResult = true;
                Close();
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Geçersiz Değer", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayarlar kaydedilirken hata oluştu: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
