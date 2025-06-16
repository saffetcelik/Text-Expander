using System.Collections.ObjectModel;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Services
{
    public interface IShortcutService
    {
        ObservableCollection<Shortcut> Shortcuts { get; }
        event Action<string>? ShortcutExpanded;
        
        Task LoadShortcutsAsync();
        Task SaveShortcutsAsync();
        void AddShortcut(string key, string expansion);
        void RemoveShortcut(string key);
        bool TryExpandShortcut(string input, out string expansion);
        Shortcut? GetShortcut(string key);
        bool ShortcutExists(string key);
    }
}
