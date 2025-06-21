using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class WindowFilter : INotifyPropertyChanged
    {
        private string _name = "";
        private string _titlePattern = "";
        private string _processName = "";
        private bool _isEnabled = true;
        private WindowFilterType _filterType = WindowFilterType.TitleContains;
        private bool _isRegex = false;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string TitlePattern
        {
            get => _titlePattern;
            set
            {
                _titlePattern = value;
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

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public WindowFilterType FilterType
        {
            get => _filterType;
            set
            {
                _filterType = value;
                OnPropertyChanged();
            }
        }

        public bool IsRegex
        {
            get => _isRegex;
            set
            {
                _isRegex = value;
                OnPropertyChanged();
            }
        }

        public string FilterTypeDisplayName => FilterType switch
        {
            WindowFilterType.TitleContains => "Başlık İçerir",
            WindowFilterType.TitleEquals => "Başlık Eşittir",
            WindowFilterType.ProcessEquals => "Process Eşittir",
            WindowFilterType.TitleStartsWith => "Başlık İle Başlar",
            WindowFilterType.TitleEndsWith => "Başlık İle Biter",
            _ => "Bilinmeyen"
        };

        public string StatusText => IsEnabled ? "🟢 Aktif" : "🔴 Pasif";
        public string StatusColor => IsEnabled ? "Green" : "Red";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum WindowFilterType
    {
        TitleContains,
        TitleEquals,
        ProcessEquals,
        TitleStartsWith,
        TitleEndsWith
    }

    public enum WindowFilterMode
    {
        AllowList,  // İzin Listesi - Sadece seçilenlerde çalış
        BlockList   // Engel Listesi - Seçilenler hariç her yerde çalış
    }
}
