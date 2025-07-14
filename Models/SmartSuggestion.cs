using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class SmartSuggestion : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private double _confidence;
        private string _context = string.Empty;
        private int _frequency;
        private DateTime _lastUsed;
        private SuggestionType _type;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }

        public double Confidence
        {
            get => _confidence;
            set
            {
                _confidence = value;
                OnPropertyChanged();
            }
        }

        public string Context
        {
            get => _context;
            set
            {
                _context = value;
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

        public SuggestionType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged();
            }
        }

        public string DisplayText => $"{Text} ({Confidence:P0})";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum SuggestionType
    {
        WordCompletion,
        NextWord,
        Phrase,
        Learned,
        SentenceCompletion
    }
}
