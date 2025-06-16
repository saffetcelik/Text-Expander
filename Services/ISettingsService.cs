using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        event Action<AppSettings>? SettingsChanged;
        
        Task LoadSettingsAsync();
        Task SaveSettingsAsync();
        void UpdateSettings(AppSettings newSettings);
        AppSettings GetCopy();
    }
}
