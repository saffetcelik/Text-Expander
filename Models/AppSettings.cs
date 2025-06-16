using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        private bool _smartSuggestionsEnabled = false;
        private int _maxSmartSuggestions = 5;
        private int _minWordLength = 2;
        private bool _learningEnabled = true;
        private double _learningWeight = 0.8;

        // Önizleme Ayarları
        private bool _previewAlwaysVisible = true; // Varsayılan: sürekli açık

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

        public bool PreviewAlwaysVisible
        {
            get => _previewAlwaysVisible;
            set
            {
                _previewAlwaysVisible = value;
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
