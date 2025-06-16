# Text Expander Projesine Katkıda Bulunma Rehberi

Text Expander'ı daha iyi bir araç haline getirmemize yardımcı olduğunuz için teşekkür ederiz! Katkılarınız, projenin gelişimi için çok değerlidir.

Bu rehber, katkıda bulunma sürecini herkes için kolay ve şeffaf hale getirmeyi amaçlamaktadır.

## 💬 Başlamadan Önce

Bir özellik eklemek veya bir hatayı düzeltmek için büyük bir çalışma yapmadan önce, lütfen **Issues** bölümünü kontrol edin veya yeni bir tane oluşturun. Bu, aynı konu üzerinde mükerrer çalışma yapılmasını önler ve önerilen değişikliğin projenin hedefleriyle uyumlu olup olmadığını en başta netleştirir.

- **Hata Bildirimi (Bug Report)**: Eğer bir hata bulduysanız, lütfen hatayı yeniden oluşturmak için adımları, beklenen davranışı ve gerçekleşen davranışı içeren detaylı bir issue açın.
- **Özellik İsteği (Feature Request)**: Yeni bir özellik veya mevcut bir özellikte bir geliştirme önermek istiyorsanız, lütfen isteğinizin arkasındaki mantığı ve potansiyel kullanım senaryolarını açıklayan bir issue açın.

## 🚀 Katkı Süreci

1.  **Fork & Clone**: Projeyi kendi GitHub hesabınıza fork'layın ve ardından yerel makinenize klonlayın.
    ```bash
    git clone https://github.com/KULLANICI_ADINIZ/Text-Expander.git
    cd Text-Expander
    ```

2.  **Yeni Bir Branch Oluşturun**: Değişikliklerinizi yapacağınız, açıklayıcı bir isme sahip yeni bir branch oluşturun.
    ```bash
    # Hata düzeltmesi için:
    git checkout -b fix/hata-aciklamasi

    # Yeni özellik için:
    git checkout -b feature/ozellik-aciklamasi
    ```

3.  **Değişikliklerinizi Yapın**: Kodu düzenleyin, yeni özellikler ekleyin veya hataları düzeltin. Lütfen projenin mevcut kod stiline ve mimarisine (MVVM, DI) uymaya özen gösterin.

4.  **Commit Mesajları**: Anlamlı ve açıklayıcı commit mesajları yazın. Yaptığınız değişikliğin **neden** yapıldığını ve **ne** yaptığını özetleyin.
    ```bash
    git commit -m "feat: Akıllı öneriler için bağlam analizi eklendi"
    ```

5.  **Push Edin**: Değişikliklerinizi GitHub'daki fork'unuza gönderin.
    ```bash
    git push origin feature/ozellik-aciklamasi
    ```

6.  **Pull Request (PR) Açın**: GitHub üzerinden `saffetcelik/Text-Expander` reposunun `main` branch'ine yönelik bir Pull Request oluşturun. PR açıklamasında yaptığınız değişiklikleri detaylı bir şekilde açıklayın ve ilgili issue numarasını (`Fixes #123` gibi) belirtin.

## 📝 Kodlama Standartları

- **Dil**: Proje C# ile geliştirilmiştir. Lütfen .NET 8 standartlarına ve modern C# özelliklerine sadık kalın.
- **Stil**: Proje genelinde tutarlı bir kod stili hedeflenmektedir. Lütfen mevcut kodun formatına ve isimlendirme kurallarına uyun.
- **Mimari**: Proje MVVM (Model-View-ViewModel) ve Dependency Injection desenlerini kullanmaktadır. Lütfen yeni ekleyeceğiniz bileşenleri bu mimariye uygun şekilde tasarlayın.

Katkılarınız için şimdiden teşekkürler!
