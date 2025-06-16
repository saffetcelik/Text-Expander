using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class SmartPhraseData : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private string _originalSentence = string.Empty;
        private int _frequency;
        private DateTime _firstSeen;
        private DateTime _lastSeen;
        private List<string> _contexts = new();
        private string _suggestedShortcut = string.Empty;
        private double _qualityScore;
        private double _usabilityScore;
        private PhraseType _phraseType;
        private List<string> _precedingWords = new();
        private List<string> _followingWords = new();

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }

        public string OriginalSentence
        {
            get => _originalSentence;
            set
            {
                _originalSentence = value;
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

        public double QualityScore
        {
            get => _qualityScore;
            set
            {
                _qualityScore = value;
                OnPropertyChanged();
            }
        }

        public double UsabilityScore
        {
            get => _usabilityScore;
            set
            {
                _usabilityScore = value;
                OnPropertyChanged();
            }
        }

        public PhraseType PhraseType
        {
            get => _phraseType;
            set
            {
                _phraseType = value;
                OnPropertyChanged();
            }
        }

        public List<string> PrecedingWords
        {
            get => _precedingWords;
            set
            {
                _precedingWords = value ?? new List<string>();
                OnPropertyChanged();
            }
        }

        public List<string> FollowingWords
        {
            get => _followingWords;
            set
            {
                _followingWords = value ?? new List<string>();
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
        public bool IsHighQuality => QualityScore > 0.7;
        public bool IsUsable => UsabilityScore > 0.6;

        public SmartPhraseData()
        {
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;
            Frequency = 1;
            Contexts = new List<string>();
            PrecedingWords = new List<string>();
            FollowingWords = new List<string>();
            PhraseType = PhraseType.General;
        }

        public SmartPhraseData(string text, string originalSentence = "") : this()
        {
            Text = text;
            OriginalSentence = originalSentence;
        }

        public void IncrementFrequency(string context = "", List<string>? preceding = null, List<string>? following = null)
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

            // Önceki ve sonraki kelimeleri kaydet
            if (preceding != null)
            {
                foreach (var word in preceding.Take(3)) // Son 3 kelime
                {
                    if (!PrecedingWords.Contains(word))
                    {
                        PrecedingWords.Add(word);
                        if (PrecedingWords.Count > 10)
                        {
                            PrecedingWords.RemoveAt(0);
                        }
                    }
                }
            }

            if (following != null)
            {
                foreach (var word in following.Take(3)) // İlk 3 kelime
                {
                    if (!FollowingWords.Contains(word))
                    {
                        FollowingWords.Add(word);
                        if (FollowingWords.Count > 10)
                        {
                            FollowingWords.RemoveAt(0);
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum PhraseType
    {
        General,        // Genel ifade
        Professional,   // Mesleki terim
        Legal,          // Hukuki terim
        Template,       // Şablon ifade
        Greeting,       // Selamlama
        Closing,        // Kapanış
        Question,       // Soru
        Statement       // Beyan
    }
}
