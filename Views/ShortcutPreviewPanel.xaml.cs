using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutPreviewPanel : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<Shortcut> _shortcuts = new();
        private ObservableCollection<Shortcut> _filteredShortcuts = new();
        private string _searchText = string.Empty;
        private double _panelOpacity = 0.9;
        private bool _isMinimized = false;
        private bool _isClickThroughEnabled = false;
        private bool _isSyncWithMainWindowEnabled = false;
        private ISettingsService _settingsService;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CloseRequested;
        public event EventHandler<double>? OpacityChanged;
        public event EventHandler<bool>? MinimizeRequested;
        public event EventHandler? AddShortcutRequested;
        public event EventHandler<bool>? ClickThroughChanged;
        public event EventHandler<bool>? SyncWithMainWindowChanged;

        public ShortcutPreviewPanel() : this(null)
        {
        }

        public ShortcutPreviewPanel(ISettingsService? settingsService)
        {
            _settingsService = settingsService ?? new SettingsService();
            InitializeComponent();
            DataContext = this;
            FilterShortcuts();

            // Ayarlardan senkronizasyon durumunu yükle
            if (_settingsService?.Settings != null)
            {
                _isSyncWithMainWindowEnabled = _settingsService.Settings.ShortcutPreviewPanelSyncWithMainWindow;
                OnPropertyChanged(nameof(IsSyncWithMainWindowEnabled));
            }
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

        public string ActiveTriggerKey => _settingsService?.Settings != null ?
            ExpansionTriggerKeyHelper.GetDescription(_settingsService.Settings.ExpansionTriggerKey) :
            "Ctrl + Space";

        public bool IsClickThroughEnabled
        {
            get => _isClickThroughEnabled;
            set
            {
                _isClickThroughEnabled = value;
                OnPropertyChanged();
                ClickThroughChanged?.Invoke(this, value);
            }
        }

        public bool IsSyncWithMainWindowEnabled
        {
            get => _isSyncWithMainWindowEnabled;
            set
            {
                _isSyncWithMainWindowEnabled = value;
                OnPropertyChanged();
                SyncWithMainWindowChanged?.Invoke(this, value);
            }
        }

        public void SetSettingsService(ISettingsService settingsService)
        {
            // Eski event'i temizle
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            _settingsService = settingsService;

            // Yeni event'i dinle
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }

            OnPropertyChanged(nameof(ActiveTriggerKey));
        }

        private void OnSettingsChanged(AppSettings settings)
        {
            // UI thread'de çalıştığımızdan emin ol
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnSettingsChanged(settings));
                return;
            }

            OnPropertyChanged(nameof(ActiveTriggerKey));
        }

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

        private void AddShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            AddShortcutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShortcutKeyBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            ShortcutKeyPopup.IsOpen = true;
        }

        private void ShortcutKeyBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            ShortcutKeyPopup.IsOpen = false;
        }

        private void TabKeyBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            TabKeyPopup.IsOpen = true;
        }

        private void TabKeyBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            TabKeyPopup.IsOpen = false;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            IsMinimized = !IsMinimized;
            MinimizeRequested?.Invoke(this, IsMinimized);
        }

        private void ClickThroughToggle_Click(object sender, MouseButtonEventArgs e)
        {
            IsClickThroughEnabled = !IsClickThroughEnabled;
        }

        private void SyncWithMainWindowToggle_Click(object sender, MouseButtonEventArgs e)
        {
            IsSyncWithMainWindowEnabled = !IsSyncWithMainWindowEnabled;

            // Ayarlara kaydet
            if (_settingsService?.Settings != null)
            {
                var settings = _settingsService.GetCopy();
                settings.ShortcutPreviewPanelSyncWithMainWindow = IsSyncWithMainWindowEnabled;
                _settingsService.UpdateSettings(settings);
                _ = _settingsService.SaveSettingsAsync();
            }
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
