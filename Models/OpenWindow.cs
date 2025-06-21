using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class OpenWindow : INotifyPropertyChanged
    {
        private bool _isSelected = false;
        private string _title = "";
        private string _processName = "";
        private string _processPath = "";
        private IntPtr _handle = IntPtr.Zero;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string ProcessName
        {
            get => _processName;
            set
            {
                _processName = value;
                OnPropertyChanged();
            }
        }

        public string ProcessPath
        {
            get => _processPath;
            set
            {
                _processPath = value;
                OnPropertyChanged();
            }
        }

        public IntPtr Handle
        {
            get => _handle;
            set
            {
                _handle = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName => string.IsNullOrEmpty(Title) ? ProcessName : $"{ProcessName} - {Title}";

        public string Icon => GetIconForProcess(ProcessName);

        private string GetIconForProcess(string processName)
        {
            return processName.ToLower() switch
            {
                "notepad" => "📝",
                "chrome" => "🌐",
                "firefox" => "🦊",
                "edge" => "🌐",
                "winword" => "📄",
                "excel" => "📊",
                "powerpnt" => "📊",
                "code" => "💻",
                "devenv" => "🔧",
                "explorer" => "📁",
                "cmd" => "⚫",
                "powershell" => "🔵",
                "calculator" => "🧮",
                "mspaint" => "🎨",
                "discord" => "💬",
                "teams" => "👥",
                "outlook" => "📧",
                "spotify" => "🎵",
                "vlc" => "🎬",
                _ => "🪟"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
