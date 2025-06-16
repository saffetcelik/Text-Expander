using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class PhraseData : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private int _frequency;
        private DateTime _firstSeen;
        private DateTime _lastSeen;
        private double _averageTypingSpeed;
        private List<string> _contexts = new();
        private string _suggestedShortcut = string.Empty;
        private double _relevanceScore;

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

        public DateTime FirstSeen
        {
            get => _firstSeen;
            set
            {
                _firstSeen = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastSeen
        {
            get => _lastSeen;
            set
            {
                _lastSeen = value;
                OnPropertyChanged();
            }
        }

        public double AverageTypingSpeed
        {
            get => _averageTypingSpeed;
            set
            {
                _averageTypingSpeed = value;
                OnPropertyChanged();
            }
        }

        public List<string> Contexts
        {
            get => _contexts;
            set
            {
                _contexts = value ?? new List<string>();
                OnPropertyChanged();
            }
        }

        public string SuggestedShortcut
        {
            get => _suggestedShortcut;
            set
            {
                _suggestedShortcut = value;
                OnPropertyChanged();
            }
        }

        public double RelevanceScore
        {
            get => _relevanceScore;
            set
            {
                _relevanceScore = value;
                OnPropertyChanged();
            }
        }

        // Hesaplanan özellikler
        public int DaysSinceFirstSeen => (DateTime.Now - FirstSeen).Days;
        public int DaysSinceLastSeen => (DateTime.Now - LastSeen).Days;
        public double FrequencyPerDay => DaysSinceFirstSeen > 0 ? (double)Frequency / DaysSinceFirstSeen : Frequency;
        public bool IsRecent => DaysSinceLastSeen <= 7;
        public bool IsFrequent => Frequency >= 3;
        public int WordCount => Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        public PhraseData()
        {
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;
            Frequency = 1;
            Contexts = new List<string>();
        }

        public PhraseData(string text) : this()
        {
            Text = text;
        }

        public void IncrementFrequency(string context = "")
        {
            Frequency++;
            LastSeen = DateTime.Now;
            
            if (!string.IsNullOrEmpty(context) && !Contexts.Contains(context))
            {
                Contexts.Add(context);
                if (Contexts.Count > 10) // Son 10 context'i tut
                {
                    Contexts.RemoveAt(0);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
