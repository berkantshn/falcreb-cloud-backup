# Changelog

Bu proje [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) formatını takip eder.

## [Unreleased]

### Doğrulananlar
- "Kur" bağlantısı v1.5.0'dan v1.5.1'e gerçek bir güncellemede, sahte verilerle GitHub'ın geçici Windows makinesinde sınandı; görevler, ayarlar ve zamanlanmış görevler korundu, oynanmış güncelleme reddedildi.
- Ana penceredeki güncelleme onay kutusunun görünümü İngilizce/Türkçe, açık/koyu temada denetlendi.

## [1.5.1] - 2026-11-02

### Düzeltilenler
- Sürüm denetim aracı (`check-live`) yönetici olmayan bir kabukta artık hiçbir şey indirmeden anlaşılır bir hata veriyor; geride boş klasör bırakmıyor.

### Eklenenler
- Sürüm durumu ve doğrulanmamış işlerin planı `PROGRESS.md` dosyasına yazıldı.

## [1.5.0] - 2026-11-02

### Eklenenler
- **İmzalı güncelleme**: Yeni sürüm çıkınca uygulama haber verir; imzası, boyutu ve özeti doğrulanan güncelleme tek tıkla ve PIN ile kurulur. Süren yedekleme kesilmez, günlük denetim kapatılabilir.
- İndirilebilir kurulum dosyası: her sürüm GitHub Releases'te hazır olarak yayımlanır.
- Tarayıcıdan gezilebilen uygulama turu.

### Açık kaynak hazırlığı
- Kurulum paketi artık OAuth kimliği içermiyor; her kullanıcı kendi Google ve Microsoft uygulama kimliğini README'deki adımlarla ekliyor.
- MIT lisansı eklendi, destek kanalı GitHub Issues oldu, gizlilik ve kullanım koşulları sayfaları kaldırıldı; "Made By FALCREB" ibaresi eklendi.
- README'ye sahte verili, İngilizce ve Türkçe ekran görüntüleri eklendi.
- Depo adı `falcreb-cloud-backup` oldu; GitHub bağlantıları buna göre güncellendi (çözüm ve proje adları aynı kaldı).
- Logo ve simgelerdeki Google Drive işareti kaldırıldı; `app.ico` tüm boyutlarıyla yeniden üretildi.

### Eklenenler
- **İngilizce arayüz**: Uygulama artık İngilizce ve Türkçe. Windows dili Türkçeyse Türkçe, değilse İngilizce açılır; ana penceredeki dil seçimi hemen uygulanır ve kaydedilir. Bildirimler, hata mesajları, loglar ve zamanlanmış çalışmalar da kayıtlı dili kullanır.
- **Bulut alanı göstergesi**: Ana ekranda ve Bulut Bağlantıları penceresinde, her hesabın ne kadarının dolduğunu gösteren bir çubuk. Değer her başarılı yüklemede kaydedilir. %85'i geçince uyarı (tepsi balonu + e-posta/Telegram), %95'te kritik. Telegram/e-posta bildirimlerinde de kalan alan yazıyor.
- **Açılış telafi görevi**: Sunucu her açıldığında, 10 dakika sonra arayüz açılmadan "kaçırılmış yedekleme var mı?" kontrolü yapılır. Kimse Windows'a giriş yapmasa bile çalışır; kaçırılmış bir şey yoksa hiçbir şey yapmaz.
- **Kaçırılan yedeklemenin telafisi**: Sunucu zamanlanmış saatte kapalıysa (elektrik kesintisi, bakım), uygulama açıldıktan birkaç dakika sonra yedekleme kendiliğinden alınır — bir sonraki güne kadar beklenmez. Görev penceresinden kapatılabilir.
- **Ayarlar kilidi**: Ana ekranın sağ üstündeki kilit düğmesiyle bir kez doğrulama yapılıp 3 dakika boyunca PIN/doğrulayıcı sorulmadan ayar değiştirilebiliyor. Düğmede kalan süre geri sayıyor; her yetkili işlemde süre yenileniyor. Elle tıklayarak, süre dolduğunda, pencere küçültüldüğünde veya kapatıldığında kendiliğinden kilitleniyor.
- **Firebird (Wolvox) veritabanları için gbak desteği**: kaynakta .fdb/.gdb bulunduğunda ham dosya kopyası yerine Firebird'ün kendi aracıyla tutarlı, geri yüklenebilir yedek alınır — Wolvox açıkken bile çalışır. gbak otomatik bulunur, görev penceresinden yol/kullanıcı/şifre ayarlanabilir.
- **Bildirimler**: e-posta (SMTP) ve/veya Telegram ile başarısız yedekleme, uzun süredir yedek alınmaması ve isteğe bağlı başarı bildirimi; "Test Gönder" ile doğrulama.
- **Görev sağlık durumu**: ana ekranda her görev için ✓ / ✕ / ⚠ durumu, son başarılı yedek zamanı ve sorun şeridi; tepsi balonuyla uyarı. Liste 30 saniyede bir kendini yeniler (gece çalışan yedeklerin sonucu açık pencerede de görünür).
- Geçmişte her çalışma için süre ve boyut bilgisi; sürüm numarası ana ekranda.

### Değişenler
- **Google token'ı artık şifreli**: Yenileme token'ı düz JSON yerine DPAPI (LocalMachine) ile şifreli saklanıyor. Eski düz metin dosya ilk okumada şifreliye taşınıyor ve şifreli kopya doğrulandıktan sonra siliniyor; okunamayan bir dosyada yeniden bağlanmak gerekiyor.
- Zamanlanmış görevlere 4 saatlik süre sınırı, hata sonrası 1 saat arayla 2 tekrar ve normal işlem önceliği eklendi; sessiz çalışma kendi içinde 3 saatte durur ve sonucu geçmişe yazar.
- Yedekleme çalışmaları artık hiçbir koşulda tarayıcı açmaz; bulut oturumu geçersizse "yeniden bağlanın" hatasıyla biter (tarayıcı beklerken sonsuza kadar takılma sorunu).
- OneDrive/Google Drive yüklemelerinde parça başına 10 dakika zaman aşımı, 5 kez yeniden deneme ve kaldığı yerden devam; doğrulama indirmesi bellek yerine geçici dosyaya yapılır.
- Zip dosya adı artık rastgele kod yerine tarih-saat içeriyor; her aşama log dosyasına yazılıyor.
- Ortak ayar dosyaları (görevler, geçmiş) süreçler arası kilitle ve atomik yazımla güncelleniyor; bozuk geçmiş dosyası silinmek yerine kenara alınıyor.
- Görev düzenlerken gece çalışmasının yazdığı "son çalışma" bilgileri artık ezilmiyor.
- Dosya izleme canlı veritabanı dosyalarındaki (.fdb) sürekli değişiklikleri yok sayıyor.

### Düzeltilenler
- Düğme, metin kutusu, şifre kutusu ve açılır liste MahApps görünümünü kaybediyordu (koyu temada düğmeler açık gri zemin üzerinde beyaz yazıyla neredeyse okunmuyordu). Stiller doğru temele bağlandı.
- Uygulama her açılışta zamanlanmış görevi yeniden kaydediyordu; bu işlem Windows'un "kaçırılan çalışmayı ilk fırsatta telafi et" kaydını siliyordu. Artık yalnızca görevde gerçekten bir değişiklik varsa yeniden kaydediliyor.
- Yarım kalan çalışmalardan (elektrik kesintisi, çökme) artakalan yüzlerce MB'lık geçici klasörler hiç silinmiyordu; artık her açılışta 24 saatten eski olanlar temizleniyor.
- Bulut hesabına bağlanırken tarayıcı açılıp anında kapandığında (uygulama yönetici yetkisiyle çalıştığı için, normal kullanıcı olarak açık bir tarayıcı varsa oluyor) pencere sonsuza kadar kilitli kalıyordu. Artık giriş adresi ekranda gösteriliyor ve kopyalanabiliyor, "Vazgeç" düğmesi var ve 5 dakika sonra kendiliğinden sonlanıyor.
- Bir bulut bağlantısı kaldırıldığında, o bulutu hedef alan görevler eskisi gibi kalıyor ve her çalışmada "bağlı değil" hatası veriyordu. Artık bağlantı kaldırılırken hangi görevlerin etkilendiği gösteriliyor ve o hedef görevlerden de çıkarılabiliyor.
- Yedekleme sürerken "uzun süredir başarılı yedek yok" uyarısının yanlışlıkla gönderilmesi; çalışan görev artık listede "Şu anda yedekleniyor..." olarak gösteriliyor.
- Takılı kalan bir gece yedeklemesinin sonraki günlerin yedeklerini sessizce engellemesi.
- Geçmiş penceresinde uzun hata mesajlarının yatay kaydırmayla kesilmesi.
- Ana ekrandaki "Son çalışma" bilgisinin uygulama açık kaldığı sürece güncellenmemesi.
- Başarısız bir çalışmanın geçici zip dosyasını Temp klasöründe bırakması.

## [1.0.1] - 2026-08-27

### Eklenenler
- Yerel yedekleme motoru: sıkıştırma (zip), SHA-256 bütünlük doğrulama, zaman damgalı klasörler (üzerine yazma yok).
- Google Drive ve OneDrive'a gerçek bulut entegrasyonu — bir görev aynı anda her ikisine de yükleyebilir.
- Windows Task Scheduler ile zamanlanmış yedekleme ve isteğe bağlı anlık dosya izleme (throttle korumalı).
- PIN ve/veya authenticator (TOTP) ile esnek kimlik doğrulama; kurtarma kodu ile PIN sıfırlama.
- Yedekleme geçmişi ve log görüntüleme ekranları.
- Windows başlangıcında otomatik başlatma seçeneği.
- Inno Setup ile kurulum paketi.

### Değişenler
- Uygulama artık açılışta kilit ekranı göstermiyor — zamanlanmış/izlenen yedeklemeler kesintisiz sürsün diye kimlik doğrulama, hassas işlemler (görev ekleme/düzenleme, güvenlik ayarları, çıkış) öncesinde ayrıca isteniyor.

### Düzeltilenler
- OneDrive girişinin Windows Server'da (Internet Explorer Gelişmiş Güvenlik Yapılandırması nedeniyle) açılmaması sorunu — sistem tarayıcısı kullanılacak şekilde düzeltildi.
- Aynı anda birden fazla uygulama kopyası açıldığında yinelenen yedeklemelere yol açan hata — tek kopya koruması eklendi.
- Yinelenen yedeklemeyi önlemek için eklenen kilit (mutex) mekanizmasının, her yedekleme sonrası uygulamayı çökertmesi.
- "Windows başlangıcında başlat" özelliğinin görünmez (tepsi simgesiz) bir kopya açmasına neden olan oturum türü hatası.
