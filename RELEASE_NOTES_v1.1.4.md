# 🚀 Text Expander v1.1.4 Release Notes

## ✨ Yeni Özellikler

### 🎯 PNG Görsel Tanıma Genişletildi
- **Döküman Editörü** pencerelerinde `imlec.png` görsel tanıma artık aktif
- Önceden sadece `.UDF` dosyaları için çalışıyordu
- Artık `"Döküman Editörü"` kelimesi geçen tüm pencereler destekleniyor
- Örnek: "Doküman Editörü v5.4.14 - 175274109411312479736648" başlıklı pencereler

### 🔄 Pencere Odak Sorunu Çözüldü
- Kısayol önizleme penceresi senkronize edildiğinde yaşanan sorun giderildi
- Döküman Editörü kapatılıp açıldığında önizleme penceresi otomatik olarak geri geliyor
- Akıllı pencere odak algılama sistemi eklendi
- `WindowHelper.WindowFocusChanged` event sistemi ile gerçek zamanlı takip

## 🔧 Teknik İyileştirmeler

### Kod Değişiklikleri
- `ImageRecognitionService.cs`: Döküman Editörü desteği eklendi
- `PreviewOverlay.xaml.cs`: Gelişmiş pencere algılama
- `MainViewModel.cs`: `OnWindowFocusChanged` event handler eklendi
- `WindowHelper.cs`: Window focus monitoring sistemi

### Performans Optimizasyonları
- Single File Deployment (117MB)
- Self-Contained .NET runtime
- ReadyToRun önceden derlenmiş kod
- Compressed binary
- Debug bilgileri kaldırılmış

## 📋 Sistem Gereksinimleri

- **İşletim Sistemi:** Windows 10/11 (64-bit)
- **RAM:** Minimum 4GB (8GB önerilen)
- **Disk Alanı:** ~120MB
- **.NET Runtime:** Dahil (Self-contained)

## 📦 İndirme

### Windows x64
- **Dosya:** `TextExpander_v1.1.4_Windows_x64.zip`
- **Boyut:** 106.61 MB (sıkıştırılmış)
- **İçerik:** 
  - `OtomatikMetinGenisletici.exe` (117MB)
  - `imlec.png` (130 bytes)
  - `README.md`

## 🚀 Kurulum

1. ZIP dosyasını indirin ve çıkarın
2. `OtomatikMetinGenisletici.exe` dosyasını çalıştırın
3. İlk çalıştırmada Windows Defender uyarısı çıkabilir - "Yine de çalıştır" seçin
4. Uygulama sistem tepsisinde çalışmaya başlar

## 🧪 Test Senaryosu

1. **Kısayol önizleme panelini senkronize edin**
2. **Döküman Editörü'nde metin yazın** (önizleme penceresi görünür)
3. **Döküman Editörü'nü kapatın** (önizleme penceresi gizlenir)
4. **Döküman Editörü'nü tekrar açın** (önizleme penceresi otomatik olarak geri gelir) ✅

## 🐛 Düzeltilen Sorunlar

- ✅ Döküman Editörü pencerelerinde PNG görsel tanıma çalışmıyordu
- ✅ Pencere kapatılıp açıldığında önizleme penceresi geri gelmiyordu
- ✅ Window focus tracking eksikti

## 🔄 Önceki Sürümden Yükseltme

- Eski sürümü kapatın
- Yeni sürümü indirin ve çalıştırın
- Ayarlar ve kısayollar otomatik olarak korunur

## 📞 Destek

Sorun yaşarsanız:
1. Uygulamayı yönetici olarak çalıştırmayı deneyin
2. Windows Defender/Antivirüs istisna listesine ekleyin
3. GitHub Issues'da sorun bildirin

---

**Geliştirici:** @saffetcelik  
**Derleme Tarihi:** 17.07.2025  
**Platform:** Windows x64  
**Commit:** 284095a
