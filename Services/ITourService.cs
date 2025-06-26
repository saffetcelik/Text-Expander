using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public interface ITourService
    {
        event Action<TourStep>? StepChanged;
        event Action? TourCompleted;
        event Action? TourSkipped;
        
        bool IsTourActive { get; }
        bool IsFirstRun { get; }
        TourStep? CurrentStep { get; }
        int CurrentStepIndex { get; }
        int TotalSteps { get; }
        
        Task StartTourAsync();
        Task NextStepAsync();
        Task PreviousStepAsync();
        Task SkipTourAsync();
        Task CompleteTourAsync();
        void SetFirstRunCompleted();
        List<TourStep> GetTourSteps();
    }
}
