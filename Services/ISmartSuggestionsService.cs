using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public interface ISmartSuggestionsService : IDisposable
    {
        bool IsEnabled { get; }
        event Action<List<SmartSuggestion>>? SuggestionsUpdated;
        event Action<SmartSuggestion>? SuggestionAccepted;
        
        Task InitializeAsync();
        Task<List<SmartSuggestion>> GetSuggestionsAsync(string context, int maxSuggestions = 5);
        Task LearnFromTextAsync(string text);
        Task AcceptSuggestionAsync(SmartSuggestion suggestion, string context);
        Task RejectSuggestionAsync(SmartSuggestion suggestion, string context);

        // Suggestion paste tracking
        void MarkSuggestionAsPasted(string suggestionText);
        bool IsRecentlyPastedSuggestion(string text);
        
        Task<LearningStatistics> GetStatisticsAsync();
        Task<DetailedStatistics> GetLearningDataAsync();
        Task ExportLearningDataAsync(string filePath);
        Task ImportLearningDataAsync(string filePath);
        Task ResetLearningDataAsync();
        
        void Enable();
        void Disable();
        void UpdateSettings(AppSettings settings);

        // Veri Yönetimi Metodları
        Task<bool> UpdateWordAsync(string oldWord, string newWord, int newCount);
        Task<bool> DeleteWordAsync(string word);
        Task<bool> UpdateBigramAsync(string oldBigram, string newBigram, int newCount);
        Task<bool> DeleteBigramAsync(string bigram);
        Task<bool> UpdateTrigramAsync(string oldTrigram, string newTrigram, int newCount);
        Task<bool> DeleteTrigramAsync(string trigram);
        Task<List<(string Word, int Count)>> SearchWordsAsync(string searchTerm, int maxResults = 50);
        Task<List<(string Bigram, int Count)>> SearchBigramsAsync(string searchTerm, int maxResults = 50);
        Task<List<(string Trigram, int Count)>> SearchTrigramsAsync(string searchTerm, int maxResults = 50);
        Task SaveDataAsync();
    }
}
