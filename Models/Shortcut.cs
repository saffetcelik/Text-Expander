using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtomatikMetinGenisletici.Models
{
    public class Shortcut : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _expansion = string.Empty;
        private DateTime _createdDate;
        private DateTime _lastUsed;
        private int _usageCount;

        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                OnPropertyChanged();
            }
        }

        public string Expansion
        {
            get => _expansion;
            set
            {
                _expansion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShortExpansion)); // Kısaltılmış versiyonu da güncelle
            }
        }

        // Tabloda gösterilecek kısaltılmış metin (50 karakter)
        public string ShortExpansion
        {
            get
            {
                if (string.IsNullOrEmpty(_expansion))
                    return string.Empty;

                if (_expansion.Length <= 50)
                    return _expansion;

                return _expansion.Substring(0, 47) + "...";
            }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                _createdDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set
            {
                _lastUsed = value;
                OnPropertyChanged();
            }
        }

        public int UsageCount
        {
            get => _usageCount;
            set
            {
                _usageCount = value;
                OnPropertyChanged();
            }
        }

        public Shortcut()
        {
            CreatedDate = DateTime.Now;
            LastUsed = DateTime.Now;
            UsageCount = 0;
        }

        public Shortcut(string key, string expansion) : this()
        {
            Key = key;
            Expansion = expansion;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
