using OtomatikMetinGenisletici.Models;
using System.IO;
using Newtonsoft.Json;

namespace OtomatikMetinGenisletici.Services
{
    public class TourService : ITourService
    {
        private const string TourSettingsFileName = "tour_settings.json";
        private List<TourStep> _tourSteps = new();
        private int _currentStepIndex = -1;
        private bool _isTourActive = false;
        private TourSettings _settings = new();

        public event Action<TourStep>? StepChanged;
        public event Action? TourCompleted;
        public event Action? TourSkipped;

        public bool IsTourActive => _isTourActive;
        public bool IsFirstRun => _settings.IsFirstRun;
        public TourStep? CurrentStep => _currentStepIndex >= 0 && _currentStepIndex < _tourSteps.Count 
            ? _tourSteps[_currentStepIndex] : null;
        public int CurrentStepIndex => _currentStepIndex;
        public int TotalSteps => _tourSteps.Count;

        public TourService()
        {
            LoadTourSettings();
            InitializeTourSteps();
        }

        private void LoadTourSettings()
        {
            try
            {
                if (File.Exists(TourSettingsFileName))
                {
                    var json = File.ReadAllText(TourSettingsFileName);
                    var settings = JsonConvert.DeserializeObject<TourSettings>(json);
                    if (settings != null)
                    {
                        _settings = settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tour ayarları yüklenirken hata: {ex.Message}");
                _settings = new TourSettings();
            }
        }

        private void SaveTourSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(TourSettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tour ayarları kaydedilirken hata: {ex.Message}");
            }
        }

        private void InitializeTourSteps()
        {
            _tourSteps = new List<TourStep>
            {
                new TourStep
                {
                    Id = "welcome",
                    Title = "🎉 Text Expander'a Hoş Geldiniz!",
                    Description = "Bu kısa tur ile programın temel özelliklerini öğreneceksiniz. Yazma hızınızı artırmaya hazır mısınız?",
                    Position = TourStepPosition.Center,
                    Icon = "🚀",
                    Type = TourStepType.Information,
                    Duration = 4000
                },
                new TourStep
                {
                    Id = "add_shortcut",
                    Title = "📝 Kısayol Ekleme",
                    Description = "Bu butona tıklayarak yeni kısayollar ekleyebilirsiniz. Örneğin 'merhaba' yazıp Tab tuşuna bastığınızda 'Merhaba, nasılsınız?' metni otomatik olarak genişler.",
                    TargetElement = "AddShortcutButton",
                    Position = TourStepPosition.Bottom,
                    Icon = "➕",
                    Type = TourStepType.Highlight,
                    ActionText = "Kısayol Ekle",
                    ActionTarget = "AddShortcutButton"
                },
                new TourStep
                {
                    Id = "tab_space_keys",
                    Title = "⌨️ Tab ve Boşluk Tuşları",
                    Description = "• Tab tuşu: Akıllı önerileri kabul etmek için\n• Boşluk tuşu: Kısayolları genişletmek için\n\nÖrnek: 'merhaba' yazıp boşluk tuşuna basın!",
                    Position = TourStepPosition.Center,
                    Icon = "⌨️",
                    Type = TourStepType.Information,
                    Duration = 6000
                },
                new TourStep
                {
                    Id = "smart_suggestions",
                    Title = "🧠 Akıllı Öneriler",
                    Description = "Program yazdığınız metinleri N-gram teknolojisi ile öğrenir ve size akıllı öneriler sunar. Bu sayede daha hızlı yazabilirsiniz.",
                    Position = TourStepPosition.Center,
                    Icon = "🤖",
                    Type = TourStepType.Information,
                    Duration = 5000
                },
                new TourStep
                {
                    Id = "learning_logs",
                    Title = "📊 Öğrenme Logları",
                    Description = "Programın öğrendiği her kelime ve cümle burada görüntülenir. İlerlemeni takip edebilir ve öğrenilen verileri yönetebilirsin.",
                    Position = TourStepPosition.Center,
                    Icon = "📈",
                    Type = TourStepType.Information,
                    Duration = 4000
                },
                new TourStep
                {
                    Id = "window_filtering",
                    Title = "🎯 Pencere Filtreleme",
                    Description = "Ayarlar menüsünden programın hangi uygulamalarda çalışacağını seçebilirsiniz. Böylece sadece istediğiniz programlarda metin genişletme aktif olur.",
                    TargetElement = "SettingsButton",
                    Position = TourStepPosition.Bottom,
                    Icon = "⚙️",
                    Type = TourStepType.Highlight
                },
                new TourStep
                {
                    Id = "shortcut_preview",
                    Title = "👁️ Kısayol Önizleme",
                    Description = "Bu panel ile tüm kısayollarınızı görebilir, arama yapabilir ve hızlıca erişebilirsiniz. Panel boyutlandırılabilir ve sürüklenebilir.",
                    TargetElement = "ShortcutPreviewButton",
                    Position = TourStepPosition.Bottom,
                    Icon = "🔗",
                    Type = TourStepType.Highlight
                },
                new TourStep
                {
                    Id = "completion",
                    Title = "🎊 Tebrikler!",
                    Description = "Tur tamamlandı! Artık Text Expander'ı etkili şekilde kullanabilirsiniz. İyi yazımlar!\n\nİpucu: Bu turu istediğiniz zaman 'Hakkında' menüsünden tekrar başlatabilirsiniz.",
                    Position = TourStepPosition.Center,
                    Icon = "🏆",
                    Type = TourStepType.Information,
                    Duration = 6000,
                    IsSkippable = false
                }
            };
        }

        public async Task StartTourAsync()
        {
            if (_isTourActive) return;

            _isTourActive = true;
            _currentStepIndex = 0;
            
            Console.WriteLine("[TOUR] Tur başlatıldı");
            
            if (CurrentStep != null)
            {
                StepChanged?.Invoke(CurrentStep);
            }
        }

        public async Task NextStepAsync()
        {
            if (!_isTourActive) return;

            _currentStepIndex++;
            
            if (_currentStepIndex >= _tourSteps.Count)
            {
                await CompleteTourAsync();
                return;
            }

            Console.WriteLine($"[TOUR] Sonraki adım: {_currentStepIndex + 1}/{_tourSteps.Count}");
            
            if (CurrentStep != null)
            {
                StepChanged?.Invoke(CurrentStep);
            }
        }

        public async Task PreviousStepAsync()
        {
            if (!_isTourActive || _currentStepIndex <= 0) return;

            _currentStepIndex--;
            
            Console.WriteLine($"[TOUR] Önceki adım: {_currentStepIndex + 1}/{_tourSteps.Count}");
            
            if (CurrentStep != null)
            {
                StepChanged?.Invoke(CurrentStep);
            }
        }

        public async Task SkipTourAsync()
        {
            if (!_isTourActive) return;

            Console.WriteLine("[TOUR] Tur atlandı");
            
            _isTourActive = false;
            _currentStepIndex = -1;
            
            TourSkipped?.Invoke();
        }

        public async Task CompleteTourAsync()
        {
            if (!_isTourActive) return;

            Console.WriteLine("[TOUR] Tur tamamlandı");
            
            _isTourActive = false;
            _currentStepIndex = -1;
            
            // İlk çalıştırma tamamlandı olarak işaretle
            if (_settings.IsFirstRun)
            {
                SetFirstRunCompleted();
            }
            
            TourCompleted?.Invoke();
        }

        public void SetFirstRunCompleted()
        {
            _settings.IsFirstRun = false;
            SaveTourSettings();
            Console.WriteLine("[TOUR] İlk çalıştırma tamamlandı olarak işaretlendi");
        }

        public List<TourStep> GetTourSteps()
        {
            return new List<TourStep>(_tourSteps);
        }

        private class TourSettings
        {
            public bool IsFirstRun { get; set; } = true;
        }
    }
}
