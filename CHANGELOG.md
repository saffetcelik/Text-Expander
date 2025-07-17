# Changelog

## [1.1.6] - 2025-01-17

### ⚡ Kritik Performance İyileştirmeleri
- **Hızlı Tab Basma Optimizasyonu**:
  - Tab queue sistemi ile ConcurrentQueue tabanlı sıralı işleme
  - Race condition'lar tamamen önlendi
  - Kelime kaçırma sorunu çözüldü

- **Önizleme Ultra Hızlandırma**:
  - Debounce timer tamamen kaldırıldı (20ms → 0ms)
  - Senkron UI güncellemeleri implementasyonu
  - SafeSetPreviewText senkron hale getirildi
  - Önizleme artık anında gösteriliyor

- **Tab Queue Sistemi**:
  - SemaphoreSlim ile async operasyon serialize
  - CancellationToken ile kontrollü iptal mekanizması
  - Context buffer akıllı yönetimi
  - Queue delay tamamen kaldırıldı (10ms → 0ms)

### 🚀 Performans İyileştirmeleri
- Task.Delay değerleri minimize edildi (150ms → 10ms)
- Clipboard restore delay optimize edildi (200ms → 100ms)
- Memory allocation'lar azaltıldı
- UI thread kontrolü ile senkron işlemler

### 🔧 Teknik İyileştirmeler
- UpdateSmartSuggestionsUI helper metodu eklendi
- SetPreviewTextDirect senkron metodu
- Context buffer overflow kontrolü optimize edildi
- Gereksiz async çağrılar kaldırıldı

### 📝 Kullanıcı Deneyimi
- Seri tab basma artık çok daha hızlı
- Önizleme gecikmesi tamamen ortadan kalktı
- "duru sürekli kitap okuyan bir çocuktur" → "duru " senaryosu optimize edildi
- Maximum hız için agresif optimizasyonlar

## [1.1.3] - 2025-01-14

### 🔧 Düzeltmeler
- **N-gram Algoritması Büyük İyileştirme**: 
  - "gitti" kelimesinin sürekli önerilme sorunu çözüldü
  - Unigram önerileri artık sadece context yoksa çalışıyor
  - Context-aware öneriler iyileştirildi
  
- **Confidence Skorları Yeniden Düzenlendi**:
  - 4-gram: 0.85-0.98 (en yüksek öncelik)
  - Trigram: 0.75-0.95 (yüksek öncelik)
  - Bigram: 0.60-0.85 (orta öncelik)
  - Unigram: 0.05-0.30 (düşük öncelik, sadece context yoksa)

- **Context Matching İyileştirmeleri**:
  - StringComparison.OrdinalIgnoreCase desteği eklendi
  - Daha kesin n-gram eşleştirme algoritması
  - 4-gram önerilerinin UI'da görünmeme sorunu çözüldü

### 🚀 Performans
- N-gram önerilerinin sıralama algoritması optimize edildi
- Debug çıktıları artırıldı (geliştirme amaçlı)
- Öneri kalitesi önemli ölçüde iyileştirildi

### 📝 Teknik Detaylar
- N-gram algoritması artık 4-gram → trigram → bigram → unigram sırasını doğru takip ediyor
- Frekans tabanlı sıralama iyileştirildi
- Context bilgisi korunması geliştirildi

## [1.1.2] - 2025-01-13

### 🆕 Yeni Özellikler
- Akıllı metin önerileri sistemi
- N-gram tabanlı öğrenme algoritması
- Gelişmiş kullanıcı arayüzü

### 🔧 Düzeltmeler
- Performans iyileştirmeleri
- Bellek kullanımı optimizasyonu

## [1.1.1] - 2025-01-12

### 🔧 Düzeltmeler
- Başlangıç hataları giderildi
- Sistem tray entegrasyonu iyileştirildi

## [1.1.0] - 2025-01-11

### 🆕 Yeni Özellikler
- Modern WPF kullanıcı arayüzü
- Sistem tray desteği
- Pencere filtreleme sistemi
- Özelleştirilebilir kısayollar

### 🔧 Düzeltmeler
- Klavye hook sistemi iyileştirildi
- Bellek sızıntıları giderildi

## [1.0.0] - 2025-01-10

### 🎉 İlk Sürüm
- Temel metin genişletme özelliği
- Kısayol yönetimi
- Basit kullanıcı arayüzü
