using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutPreviewPanel : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<Shortcut> _shortcuts = new();
        private ObservableCollection<Shortcut> _filteredShortcuts = new();
        private string _searchText = string.Empty;
        private double _panelOpacity = 0.9;
        private bool _isMinimized = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CloseRequested;
        public event EventHandler<double>? OpacityChanged;
        public event EventHandler<bool>? MinimizeRequested;

        public ShortcutPreviewPanel()
        {
            InitializeComponent();
            DataContext = this;
            FilterShortcuts();
        }

        public ObservableCollection<Shortcut> Shortcuts
        {
            get => _shortcuts;
            set
            {
                _shortcuts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShortcutCount));
                FilterShortcuts();
            }
        }

        public ObservableCollection<Shortcut> FilteredShortcuts
        {
            get => _filteredShortcuts;
            private set
            {
                _filteredShortcuts = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterShortcuts();
            }
        }

        public double PanelOpacity
        {
            get => _panelOpacity;
            set
            {
                _panelOpacity = Math.Max(0.3, Math.Min(1.0, value));
                OnPropertyChanged();
                OpacityChanged?.Invoke(this, _panelOpacity);
            }
        }

        public int ShortcutCount => _shortcuts?.Count ?? 0;

        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                _isMinimized = value;
                OnPropertyChanged();
                UpdateMinimizeState();
            }
        }

        private void FilterShortcuts()
        {
            if (_shortcuts == null)
            {
                FilteredShortcuts = new ObservableCollection<Shortcut>();
                return;
            }

            var filtered = _shortcuts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var searchLower = _searchText.ToLowerInvariant();
                filtered = filtered.Where(s => 
                    s.Key.ToLowerInvariant().Contains(searchLower) ||
                    s.Expansion.ToLowerInvariant().Contains(searchLower));
            }

            // En çok kullanılan kısayolları üstte göster
            filtered = filtered.OrderByDescending(s => s.UsageCount)
                              .ThenBy(s => s.Key);

            FilteredShortcuts = new ObservableCollection<Shortcut>(filtered);
        }

        private void ShortcutItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is Shortcut shortcut)
            {
                // Tooltip zaten XAML'de tanımlı, burada ek işlem yapabiliriz
                border.Background = System.Windows.Media.Brushes.LightBlue;
            }
        }

        private void ShortcutItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.ClearValue(Border.BackgroundProperty);
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PanelOpacity = e.NewValue;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            IsMinimized = !IsMinimized;
            MinimizeRequested?.Invoke(this, IsMinimized);
        }

        private void UpdateMinimizeState()
        {
            // Minimize butonunun içeriğini güncelle
            if (MinimizeButton != null)
            {
                MinimizeButton.Content = IsMinimized ? "□" : "−";
                MinimizeButton.ToolTip = IsMinimized ? "Büyüt" : "Küçült";
            }

            // UI elementlerinin görünürlüğünü kontrol et
            var visibility = IsMinimized ? Visibility.Collapsed : Visibility.Visible;

            if (SearchBox != null)
                SearchBox.Visibility = visibility;

            if (ShortcutsScrollViewer != null)
                ShortcutsScrollViewer.Visibility = visibility;

            if (FooterPanel != null)
                FooterPanel.Visibility = visibility;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
