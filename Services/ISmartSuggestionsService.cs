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
        
        Task<LearningStatistics> GetStatisticsAsync();
        Task<DetailedStatistics> GetLearningDataAsync();
        Task ExportLearningDataAsync(string filePath);
        Task ImportLearningDataAsync(string filePath);
        Task ResetLearningDataAsync();
        
        void Enable();
        void Disable();
        void UpdateSettings(AppSettings settings);
    }
}
