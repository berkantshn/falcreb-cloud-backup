# Çalışma Günlüğü

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
