using System.Collections.Concurrent;

namespace OtomatikMetinGenisletici.Models
{
    public class LearningData
    {
        public ConcurrentDictionary<string, int> WordFrequencies { get; set; } = new();
        public ConcurrentDictionary<string, int> Bigrams { get; set; } = new();
        public ConcurrentDictionary<string, int> Trigrams { get; set; } = new();
        public ConcurrentDictionary<string, List<string>> CompletionPrefixes { get; set; } = new();
        public ConcurrentDictionary<string, List<string>> UserCorrections { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public int TotalWordsLearned { get; set; }
        public int TotalSuggestionsGiven { get; set; }
        public int TotalSuggestionsAccepted { get; set; }
        public int TotalSuggestionsRejected { get; set; }
    }

    public class NGramData
    {
        public string Key { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public DateTime LastUsed { get; set; }
        public double Weight { get; set; } = 1.0;
    }

    public class WordCompletionData
    {
        public string Prefix { get; set; } = string.Empty;
        public string Completion { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public DateTime LastUsed { get; set; }
        public double Confidence { get; set; }
    }

    public class LearningStatistics
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
    }
}
