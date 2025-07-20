# Changelog

Bu dosya projedeki tüm önemli değişiklikleri belgelemektedir.

## [1.2.3] - 2025-01-20

### 🐛 Düzeltmeler
- **Tab ile kabul edilen metinlerin öğrenme loglarında görünmemesi sorunu düzeltildi**
  - Tab ile kabul edilen metinler artık cümle tamamlandığında öğrenme loglarına ekleniyor
  - Pending sistem ile tab metinleri geçici olarak saklanıyor
  - Sadece cümle tamamlandığında (nokta/Enter) öğrenme loglarına ekleniyor

- **Tab ile kabul edilen metinlerin kelime kelime ayrı öğrenilmesi sorunu düzeltildi**
  - Tab ile kabul edilen metinler artık öğrenme loglarını kirletmiyor
  - Ana cümle tek seferde öğrenme loglarında görünüyor

- **Tab ile tamamlanan cümlelerin eksik algılanması sorunu düzeltildi**
  - KeyboardHookService'e AddTabCompletedTextToSentenceBuffer metodu eklendi
  - Tab ile eklenen metinler sentence buffer'a senkronize ediliyor
  - Modern interface-based çözüm implementasyonu

- **Tab sonrası manuel boşluk algılama sorunu düzeltildi**
  - Manuel olarak eklenen boşluklar artık doğru algılanıyor
  - Kelimeler birleşik olarak öğrenilmiyor
  - Akıllı boşluk algılama sistemi

### 🔧 Geliştirmeler
- **Detaylı debug logging sistemi eklendi**
  - Frekans güncelleme logları
  - Tab öğrenme süreç logları
  - Öneri algoritması debug bilgileri
  - "kalma" ile başlayan kelimelerin özel takibi

- **Aktif öğrenme sistemi debug araçları**
  - Kelime frekanslarının gerçek zamanlı takibi
  - Frekans karşılaştırma logları
  - Öneri algoritması şeffaflığı

### 🏗️ Teknik İyileştirmeler
- Modern event-driven architecture
- Thread-safe implementasyonlar
- Interface-based service design
- Improved error handling ve logging

## [1.2.2] - 2024-XX-XX
### Önceki sürüm değişiklikleri...

## [1.2.1] - 2024-XX-XX
### Önceki sürüm değişiklikleri...

## [1.2.0] - 2024-XX-XX
### Önceki sürüm değişiklikleri...
