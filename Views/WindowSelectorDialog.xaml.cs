using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Helpers;

namespace OtomatikMetinGenisletici.Views
{
    public partial class WindowSelectorDialog : Window
    {
        public ObservableCollection<OpenWindow> OpenWindows { get; private set; }
        public List<WindowFilter> SelectedFilters { get; private set; }
        public WindowFilterMode SelectedFilterMode { get; private set; }

        public WindowSelectorDialog(WindowFilterMode currentMode = WindowFilterMode.AllowList)
        {
            InitializeComponent();
            
            OpenWindows = new ObservableCollection<OpenWindow>();
            SelectedFilters = new List<WindowFilter>();
            
            WindowsDataGrid.ItemsSource = OpenWindows;
            
            // Mevcut modu ayarla
            if (currentMode == WindowFilterMode.AllowList)
            {
                AllowListRadio.IsChecked = true;
            }
            else
            {
                BlockListRadio.IsChecked = true;
            }
            
            UpdateModeDescription();
            LoadOpenWindows();
            
            // Event handlers
            AllowListRadio.Checked += ModeRadio_Changed;
            BlockListRadio.Checked += ModeRadio_Changed;
        }

        private void ModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateModeDescription();
        }

        private void UpdateModeDescription()
        {
            if (AllowListRadio.IsChecked == true)
            {
                ModeDescriptionText.Text = "İzin Listesi: Program sadece seçtiğiniz pencerelerde çalışır. Diğer tüm pencereler engellenir.";
                SelectedFilterMode = WindowFilterMode.AllowList;
            }
            else
            {
                ModeDescriptionText.Text = "Engel Listesi: Program seçtiğiniz pencereler hariç her yerde çalışır. Seçtiğiniz pencereler engellenir.";
                SelectedFilterMode = WindowFilterMode.BlockList;
            }
        }

        private void LoadOpenWindows()
        {
            try
            {
                OpenWindows.Clear();
                var windows = WindowHelper.GetOpenWindows();
                
                foreach (var window in windows)
                {
                    window.PropertyChanged += Window_PropertyChanged;
                    OpenWindows.Add(window);
                }
                
                UpdateSelectionCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Açık pencereler yüklenirken hata oluştu: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OpenWindow.IsSelected))
            {
                UpdateSelectionCount();
            }
        }

        private void UpdateSelectionCount()
        {
            try
            {
                var selectedCount = OpenWindows.Count(w => w.IsSelected);
                SelectionCountText.Text = $"{selectedCount} pencere seçildi";

                AddSelectedButton.IsEnabled = selectedCount > 0;

                // Debug için
                System.Diagnostics.Debug.WriteLine($"[WindowSelector] UpdateSelectionCount: {selectedCount} pencere seçildi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowSelector] UpdateSelectionCount hatası: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadOpenWindows();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var window in OpenWindows)
            {
                window.IsSelected = true;
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var window in OpenWindows)
            {
                window.IsSelected = false;
            }
        }

        private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedWindows = OpenWindows.Where(w => w.IsSelected).ToList();
                
                if (!selectedWindows.Any())
                {
                    MessageBox.Show("Lütfen en az bir pencere seçin.", "Uyarı", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedFilters.Clear();
                
                foreach (var window in selectedWindows)
                {
                    // Her pencere için hem başlık hem de process filtresi oluştur
                    // Önce process filtresi (daha genel)
                    var processFilter = new WindowFilter
                    {
                        Name = $"{window.ProcessName} (Process)",
                        ProcessName = window.ProcessName,
                        FilterType = WindowFilterType.ProcessEquals,
                        IsEnabled = true
                    };
                    
                    // Sonra başlık filtresi (daha spesifik)
                    var titleFilter = new WindowFilter
                    {
                        Name = $"{window.ProcessName} - {window.Title}",
                        TitlePattern = window.Title,
                        FilterType = WindowFilterType.TitleEquals,
                        IsEnabled = true
                    };
                    
                    SelectedFilters.Add(processFilter);
                    SelectedFilters.Add(titleFilter);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Filtreler oluşturulurken hata oluştu: {ex.Message}", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void WindowsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Checkbox değişikliği sonrası sayımı güncelle
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateSelectionCount();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
