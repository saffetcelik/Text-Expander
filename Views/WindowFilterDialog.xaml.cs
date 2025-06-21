using System.Windows;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Views
{
    public partial class WindowFilterDialog : Window
    {
        public WindowFilter WindowFilter { get; private set; }
        private bool _isEditMode;

        public WindowFilterDialog(WindowFilter? existingFilter = null)
        {
            InitializeComponent();
            
            _isEditMode = existingFilter != null;
            WindowFilter = existingFilter ?? new WindowFilter();
            
            InitializeFilterTypes();
            LoadFilter();
            
            Title = _isEditMode ? "✏️ Pencere Filtresi Düzenle" : "➕ Yeni Pencere Filtresi";
        }

        private void InitializeFilterTypes()
        {
            FilterTypeComboBox.Items.Clear();
            
            var filterTypes = new[]
            {
                new { Value = WindowFilterType.TitleContains, Display = "Başlık İçerir" },
                new { Value = WindowFilterType.TitleEquals, Display = "Başlık Eşittir" },
                new { Value = WindowFilterType.ProcessEquals, Display = "Process Eşittir" },
                new { Value = WindowFilterType.TitleStartsWith, Display = "Başlık İle Başlar" },
                new { Value = WindowFilterType.TitleEndsWith, Display = "Başlık İle Biter" }
            };

            foreach (var filterType in filterTypes)
            {
                FilterTypeComboBox.Items.Add(filterType);
            }
            
            FilterTypeComboBox.DisplayMemberPath = "Display";
            FilterTypeComboBox.SelectedValuePath = "Value";
        }

        private void LoadFilter()
        {
            NameTextBox.Text = WindowFilter.Name;
            TitlePatternTextBox.Text = WindowFilter.TitlePattern;
            ProcessNameTextBox.Text = WindowFilter.ProcessName;
            IsEnabledCheckBox.IsChecked = WindowFilter.IsEnabled;
            IsRegexCheckBox.IsChecked = WindowFilter.IsRegex;
            
            // Filtre türünü seç
            foreach (var item in FilterTypeComboBox.Items)
            {
                if (item is { } obj && obj.GetType().GetProperty("Value")?.GetValue(obj) is WindowFilterType filterType)
                {
                    if (filterType == WindowFilter.FilterType)
                    {
                        FilterTypeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            
            UpdateFieldVisibility();
        }

        private void FilterTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateFieldVisibility();
        }

        private void UpdateFieldVisibility()
        {
            if (FilterTypeComboBox.SelectedItem is { } selectedItem)
            {
                var filterType = (WindowFilterType)selectedItem.GetType().GetProperty("Value")!.GetValue(selectedItem)!;
                
                // Process türü seçiliyse process alanını vurgula, diğer durumlarda başlık alanını
                if (filterType == WindowFilterType.ProcessEquals)
                {
                    ProcessNameTextBox.Background = System.Windows.Media.Brushes.LightYellow;
                    TitlePatternTextBox.Background = System.Windows.Media.Brushes.White;
                }
                else
                {
                    TitlePatternTextBox.Background = System.Windows.Media.Brushes.LightYellow;
                    ProcessNameTextBox.Background = System.Windows.Media.Brushes.White;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validasyon
                if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    MessageBox.Show("Lütfen filtre adını girin.", "Uyarı", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    NameTextBox.Focus();
                    return;
                }

                if (FilterTypeComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Lütfen filtre türünü seçin.", "Uyarı", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    FilterTypeComboBox.Focus();
                    return;
                }

                var selectedFilterType = (WindowFilterType)FilterTypeComboBox.SelectedItem.GetType()
                    .GetProperty("Value")!.GetValue(FilterTypeComboBox.SelectedItem)!;

                // Filtre türüne göre gerekli alanları kontrol et
                if (selectedFilterType == WindowFilterType.ProcessEquals)
                {
                    if (string.IsNullOrWhiteSpace(ProcessNameTextBox.Text))
                    {
                        MessageBox.Show("Process türü için process adını girin.", "Uyarı", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        ProcessNameTextBox.Focus();
                        return;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(TitlePatternTextBox.Text))
                    {
                        MessageBox.Show("Başlık türleri için başlık desenini girin.", "Uyarı", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        TitlePatternTextBox.Focus();
                        return;
                    }
                }

                // Regex validasyonu
                if (IsRegexCheckBox.IsChecked == true)
                {
                    try
                    {
                        var pattern = selectedFilterType == WindowFilterType.ProcessEquals 
                            ? ProcessNameTextBox.Text 
                            : TitlePatternTextBox.Text;
                        
                        System.Text.RegularExpressions.Regex.IsMatch("test", pattern);
                    }
                    catch
                    {
                        MessageBox.Show("Geçersiz regex deseni. Lütfen geçerli bir regex ifadesi girin.", "Uyarı", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Değerleri kaydet
                WindowFilter.Name = NameTextBox.Text.Trim();
                WindowFilter.TitlePattern = TitlePatternTextBox.Text.Trim();
                WindowFilter.ProcessName = ProcessNameTextBox.Text.Trim();
                WindowFilter.FilterType = selectedFilterType;
                WindowFilter.IsEnabled = IsEnabledCheckBox.IsChecked ?? true;
                WindowFilter.IsRegex = IsRegexCheckBox.IsChecked ?? false;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filtre kaydedilirken hata oluştu: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
