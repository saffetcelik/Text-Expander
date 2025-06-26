# 🤝 Text Expander Projesine Katkıda Bulunma Rehberi

Text Expander'ı daha iyi bir araç haline getirmemize yardımcı olduğunuz için teşekkür ederiz! Katkılarınız, projenin gelişimi için çok değerlidir.

Bu rehber, katkıda bulunma sürecini herkes için kolay ve şeffaf hale getirmeyi amaçlamaktadır.

## 📋 İçindekiler

- [Başlamadan Önce](#-başlamadan-önce)
- [Katkı Türleri](#-katkı-türleri)
- [Geliştirme Ortamı Kurulumu](#️-geliştirme-ortamı-kurulumu)
- [Katkı Süreci](#-katkı-süreci)
- [Kodlama Standartları](#-kodlama-standartları)
- [Test Yazımı](#-test-yazımı)
- [Dokümantasyon](#-dokümantasyon)
- [İletişim](#-i̇letişim)

## 💬 Başlamadan Önce

Bir özellik eklemek veya bir hatayı düzeltmek için büyük bir çalışma yapmadan önce, lütfen **Issues** bölümünü kontrol edin veya yeni bir tane oluşturun. Bu, aynı konu üzerinde mükerrer çalışma yapılmasını önler ve önerilen değişikliğin projenin hedefleriyle uyumlu olup olmadığını en başta netleştirir.

### 🐛 Hata Bildirimi (Bug Report)
Eğer bir hata bulduysanız, lütfen aşağıdaki bilgileri içeren detaylı bir issue açın:
- **Hata Açıklaması**: Neyin yanlış gittiğini açıklayın
- **Yeniden Oluşturma Adımları**: Hatayı nasıl yeniden oluşturabiliriz?
- **Beklenen Davranış**: Ne olması gerekiyordu?
- **Gerçekleşen Davranış**: Ne oldu?
- **Sistem Bilgileri**: Windows sürümü, .NET sürümü
- **Ekran Görüntüleri**: Varsa ekran görüntüleri ekleyin

### ✨ Özellik İsteği (Feature Request)
Yeni bir özellik önermek istiyorsanız, lütfen şunları belirtin:
- **Özellik Açıklaması**: Ne istiyorsunuz?
- **Motivasyon**: Neden bu özellik gerekli?
- **Kullanım Senaryoları**: Bu özellik nasıl kullanılacak?
- **Alternatifler**: Başka çözümler düşündünüz mü?

## 🎯 Katkı Türleri

### 🐛 Hata Düzeltmeleri
- Mevcut hataları tespit edin ve düzeltin
- Edge case'leri ele alın
- Performance sorunlarını çözün

### ✨ Yeni Özellikler
- Kullanıcı deneyimini iyileştiren özellikler
- Yeni kısayol türleri
- Gelişmiş filtreleme seçenekleri
- Tema ve görünüm iyileştirmeleri

### 📚 Dokümantasyon
- README.md iyileştirmeleri
- Kod yorumları
- API dokümantasyonu
- Kullanım kılavuzları

### 🧪 Test Yazımı
- Unit testler
- Integration testler
- UI testleri
- Performance testleri

### 🌍 Lokalizasyon
- Çoklu dil desteği
- Çeviri iyileştirmeleri
- Kültürel adaptasyonlar

## 🛠️ Geliştirme Ortamı Kurulumu

### Gereksinimler
- **Visual Studio 2022** veya **Visual Studio Code**
- **.NET 8 SDK**
- **Git**
- **Windows 10/11** (geliştirme ve test için)

### Kurulum Adımları

```bash
# 1. Projeyi fork edin ve klonlayın
git clone https://github.com/YOUR-USERNAME/Text-Expander.git
cd Text-Expander

# 2. Bağımlılıkları yükleyin
dotnet restore

# 3. Projeyi derleyin
dotnet build

# 4. Geliştirme modunda çalıştırın
dotnet run --configuration Debug
```

## 🚀 Katkı Süreci

### 1. 🍴 Fork & Clone
Projeyi kendi GitHub hesabınıza fork'layın ve yerel makinenize klonlayın:
```bash
git clone https://github.com/YOUR-USERNAME/Text-Expander.git
cd Text-Expander
```

### 2. 🌿 Branch Oluşturma
Açıklayıcı bir isme sahip yeni bir branch oluşturun:
```bash
# Hata düzeltmesi için:
git checkout -b fix/keyboard-hook-memory-leak

# Yeni özellik için:
git checkout -b feature/multi-language-support

# Dokümantasyon için:
git checkout -b docs/update-installation-guide
```

### 3. 💻 Geliştirme
- Kodu düzenleyin, yeni özellikler ekleyin veya hataları düzeltin
- Projenin mevcut kod stiline ve mimarisine (MVVM, DI) uyun
- Değişikliklerinizi test edin

### 4. 📝 Commit Mesajları
[Conventional Commits](https://www.conventionalcommits.org/) formatını kullanın:
```bash
# Örnekler:
git commit -m "feat: add multi-language support for UI"
git commit -m "fix: resolve memory leak in keyboard hook service"
git commit -m "docs: update installation instructions"
git commit -m "test: add unit tests for shortcut service"
git commit -m "refactor: improve performance of text learning engine"
```

### 5. 📤 Push & Pull Request
```bash
# Değişikliklerinizi push edin
git push origin feature/your-feature-name

# GitHub'da Pull Request oluşturun
```

### Pull Request Şablonu
PR açıklamasında şunları belirtin:
- **Değişiklik Türü**: Bug fix, feature, docs, etc.
- **Açıklama**: Ne yaptığınızı detaylı açıklayın
- **Test Edilen Senaryolar**: Hangi durumları test ettiniz?
- **İlgili Issue**: `Fixes #123` veya `Closes #456`
- **Ekran Görüntüleri**: UI değişiklikleri varsa

## 📝 Kodlama Standartları

### C# Kodlama Kuralları
- **.NET 8** standartlarına uyun
- **PascalCase** class, method, property isimleri için
- **camelCase** field ve local variable isimleri için
- **Async/await** pattern'ini doğru kullanın
- **SOLID** prensiplerine uyun

### Mimari Kuralları
- **MVVM Pattern**: View, ViewModel, Model ayrımını koruyun
- **Dependency Injection**: Constructor injection kullanın
- **Interface Segregation**: Küçük, odaklanmış interface'ler oluşturun
- **Single Responsibility**: Her class tek bir sorumluluğa sahip olmalı

### Kod Kalitesi
```csharp
// ✅ İyi örnek
public class ShortcutService : IShortcutService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ShortcutService> _logger;

    public ShortcutService(ISettingsService settingsService, ILogger<ShortcutService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TryExpandShortcutAsync(string key, CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

## 🧪 Test Yazımı

### Unit Test Kuralları
- **xUnit** framework kullanın
- **AAA Pattern** (Arrange, Act, Assert) uygulayın
- **Test method isimleri** açıklayıcı olmalı
- **Mock** kullanımında dikkatli olun

```csharp
[Fact]
public async Task TryExpandShortcut_WithValidKey_ShouldReturnTrue()
{
    // Arrange
    var mockSettings = new Mock<ISettingsService>();
    var service = new ShortcutService(mockSettings.Object);

    // Act
    var result = await service.TryExpandShortcutAsync("test");

    // Assert
    Assert.True(result);
}
```

## 📚 Dokümantasyon

### Kod Yorumları
- **XML Documentation** kullanın
- **Public API**'ler için zorunlu
- **Karmaşık algoritmalar** için açıklama ekleyin

```csharp
/// <summary>
/// Expands a shortcut key to its full text representation.
/// </summary>
/// <param name="key">The shortcut key to expand</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>True if expansion was successful, false otherwise</returns>
public async Task<bool> TryExpandShortcutAsync(string key, CancellationToken cancellationToken = default)
{
    // Implementation
}
```

## 📞 İletişim

- **GitHub Issues**: Hata bildirimleri ve özellik istekleri için
- **GitHub Discussions**: Genel sorular ve tartışmalar için
- **Pull Request Reviews**: Kod incelemesi ve geri bildirim için

## 🎉 Teşekkürler

Katkılarınız için şimdiden teşekkürler! Her türlü katkı, projenin gelişimi için değerlidir.
