using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace OtomatikMetinGenisletici.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private bool _autoStart = false;
        private bool _showNotifications = true;
        private int _expansionDelay = 0;
        private string _fontFamily = "Arial";
        private int _fontSize = 12;
        private int _minPhraseLength = 3;
        private int _maxPhraseLength = 15;
        private int _minFrequency = 2;
        private int _maxSuggestions = 20;
        private double _contextWeight = 0.7;

        // Akıllı Öneriler Ayarları
        private bool _smartSuggestionsEnabled = true;
        private int _maxSmartSuggestions = 5;
        private int _minWordLength = 2;
        private bool _learningEnabled = true;
        private double _learningWeight = 0.8;



        // Kısayol Önizleme Paneli Ayarları
        private bool _shortcutPreviewPanelVisible = true; // Varsayılan: açılışta açık
        private double _shortcutPreviewPanelOpacity = 0.9;
        private double _shortcutPreviewPanelWidth = 290;
        private double _shortcutPreviewPanelHeight = 481;
        private double _shortcutPreviewPanelLeft = -1; // -1 = otomatik pozisyon
        private double _shortcutPreviewPanelTop = -1;

        // Pencere Filtreleme Ayarları
        private ObservableCollection<WindowFilter> _windowFilters = new();
        private bool _windowFilteringEnabled = false; // Varsayılan: tüm pencerelerde çalış
        private WindowFilterMode _windowFilterMode = WindowFilterMode.AllowList;

        public AppSettings()
        {
            // Varsayılan olarak hiçbir pencere filtresi eklenmez
            // Kullanıcı istediğinde kendi filtrelerini oluşturabilir
        }

        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                OnPropertyChanged();
            }
        }

        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                _showNotifications = value;
                OnPropertyChanged();
            }
        }

        public int ExpansionDelay
        {
            get => _expansionDelay;
            set
            {
                _expansionDelay = value;
                OnPropertyChanged();
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                _fontFamily = value;
                OnPropertyChanged();
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                OnPropertyChanged();
            }
        }

        public int MinPhraseLength
        {
            get => _minPhraseLength;
            set
            {
                _minPhraseLength = value;
                OnPropertyChanged();
            }
        }

        public int MaxPhraseLength
        {
            get => _maxPhraseLength;
            set
            {
                _maxPhraseLength = value;
                OnPropertyChanged();
            }
        }

        public int MinFrequency
        {
            get => _minFrequency;
            set
            {
                _minFrequency = value;
                OnPropertyChanged();
            }
        }

        public int MaxSuggestions
        {
            get => _maxSuggestions;
            set
            {
                _maxSuggestions = value;
                OnPropertyChanged();
            }
        }

        public double ContextWeight
        {
            get => _contextWeight;
            set
            {
                _contextWeight = value;
                OnPropertyChanged();
            }
        }

        public bool SmartSuggestionsEnabled
        {
            get => _smartSuggestionsEnabled;
            set
            {
                _smartSuggestionsEnabled = value;
                OnPropertyChanged();
            }
        }

        public int MaxSmartSuggestions
        {
            get => _maxSmartSuggestions;
            set
            {
                _maxSmartSuggestions = value;
                OnPropertyChanged();
            }
        }

        public int MinWordLength
        {
            get => _minWordLength;
            set
            {
                _minWordLength = value;
                OnPropertyChanged();
            }
        }

        public bool LearningEnabled
        {
            get => _learningEnabled;
            set
            {
                _learningEnabled = value;
                OnPropertyChanged();
            }
        }

        public double LearningWeight
        {
            get => _learningWeight;
            set
            {
                _learningWeight = value;
                OnPropertyChanged();
            }
        }



        public bool ShortcutPreviewPanelVisible
        {
            get => _shortcutPreviewPanelVisible;
            set
            {
                _shortcutPreviewPanelVisible = value;
                OnPropertyChanged();
            }
        }

        public double ShortcutPreviewPanelOpacity
        {
            get => _shortcutPreviewPanelOpacity;
            set
            {
                _shortcutPreviewPanelOpacity = Math.Max(0.1, Math.Min(1.0, value));
                OnPropertyChanged();
            }
        }

        public double ShortcutPreviewPanelWidth
        {
            get => _shortcutPreviewPanelWidth;
            set
            {
                _shortcutPreviewPanelWidth = Math.Max(200, Math.Min(500, value));
                OnPropertyChanged();
            }
        }

        public double ShortcutPreviewPanelHeight
        {
            get => _shortcutPreviewPanelHeight;
            set
            {
                _shortcutPreviewPanelHeight = Math.Max(300, Math.Min(1200, value));
                OnPropertyChanged();
            }
        }

        public double ShortcutPreviewPanelLeft
        {
            get => _shortcutPreviewPanelLeft;
            set
            {
                _shortcutPreviewPanelLeft = value;
                OnPropertyChanged();
            }
        }

        public double ShortcutPreviewPanelTop
        {
            get => _shortcutPreviewPanelTop;
            set
            {
                _shortcutPreviewPanelTop = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<WindowFilter> WindowFilters
        {
            get => _windowFilters;
            set
            {
                _windowFilters = value;
                OnPropertyChanged();
            }
        }

        public bool WindowFilteringEnabled
        {
            get => _windowFilteringEnabled;
            set
            {
                _windowFilteringEnabled = value;
                OnPropertyChanged();
            }
        }

        public WindowFilterMode WindowFilterMode
        {
            get => _windowFilterMode;
            set
            {
                _windowFilterMode = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
