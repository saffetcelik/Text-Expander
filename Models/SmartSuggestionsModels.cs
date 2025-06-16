using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class WordUsageStatistic : INotifyPropertyChanged
    {
        private int _rank;
        private string _word = string.Empty;
        private int _count;
        private double _percentage;

        public int Rank
        {
            get => _rank;
            set
            {
                _rank = value;
                OnPropertyChanged();
            }
        }

        public string Word
        {
            get => _word;
            set
            {
                _word = value;
                OnPropertyChanged();
            }
        }

        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                OnPropertyChanged();
            }
        }

        public double Percentage
        {
            get => _percentage;
            set
            {
                _percentage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NGramStatistic : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private int _frequency;
        private DateTime _lastUsed;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }

        public int Frequency
        {
            get => _frequency;
            set
            {
                _frequency = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set
            {
                _lastUsed = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LearningActivity : INotifyPropertyChanged
    {
        private string _icon = string.Empty;
        private string _description = string.Empty;
        private DateTime _timestamp;
        private LearningActivityType _type;

        public string Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        public LearningActivityType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum LearningActivityType
    {
        WordLearned,
        SuggestionAccepted,
        SuggestionRejected,
        DataExported,
        DataImported,
        DataReset,
        SettingsChanged
    }

    public class DetailedStatistics
    {
        public int TotalUniqueWords { get; set; }
        public int TotalWordCount { get; set; }
        public int TotalBigrams { get; set; }
        public int TotalTrigrams { get; set; }
        public int CompletionPrefixes { get; set; }
        public int UserCorrections { get; set; }
        public List<(string Word, int Count)> MostCommonWords { get; set; } = new();
        public double AveragePredictionTime { get; set; }
        public double AccuracyScore { get; set; }
        public DateTime LastLearningSession { get; set; }
        public TimeSpan TotalLearningTime { get; set; }
        public int TotalSuggestionsGiven { get; set; }
        public int TotalSuggestionsAccepted { get; set; }
        public int TotalSuggestionsRejected { get; set; }
        public Dictionary<string, int> WordsByLength { get; set; } = new();
        public Dictionary<string, int> BigramsByFrequency { get; set; } = new();
        public Dictionary<string, int> TrigramsByFrequency { get; set; } = new();
        public List<LearningActivity> RecentActivities { get; set; } = new();
    }

    public class SmartSuggestionsSettings
    {
        public bool IsEnabled { get; set; }
        public bool LearningEnabled { get; set; }
        public int MaxSuggestions { get; set; }
        public int MinWordLength { get; set; }
        public double LearningWeight { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
