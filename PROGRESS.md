# Çalışma Günlüğü

### [2026-11-02 - bilgisayar adı public repoda yazılmadı]
- **Yapılanlar & Kararlar:**
  - "Kur" sırasında kurulum başarısız olursa veya hiç başlamazsa uygulama artık yeniden açılıyor ve bir kez, `update-install.log` dosyasını gösteren uyarı veriyor. Doğru kurulumda uyarı çıkmıyor.
  - Güncelleme kutusu yeniden açılınca denetim hemen yapılıyor.
  - Sürüm hattındaki kurulum sınaması log tutuyor, başarısızlıkta logları saklıyor, sabit süre yerine uygulamanın hazır olmasını bekliyor. Yeniden deneme eklenmedi, hiçbir denetim kaldırılmadı.
  - Bu iş PR #7'de taslak olarak duruyor; yeni sürüm yayımlanmadı, merge ve yayın sahibinin onayına bağlı.
  - Sınama: https://github.com/berkantshn/falcreb-cloud-backup/actions/runs/41435566271 (110 denetim, yerelde üretilen test kurulumlarıyla, geçici anahtarla, sahte veriyle).
- **Son Commit:** 61ca72a ci: log installs and wait for app readiness in the release check
- **Aktif Branch:** fix/update-failure-and-release-gate
- **Sıradaki İlk İş (Next Action):** PR #7'yi incele; onaylanırsa merge et, ardından yeni sürüm kararını ver. `test/e2e-update-failure` branch'inin silinmesine karar ver.
- **Bilinen Sorunlar / Dikkat Edilmesi Gerekenler:**
  - Üstüne kurulumdaki kod 5'in asıl sebebi bulunamadı: yeniden üretilemedi, CPU yükü altında da çıkmadı. Tekrar çıkarsa artık log saklanacak.
  - Windows'un çalıştıramadığı bir kurulum dosyasında bazen kendi hata kutusu çıkıyor; uygulama o kutu kapatılınca açılıyor.
  - Gerçek yayın anahtarı ve gerçek masaüstünde UAC istemiyle henüz denenmedi; kullanıcılar bu düzeltmeyi ancak yeni bir sürümle alır ve v1.5.1'deki eski "Kur" akışıyla geçer.

### [2026-11-02 - bilgisayar adı public repoda yazılmadı]
- **Yapılanlar & Kararlar:**
  - v1.5.1 imzalı olarak yayınlandı (küçük düzeltme: `check-live` yönetici olmayan kabukta anlaşılır hata veriyor).
  - "Kur" bağlantısı gerçek bir güncellemede denendi ve çalıştı: GitHub'ın geçici Windows makinesinde v1.5.0 kuruldu, sahte verilerle açıldı, bağlantıya gerçek fare ile tıklandı, PIN girildi, uygulama v1.5.1 olarak kendiliğinden yeniden açıldı (Türkçe/açık ve İngilizce/koyu).
  - Görevler, arayüz ayarları, bildirim ayarları, kota kaydı ve PIN dosyası bayt bayt aynı kaldı; zamanlanmış görevlerin XML kayıtları değişmedi; uygulama logunda hata yok.
  - Windows'un varsayılan UAC ayarıyla da güncelleme zincirinde onay penceresi çıkmadı.
  - Oynanmış güncelleme reddedildi: bozulan her alan, başka anahtarla imza, özeti tutmayan ve beklenenden büyük dosya. Özel anahtar kullanılmadı.
  - Onay kutusu dört dil/tema birleşiminde ve en küçük pencerede temiz görünüyor; kapalıyken 3 dakikada bildirim çıkmadı.
  - Test koşusu: https://github.com/berkantshn/falcreb-cloud-backup/actions/runs/41424994013 — betikler main'e alınmadı, `test/e2e-update` branch'inde duruyor.
- **Son Commit:** 28fe783 Merge pull request #5 from berkantshn/chore/release-1-5-1
- **Aktif Branch:** main
- **Sıradaki İlk İş (Next Action):** `test/e2e-update` branch'ini silme kararını ver (merge edilmedi, onay gerekiyor). Ardından gerçek Google/OneDrive girişi ve yüklemeyi dene.
- **Bilinen Sorunlar / Dikkat Edilmesi Gerekenler:**
  - Sürüm hattındaki "üstüne kurulum sınaması" aralıklı başarısız oluyor: bu dokümantasyon PR'ında 3 denemenin 2'sinde kurulum kod 5 ile çıktı, üçüncüsü geçti. Başarısız denemelerde makine belirgin yavaştı; sebep kanıtlanmadı çünkü o adım kurulum logu tutmuyor. Aynı adımlar loglu olarak 3 kez, yayındaki kurulum dosyası açık uygulamanın üstüne 16 kez sorunsuz çalıştı. Bir sürüm etiketinde tekrarlanırsa yayın durur, hiçbir şey yayımlanmaz; öneri: adıma `/LOG` eklemek ve pencerenin açılmasını beklemek.
  - Sessiz kurulum "Kur" sırasında bir sebeple başarısız olursa uygulama çoktan kapanmış olur ve kendiliğinden açılmaz; iz yalnızca `update-install.log` dosyasında kalır (koddan görüldü, testlerde yaşanmadı).
  - Test CI makinesinde yapıldı: oturum baştan yönetici, UAC onayını gören gerçek bir kullanıcı yok, gerçek bulut hesabı yok, ekran 1024x768.
  - İndirilen kurulum dosyası (yaklaşık 4 MB) `%ProgramData%\FalcrebCloudBackup\updates` altında bir sonraki güncellemeye kadar kalıyor.
  - Kutu kapalıyken uygulama açılıp sonra kutu açılırsa denetim hemen yapılmıyor; bir sonraki günlük denetimi veya yeniden açılışı bekliyor (koddan görüldü, çalışırken denenmedi).
  - En küçük pencerede (800x520), sorun şeridi ve bildirim görünürken görev listesi tek satırın altına iniyor; kaydırılabiliyor, taşma yok.
  - "Yedekleme sürerken güncelleme" uyarısı bu testte denenmedi.
  - Gerçek Google/OneDrive girişi, yükleme ve zamanlanmış çalışma henüz denenmedi. Kurulum dosyasında Authenticode kod imzası yok.

### [2026-11-02 - bilgisayar adı public repoda yazılmadı]
- **Yapılanlar & Kararlar:**
  - v1.5.0 imzalı olarak yayınlandı; testler, kurulum dosyası, imzalama ve sessiz kurulum sınaması GitHub Actions'ta geçti.
  - Yayındaki dosyanın SHA-256 özeti `SHA256SUMS.txt` ile eşleşti.
  - İmzalama anahtarı GitHub secret'ı oldu; açık değeri repoda yok.
  - Depo public olduğu için hostname bu dosyaya yazılmıyor; gerçek değer özel Drive yedeğinin `backup-info.json` dosyasında.
- **Son Commit:** 389ec01 Merge pull request #1 from berkantshn/feature/signed-updates-and-release
- **Aktif Branch:** main
- **Sıradaki İlk İş (Next Action):** Yönetici PowerShell'inde `ReleaseSigner check-live 1.4.0` ile yayındaki sürümün indirme ve imza doğrulamasını tamamla.
- **Bilinen Sorunlar / Dikkat Edilmesi Gerekenler:**
  - `check-live` indirdiği dosyayı `%ProgramData%\FalcrebCloudBackup\updates` altına yazar, yönetici ister. Yönetici olmayan bir denemeden boş bir alt klasör kalmış olabilir; uygulama sonraki indirmede eski klasörleri temizler.
  - Uygulamadaki "Kur" bağlantısı gerçek bir güncellemede denenmedi. Plan: 1.5.0'ı denemelik makineye kur, küçük bir 1.5.1 yayınla, "Kur"a bas; görevlerin, ayarların ve zamanlanmış görevlerin korunduğunu doğrula. Uygulamadan önce onay alınır.
  - Ana penceredeki güncelleme onay kutusunun görünümü kontrol edilmedi.
  - Gerçek Google/OneDrive girişi, yükleme ve zamanlanmış çalışma henüz denenmedi.
  - Kurulum dosyasında Authenticode kod imzası yok; Windows "bilinmeyen yayıncı" uyarısı verebilir. Güncelleme imzası bu uyarıyı gidermez.
