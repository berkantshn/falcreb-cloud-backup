using System.Collections.Generic;

namespace FalcrebCloudBackup.Core.Localization
{
    /// <summary>
    /// Tüm görünen metinlerin tek tablosu: anahtar, İngilizce, Türkçe. İkisi aynı satırda durduğu
    /// için bir dilin unutulması mümkün değildir. {0}, {1} yer tutucuları string.Format biçimindedir.
    /// </summary>
    internal static class Strings
    {
        private static readonly Dictionary<string, string[]> Table = Build();

        public static string Get(string key, string language)
        {
            if (!Table.TryGetValue(key, out var values))
            {
                return key;
            }

            return language == Loc.Turkish ? values[1] : values[0];
        }

        public static IEnumerable<string> Keys => Table.Keys;

        private static void Add(Dictionary<string, string[]> t, string key, string en, string tr) => t.Add(key, new[] { en, tr });

        private static Dictionary<string, string[]> Build()
        {
            var t = new Dictionary<string, string[]>();
            AddCommon(t);
            AddMainWindow(t);
            AddJobEdit(t);
            AddCloudConnections(t);
            AddNotificationSettings(t);
            AddSecurity(t);
            AddHistoryAndLogs(t);
            AddHealth(t);
            AddEngine(t);
            AddNotifications(t);
            AddProviders(t);
            AddSchedulingAndStorage(t);
            AddAppLifecycle(t);
            return t;
        }

        private static void AddCommon(Dictionary<string, string[]> t)
        {
            Add(t, "Common_Cancel", "Cancel", "İptal");
            Add(t, "Common_Save", "Save", "Kaydet");
            Add(t, "Common_Close", "Close", "Kapat");
            Add(t, "Common_Refresh", "Refresh", "Yenile");
            Add(t, "Common_Warning", "Warning", "Uyarı");
            Add(t, "Common_Success", "Success", "Başarılı");
            Add(t, "Common_AllFiles", "All files", "Tüm dosyalar");
            Add(t, "Common_UnknownError", "unknown error", "bilinmeyen hata");

            Add(t, "Day_Monday", "Monday", "Pazartesi");
            Add(t, "Day_Tuesday", "Tuesday", "Salı");
            Add(t, "Day_Wednesday", "Wednesday", "Çarşamba");
            Add(t, "Day_Thursday", "Thursday", "Perşembe");
            Add(t, "Day_Friday", "Friday", "Cuma");
            Add(t, "Day_Saturday", "Saturday", "Cumartesi");
            Add(t, "Day_Sunday", "Sunday", "Pazar");

            Add(t, "Time_Day_One", "{0} day", "{0} gün");
            Add(t, "Time_Day_Other", "{0} days", "{0} gün");
            Add(t, "Time_Hour_One", "{0} hour", "{0} saat");
            Add(t, "Time_Hour_Other", "{0} hours", "{0} saat");
            Add(t, "Time_Minute_One", "{0} minute", "{0} dakika");
            Add(t, "Time_Minute_Other", "{0} minutes", "{0} dakika");

            Add(t, "Duration_HoursMinutes", "{0} h {1} min", "{0} sa {1} dk");
            Add(t, "Duration_MinutesSeconds", "{0} min {1} s", "{0} dk {1} sn");
            Add(t, "Duration_Seconds", "{0} s", "{0} sn");
            Add(t, "Size_Bytes", "{0} bytes", "{0} bayt");

            Add(t, "Result_Success", "succeeded", "başarılı");
            Add(t, "Result_Failed", "failed", "başarısız");
            Add(t, "Result_FailedWithReason", "failed: {0}", "başarısız — {0}");
        }

        private static void AddMainWindow(Dictionary<string, string[]> t)
        {
            Add(t, "Tray_Open", "Open", "Aç");
            Add(t, "Tray_Exit", "Exit", "Çıkış");
            Add(t, "Tray_AllGood", "FalcrebCloudBackup: all good", "FalcrebCloudBackup — her şey yolunda");
            Add(t, "Tray_Problems_One", "FalcrebCloudBackup: problem in {0} job", "FalcrebCloudBackup — {0} görevde sorun var");
            Add(t, "Tray_Problems_Other", "FalcrebCloudBackup: problems in {0} jobs", "FalcrebCloudBackup — {0} görevde sorun var");

            Add(t, "Main_AutoStart", "Start with Windows", "Windows başlangıcında başlat");
            Add(t, "Main_CheckUpdates", "Check for new versions daily", "Her gün yeni sürümü denetle");
            Add(t, "Update_Available", "New version available: v{0}.", "Yeni sürüm var: v{0}.");
            Add(t, "Update_Install", "Install", "Kur");
            Add(t, "Update_Downloading", "Downloading and verifying v{0}...", "v{0} indiriliyor ve doğrulanıyor...");
            Add(t, "Update_Confirm", "Install v{0} now? The app closes during the update and reopens when it finishes. Your jobs and settings are kept.", "v{0} şimdi kurulsun mu? Güncelleme sırasında uygulama kapanır, bitince yeniden açılır. Görevleriniz ve ayarlarınız korunur.");
            Add(t, "Update_BackupRunning", "A backup is running. Try the update again when it finishes.", "Şu an bir yedekleme çalışıyor. Bitince güncellemeyi yeniden deneyin.");
            Add(t, "Update_Failed", "The update could not be installed and nothing was changed: {0}", "Güncelleme kurulamadı, hiçbir şey değiştirilmedi: {0}");
            Add(t, "Log_UpdateCheckFailed", "Update check failed: {0}", "Güncelleme denetimi başarısız: {0}");
            Add(t, "Log_UpdateSettingSaveFailed", "Could not save the update setting: {0}", "Güncelleme ayarı kaydedilemedi: {0}");
            Add(t, "Log_UpdateInstalling", "Verified update v{0}; starting the installer.", "v{0} güncellemesi doğrulandı, kurulum başlatılıyor.");
            Add(t, "Update_FailedAfterRestart", "The last update could not be installed. The app is still on v{0}, and your jobs and settings were not changed. Details: {1}. You can try again when the update is offered.", "Son güncelleme kurulamadı. Uygulama v{0} sürümünde çalışmaya devam ediyor; görevleriniz ve ayarlarınız değişmedi. Ayrıntılar: {1}. Güncelleme önerildiğinde yeniden deneyebilirsiniz.");
            Add(t, "Log_UpdateFailedAfterRestart", "The last update could not be installed; the app is on v{0}.", "Son güncelleme kurulamadı; uygulama v{0} sürümünde.");
            Add(t, "Log_UpdateAttemptStoreFailed", "Could not read or write the update record: {0}", "Güncelleme kaydı okunamadı veya yazılamadı: {0}");
            Add(t, "Log_UpdateFailed", "Update failed.", "Güncelleme başarısız.");
            Add(t, "Main_AutoStartError", "Could not change the auto-start setting: {0}", "Otomatik başlatma ayarlanırken bir sorun oluştu: {0}");
            Add(t, "Main_ThemeDark", "Dark Theme", "Koyu Tema");
            Add(t, "Main_ThemeLight", "Light Theme", "Açık Tema");
            Add(t, "Main_Locked", "Locked", "Kilitli");
            Add(t, "Main_Unlocked", "Unlocked · {0}", "Açık · {0}");
            Add(t, "Main_UnlockedTip",
                "Settings are unlocked, so your PIN or authenticator code won't be requested again. Click to lock now. Locks automatically when the time runs out or when the window is minimized or closed.",
                "Ayarlar kilidi açık — PIN/doğrulayıcı tekrar sorulmayacak. Hemen kilitlemek için tıklayın. Süre dolduğunda, pencere küçültüldüğünde veya kapatıldığında kendiliğinden kilitlenir.");
            Add(t, "Main_LockedTip",
                "Settings are locked. Click to unlock: verify once and you won't be asked again for {0:0} minutes.",
                "Ayarlar kilitli. Açmak için tıklayın: bir kez doğrulama yapılır, {0:0} dakika boyunca tekrar sorulmaz.");

            Add(t, "Main_JobsTitle", "Backup Jobs", "Yedekleme Görevleri");
            Add(t, "Main_NewJob", "New Job", "Yeni Görev");
            Add(t, "Main_Edit", "Edit", "Düzenle");
            Add(t, "Main_Remove", "Remove", "Kaldır");
            Add(t, "Main_RunNow", "Run Now", "Şimdi Çalıştır");
            Add(t, "Main_CloudConnections", "Cloud Connections", "Bulut Bağlantıları");
            Add(t, "Main_Notifications", "Notifications", "Bildirimler");
            Add(t, "Main_History", "History", "Geçmiş");
            Add(t, "Main_Logs", "Logs", "Loglar");
            Add(t, "Main_Security", "Security", "Güvenlik");
            Add(t, "Main_Language", "Language", "Dil");

            Add(t, "Main_ListLoadFailed", "Could not read the job list: {0}", "Görev listesi okunamadı: {0}");
            Add(t, "Main_Subtitle_Empty", "No backup jobs yet. Click \"New Job\" to get started.", "Henüz bir yedekleme görevi tanımlanmadı. \"Yeni Görev\" ile başlayın.");
            Add(t, "Main_Subtitle_Ok_One", "{0} job configured. All good.", "{0} görev tanımlı. Her şey yolunda.");
            Add(t, "Main_Subtitle_Ok_Other", "{0} jobs configured. All good.", "{0} görev tanımlı. Her şey yolunda.");
            Add(t, "Main_Subtitle_Problems_One", "{0} job configured. Problems in {1}.", "{0} görev tanımlı, {1} tanesinde sorun var.");
            Add(t, "Main_Subtitle_Problems_Other", "{0} jobs configured. Problems in {1}.", "{0} görev tanımlı, {1} tanesinde sorun var.");
            Add(t, "Main_QuotaRow", "{0} / {1} · {2} free", "{0} / {1} · {2} boş");

            Add(t, "Quota_AlertCritical", "{0} is almost full", "{0} alanı bitmek üzere");
            Add(t, "Quota_AlertLow", "{0} is running low on space", "{0} alanı azalıyor");
            Add(t, "Quota_AlertBody",
                "{0}\n\nServer: {1}\n\nThe app never deletes backups, so used space grows every day. Once the space is full, backup uploads will start to fail. Free up space in your cloud account or upgrade your storage plan.",
                "{0}\n\nSunucu: {1}\n\nUygulama hiçbir yedeği silmez, bu yüzden kullanılan alan her gün artar. Alan tamamen dolduğunda yedekleme yüklemeleri başarısız olmaya başlar. Bulut hesabınızdan yer açın ya da depolama planınızı yükseltin.");

            Add(t, "Banner_OneProblem", "Problem with job '{0}'", "'{0}' görevinde sorun var");
            Add(t, "Banner_ManyProblems", "{0} jobs have problems", "{0} görevde sorun var");

            Add(t, "Stale_Title", "'{0}': {1}", "'{0}' — {1}");
            Add(t, "Stale_Body",
                "{0}.\n{1}\n\nServer: {2}\nSource: {3}\n\nOpen the app and check the \"History\" and \"Logs\" screens. If needed, use \"Run Now\" to back up manually.",
                "{0}.\n{1}\n\nSunucu: {2}\nKaynak: {3}\n\nUygulamayı açıp \"Geçmiş\" ve \"Loglar\" ekranlarını kontrol edin; gerekirse \"Şimdi Çalıştır\" ile elle yedek alın.");
            Add(t, "CatchUp_Balloon", "'{0}': missed backup started", "'{0}' — kaçırılan yedekleme başlatıldı");
            Add(t, "Run_FailedBalloon", "'{0}' backup failed", "'{0}' yedeklemesi başarısız");
            Add(t, "Run_UnexpectedError", "Unexpected error: {0}", "Beklenmedik hata: {0}");

            Add(t, "Job_RemoveConfirm",
                "Remove job '{0}'? This only deletes the job definition. Backups already uploaded to the cloud are not affected.",
                "'{0}' görevi kaldırılsın mı? Bu yalnızca görev tanımını siler, daha önce buluta yüklenmiş yedekleri etkilemez.");
            Add(t, "Job_RemoveTitle", "Remove Job", "Görevi Kaldır");
            Add(t, "Job_RemoveTaskError", "There was a problem removing the scheduled task: {0}", "Zamanlanmış görev kaldırılırken bir sorun oluştu: {0}");
            Add(t, "Job_SyncError",
                "The job was saved, but registering the scheduled task with Windows failed:\n{0}\n\nMake sure you are running the app as administrator.",
                "Görev kaydedildi, ancak zamanlanmış görev Windows'a kaydedilirken bir sorun oluştu:\n{0}\n\nUygulamayı yönetici olarak çalıştırdığınızdan emin olun.");

            Add(t, "Progress_Title", "Backing Up", "Yedekleniyor");
            Add(t, "Progress_Running", "Backing up '{0}'...", "'{0}' yedekleniyor...");
            Add(t, "Progress_StartFailed", "The backup could not start: {0}", "Yedekleme başlatılamadı: {0}");
            Add(t, "Progress_AllOk", "Backed up to all destinations successfully.", "Tüm hedeflere başarıyla yedeklendi.");
            Add(t, "Progress_SomeFailed", "The backup failed for some destinations. See the details above.", "Yedekleme bazı hedeflerde başarısız oldu — yukarıdaki ayrıntılara bakın.");
            Add(t, "Progress_Done", "Backup Complete", "Yedekleme tamamlandı");
            Add(t, "Progress_Failed", "Backup Failed", "Yedekleme başarısız");

            Add(t, "Log_Lock_ClosedManually", "Settings lock closed manually.", "Ayarlar kilidi elle kapatıldı.");
            Add(t, "Log_Lock_Opened", "Settings unlocked ({0:0} minutes).", "Ayarlar kilidi açıldı ({0:0} dakika).");
            Add(t, "Log_Lock_ClosedHidden", "Settings locked (window hidden).", "Ayarlar kilidi kapatıldı (pencere gizlendi).");
            Add(t, "Log_Lock_Expired", "Settings lock expired and closed automatically.", "Ayarlar kilidi süre dolduğu için kendiliğinden kapandı.");
            Add(t, "Log_BackfillFailed", "Could not fill in status info for older job records.", "Eski görev kayıtları için durum bilgisi doldurulamadı.");
            Add(t, "Log_JobListReadFailed", "Could not read the job list.", "Görev listesi okunamadı.");
            Add(t, "Log_QuotaReadFailed", "Could not read cloud storage info: {0}", "Bulut alan bilgisi okunamadı: {0}");
            Add(t, "Log_QuotaAlertReadFailed", "Could not read cloud storage info for the low-space alert: {0}", "Alan uyarısı için bulut bilgisi okunamadı: {0}");
            Add(t, "Log_QuotaAlertFailed", "Could not send the low-space alert.", "Alan uyarısı gönderilemedi.");
            Add(t, "Log_QuotaAlert", "{0}: {1}", "{0} — {1}");
            Add(t, "Log_HealthJobsReadFailed", "Could not read the job list for the health check.", "Sağlık kontrolü için görev listesi okunamadı.");
            Add(t, "Log_StaleWarning", "Warning: '{0}': {1}. {2}", "Uyarı: '{0}' — {1}. {2}");
            Add(t, "Log_CatchUpStarting", "Catching up a missed backup: '{0}': {1}.", "Kaçırılan yedekleme telafi ediliyor: '{0}' — {1}.");
            Add(t, "Log_CatchUpFailed", "The catch-up backup ended with an unexpected error: '{0}'", "Telafi yedeklemesi beklenmedik bir hatayla bitti: '{0}'");
            Add(t, "Log_ManualRunFailed", "Manual run of '{0}' ended with an unexpected error.", "'{0}' elle çalıştırma beklenmedik hatayla bitti.");
            Add(t, "Log_LanguageSaveFailed", "Could not save the language setting: {0}", "Dil ayarı kaydedilemedi: {0}");
            Add(t, "Log_LanguageLoadFailed", "Could not read the language setting, using the system default: {0}", "Dil ayarı okunamadı, sistem varsayılanı kullanılıyor: {0}");
        }

        private static void AddJobEdit(Dictionary<string, string[]> t)
        {
            Add(t, "JobEdit_Title", "Backup Job", "Yedekleme Görevi");
            Add(t, "JobEdit_Name", "Job Name", "Görev Adı");
            Add(t, "JobEdit_Source", "Folder or File to Back Up", "Yedeklenecek Klasör/Dosya");
            Add(t, "JobEdit_PickFolder", "Choose Folder", "Klasör Seç");
            Add(t, "JobEdit_PickFile", "Choose File", "Dosya Seç");
            Add(t, "JobEdit_SendTo", "Send to Cloud", "Buluta Gönder");
            Add(t, "JobEdit_GoogleFolder", "Google Drive folder name (the job name is used if left empty)", "Google Drive klasör adı (boş bırakılırsa görev adı kullanılır)");
            Add(t, "JobEdit_OneDriveFolder", "OneDrive folder name (the job name is used if left empty)", "OneDrive klasör adı (boş bırakılırsa görev adı kullanılır)");
            Add(t, "JobEdit_Schedule", "Schedule", "Zamanlama");
            Add(t, "JobEdit_FreqHourly", "Every hour", "Her saat");
            Add(t, "JobEdit_FreqDaily", "Every day", "Her gün");
            Add(t, "JobEdit_FreqWeekly", "One day a week", "Haftada bir gün");
            Add(t, "JobEdit_Day", "Day:", "Gün:");
            Add(t, "JobEdit_Time", "Time:", "Saat:");
            Add(t, "JobEdit_Options", "Options", "Seçenekler");
            Add(t, "JobEdit_Enabled", "Job enabled", "Görev etkin");
            Add(t, "JobEdit_Watch", "Back up right away when a new file appears", "Yeni dosya oluşunca hemen yedekle");
            Add(t, "JobEdit_WatchHint",
                "Constant changes to live database files (.fdb) are not watched. Databases are backed up by the scheduled run.",
                "Canlı veritabanı dosyalarındaki (.fdb) sürekli değişiklikler izlenmez; veritabanı için zamanlanmış çalışma kullanılır.");
            Add(t, "JobEdit_CatchUp", "Catch up on missed backups", "Kaçırılan yedeklemeyi telafi et");
            Add(t, "JobEdit_CatchUpHint",
                "If the server is off at the scheduled time (a power outage, for example), the backup runs automatically a few minutes after the app starts.",
                "Sunucu zamanlanmış saatte kapalıysa (elektrik kesintisi vb.), uygulama açıldıktan birkaç dakika sonra yedekleme kendiliğinden alınır.");
            Add(t, "JobEdit_Zip", "Compress before uploading (zip)", "Yüklemeden önce sıkıştır (zip)");
            Add(t, "JobEdit_Verify", "Verify integrity after upload (the file is downloaded and compared)", "Yükleme sonrası bütünlük doğrula (dosya geri indirilip karşılaştırılır)");
            Add(t, "JobEdit_Timestamp", "Upload to a timestamped folder (never overwrite)", "Zaman damgalı klasöre yükle (üzerine yazma)");

            Add(t, "JobEdit_DestSetupMissing", "setup incomplete", "kurulum eksik");
            Add(t, "JobEdit_DestConnected", "Connected ✓", "Bağlı ✓");
            Add(t, "JobEdit_DestNotConnected", "Not connected. Connect under Cloud Connections.", "Bağlı değil — Bulut Bağlantıları'ndan bağlanın");
            Add(t, "JobEdit_SourceSize_One", "{0} file, {1}", "{0} dosya, {1}");
            Add(t, "JobEdit_SourceSize_Other", "{0} files, {1}", "{0} dosya, {1}");
            Add(t, "JobEdit_SourceSizeError", "Could not calculate the size: {0}", "Boyut hesaplanamadı: {0}");

            Add(t, "Firebird_Title", "Firebird Database", "Firebird Veritabanı");
            Add(t, "Firebird_InfoOne",
                "This source contains a Firebird database ({0}). A live database file can't be copied while the program is open, and even a closed one may copy inconsistently. gbak always produces a restorable backup.",
                "Bu kaynakta bir Firebird veritabanı var ({0}). Canlı veritabanı dosyası program açıkken kopyalanamaz, kapalıyken bile tutarsız çıkabilir; gbak ise her durumda geri yüklenebilir bir yedek üretir.");
            Add(t, "Firebird_InfoMany",
                "This source contains {0} Firebird databases. Live database files can't be copied while the program is open, and even closed ones may copy inconsistently. gbak always produces a restorable backup.",
                "Bu kaynakta {0} Firebird veritabanı var. Canlı veritabanı dosyaları program açıkken kopyalanamaz, kapalıyken bile tutarsız çıkabilir; gbak ise her durumda geri yüklenebilir bir yedek üretir.");
            Add(t, "Firebird_GbakCheck", "Back up databases consistently with gbak (recommended)", "Veritabanlarını gbak ile tutarlı yedekle (önerilir)");
            Add(t, "Firebird_GbakPath", "gbak.exe location", "gbak.exe konumu");
            Add(t, "Firebird_Find", "Find", "Bul");
            Add(t, "Firebird_Browse", "Browse", "Gözat");
            Add(t, "Firebird_User", "Firebird user name", "Firebird kullanıcı adı");
            Add(t, "Firebird_Password", "Firebird password", "Firebird şifresi");
            Add(t, "Firebird_DefaultsHint",
                "Wolvox installations usually use SYSDBA / masterkey. If you haven't changed them, leave them as they are.",
                "Wolvox kurulumlarında genellikle SYSDBA / masterkey kullanılır; değiştirmediyseniz olduğu gibi bırakın.");
            Add(t, "Firebird_GbakNotFoundStatus",
                "gbak.exe not found. Is Firebird installed? Use \"Find\" to search again or \"Browse\" to select it yourself (usually under C:\\Program Files\\Firebird\\...).",
                "gbak.exe bulunamadı — Firebird kurulu mu? \"Bul\" ile tekrar arayın ya da \"Gözat\" ile elle seçin (genellikle C:\\Program Files\\Firebird\\... altında).");
            Add(t, "Firebird_GbakFound", "gbak found ✓", "gbak bulundu ✓");
            Add(t, "Firebird_GbakNotHere", "There is no gbak.exe at this location.", "Bu konumda gbak.exe yok.");
            Add(t, "Firebird_GbakNotFoundMessage",
                "gbak.exe could not be found automatically. Use \"Browse\" to select it from the folder where the Firebird server is installed (for example C:\\Program Files\\Firebird\\Firebird_2_5).",
                "gbak.exe otomatik bulunamadı. Firebird sunucusunun kurulu olduğu klasörü (örn. C:\\Program Files\\Firebird\\Firebird_2_5) \"Gözat\" ile seçebilirsiniz.");
            Add(t, "Firebird_GbakNotFoundTitle", "gbak not found", "gbak bulunamadı");

            Add(t, "JobEdit_ErrNameEmpty", "The job name can't be empty.", "Görev adı boş olamaz.");
            Add(t, "JobEdit_ErrNoSource", "Select a folder or file.", "Bir klasör veya dosya seçin.");
            Add(t, "JobEdit_ErrSourceMissing", "The selected source was not found: {0}", "Seçilen kaynak bulunamadı: {0}");
            Add(t, "JobEdit_ErrNoDestination", "Select at least one cloud destination.", "En az bir bulut hedefi seçin.");
            Add(t, "JobEdit_ErrNoFrequency", "Select how often to run the schedule.", "Bir zamanlama sıklığı seçin.");
            Add(t, "JobEdit_ErrGbak",
                "gbak.exe not found. Select it from the Firebird install folder, or turn off the gbak option (not recommended).",
                "gbak.exe bulunamadı. Firebird kurulum klasöründen seçin ya da gbak seçeneğini kapatın (önerilmez).");
        }

        private static void AddCloudConnections(Dictionary<string, string[]> t)
        {
            Add(t, "Cloud_Title", "Cloud Connections", "Bulut Bağlantıları");
            Add(t, "Cloud_Disconnect", "Disconnect", "Kaldır");
            Add(t, "Cloud_Connect", "Connect", "Bağlan");
            Add(t, "Cloud_Reconnect", "Reconnect", "Yeniden Bağlan");
            Add(t, "Cloud_AuthWaitTitle", "Waiting for sign-in in your browser", "Tarayıcıda giriş bekleniyor");
            Add(t, "Cloud_AuthWaitHint",
                "If the browser didn't open, or closed right away, copy the address below and paste it into a browser you open yourself. This window updates on its own once you've signed in.",
                "Tarayıcı açılmadıysa ya da açılır açılmaz kapandıysa: aşağıdaki adresi kopyalayın ve elle açtığınız bir tarayıcıya yapıştırın. Giriş tamamlanınca bu pencere kendiliğinden güncellenir.");
            Add(t, "Cloud_Copy", "Copy", "Kopyala");
            Add(t, "Cloud_CancelConnect", "Cancel", "Vazgeç");
            Add(t, "Cloud_RefreshQuota", "Refresh Cloud Storage", "Bulut Alanını Yenile");
            Add(t, "Cloud_QuotaAsOf", "  ·  as of {0}", "  ·  {0} itibarıyla");
            Add(t, "Cloud_QuotaFetching", "Getting cloud storage info...", "Bulut alanı bilgisi alınıyor...");
            Add(t, "Cloud_QuotaUpdated", "Cloud storage info updated.", "Bulut alanı bilgisi güncellendi.");
            Add(t, "Cloud_SetupMissing", "Setup incomplete: credentials file not found", "Kurulum eksik — kimlik dosyası bulunamadı");
            Add(t, "Cloud_GoogleSetupInfo",
                "Copy the OAuth client file you downloaded from Google Cloud Console to:\n{0}",
                "Google Cloud Console'dan indirdiğiniz OAuth istemci dosyasını şu konuma kopyalayın:\n{0}");
            Add(t, "Cloud_Connected", "Connected", "Bağlı");
            Add(t, "Cloud_NotConnected", "Not connected", "Bağlı değil");
            Add(t, "Cloud_WaitingForBrowser", "Waiting for {0} sign-in in your browser...", "Tarayıcıda {0} girişi bekleniyor...");
            Add(t, "Cloud_ConnectedInfo", "{0} connected.", "{0} bağlantısı kuruldu.");
            Add(t, "Cloud_NotCompleted", "Connection not completed", "Bağlantı tamamlanmadı");
            Add(t, "Cloud_Aborted",
                "Sign-in ended before it finished. If the browser won't open, close all open browser windows and try again, or click \"Connect\" and paste the address shown into a browser yourself.",
                "Giriş tamamlanmadan sonlandırıldı. Tarayıcı açılmıyorsa: açık olan tüm tarayıcı pencerelerini kapatıp tekrar deneyin ya da \"Bağlan\" deyip görünen adresi kopyalayarak elle bir tarayıcıya yapıştırın.");
            Add(t, "Cloud_Failed", "Connection failed", "Bağlantı başarısız");
            Add(t, "Cloud_UrlCopied", "Address copied. Open a browser and paste it into the address bar.", "Adres kopyalandı — bir tarayıcı açıp adres çubuğuna yapıştırın.");
            Add(t, "Cloud_UrlCopyFailed", "Could not copy the address to the clipboard ({0}). You can select and copy it from the box yourself.", "Adres panoya kopyalanamadı ({0}). Kutudaki adresi elle seçip kopyalayabilirsiniz.");

            Add(t, "Cloud_RemotePhrase_Google", "on Drive", "Drive'daki");
            Add(t, "Cloud_RemotePhrase_OneDrive", "on OneDrive", "OneDrive'daki");
            Add(t, "Cloud_DisconnectTitle", "Disconnect", "Bağlantıyı Kaldır");
            Add(t, "Cloud_DisconnectBase",
                "Disconnect {0}? This only forgets the saved sign-in on this computer. Your backups {1} are not affected. You can connect again with a different account.",
                "{0} bağlantısı kaldırılsın mı? Bu, yalnızca bu bilgisayardaki kayıtlı girişi unutur — {1} yedekleriniz etkilenmez. Farklı bir hesapla tekrar bağlanabilirsiniz.");
            Add(t, "Cloud_DisconnectJobs",
                "These jobs use {0} as a destination: {1}\nIf you disconnect, they will keep reporting a {0} error on every run.\n\n",
                "Şu görevler {0} hedefini kullanıyor: {1}\nBağlantıyı kaldırırsanız bu görevler her çalışmada {0} için hata vermeye devam eder.\n\n");
            Add(t, "Cloud_DisconnectOptionsRemovable",
                "• Yes: disconnect and also remove {0} from these jobs\n• No: only disconnect and leave the jobs unchanged (choose this if you'll reconnect right away to switch accounts)\n• Cancel: keep the connection",
                "• Evet — bağlantıyı kaldır ve {0} hedefini bu görevlerden de çıkar\n• Hayır — sadece bağlantıyı kaldır, görev tanımlarına dokunma (hesabı değiştirmek için hemen yeniden bağlanacaksanız bunu seçin)\n• İptal — vazgeç");
            Add(t, "Cloud_DisconnectOptionsSimple",
                "• Yes: disconnect (the jobs stay unchanged)\n• No: keep the connection",
                "• Evet — bağlantıyı kaldır (görev tanımları değişmez)\n• Hayır — vazgeç");
            Add(t, "Cloud_DisconnectNote",
                "\n\nNote: {0} only backs up to {1}. A job with no destination can't back up, so edit it to choose another cloud destination or disable it.",
                "\n\nNot: {0} görevinin tek hedefi {1}. Hedefsiz bir görev yedekleme yapamaz; bu görevi düzenleyip başka bir bulut hedefi seçin ya da görevi devre dışı bırakın.");

            Add(t, "Log_ConnectAborted", "{0} sign-in ended before it finished (canceled or timed out).", "{0} girişi tamamlanmadan sonlandırıldı (vazgeçildi ya da süre doldu).");
            Add(t, "Log_ConnectFailed", "Could not connect to {0}.", "{0} bağlantısı kurulamadı.");
            Add(t, "Log_DisconnectJobsReadFailed", "Could not read the job list while disconnecting.", "Bağlantı kaldırılırken görev listesi okunamadı.");
            Add(t, "Log_DestinationRemoved", "Removed {1} as a destination from job '{0}' (the connection was removed).", "'{0}' görevinden {1} hedefi çıkarıldı (bağlantı kaldırıldığı için).");
        }

        private static void AddNotificationSettings(Dictionary<string, string[]> t)
        {
            Add(t, "NotifyWin_Title", "Notifications", "Bildirimler");
            Add(t, "NotifyWin_Intro",
                "Lets you know when a backup fails or when no backup has succeeded for a long time. When nobody is at the server, this is the only way to notice a problem.",
                "Bir yedekleme başarısız olduğunda ya da uzun süre hiç başarılı yedek alınmadığında size haber verir. Sunucu başında kimse yokken bir sorunun fark edilmesinin tek yolu budur.");
            Add(t, "NotifyWin_When", "When should you be notified?", "Ne zaman bildirilsin?");
            Add(t, "NotifyWin_OnFailure", "When a backup fails", "Yedekleme başarısız olduğunda");
            Add(t, "NotifyWin_WhenStale", "When no backup succeeds in the expected time (at most once a day)", "Beklenen sürede başarılı yedek alınmadığında (günde en fazla bir kez)");
            Add(t, "NotifyWin_OnSuccess", "Also after every successful backup", "Her başarılı yedeklemede de haber ver");
            Add(t, "NotifyWin_EmailEnabled", "Notify by email", "E-posta ile bildir");
            Add(t, "NotifyWin_Provider", "Provider", "Sağlayıcı");
            Add(t, "NotifyWin_PresetM365", "Microsoft 365 (business)", "Microsoft 365 (kurumsal)");
            Add(t, "NotifyWin_PresetCustom", "Custom SMTP", "Özel SMTP");
            Add(t, "NotifyWin_SmtpHost", "SMTP server", "SMTP sunucusu");
            Add(t, "NotifyWin_Tls", "Encrypted (TLS)", "Şifreli (TLS)");
            Add(t, "NotifyWin_User", "User name (email)", "Kullanıcı adı (e-posta)");
            Add(t, "NotifyWin_Password", "Password / app password", "Şifre / uygulama şifresi");
            Add(t, "NotifyWin_From", "From address (the user name is used if left empty)", "Gönderen adres (boşsa kullanıcı adı kullanılır)");
            Add(t, "NotifyWin_To", "To address(es): separate multiple addresses with semicolons", "Alıcı adres(ler) — birden fazlaysa noktalı virgülle ayırın");
            Add(t, "NotifyWin_EmailHint",
                "Gmail and Outlook don't allow SMTP sign-in with your regular account password. Create an \"app password\" in your account settings and enter that here. SSL port 465 isn't supported; use 587.",
                "Gmail ve Outlook, normal hesap şifresiyle SMTP girişine izin vermez: hesap ayarlarından bir \"uygulama şifresi\" oluşturup buraya onu yazın. 465 numaralı SSL portu desteklenmez; 587 kullanın.");
            Add(t, "NotifyWin_TelegramEnabled", "Notify by Telegram", "Telegram ile bildir");
            Add(t, "NotifyWin_TelegramHint",
                "Create a bot with @BotFather on Telegram and get its token. Then send the bot a message and get your own Chat ID from @userinfobot.",
                "Telegram'da @BotFather ile bir bot oluşturup token'ı alın. Sonra bota bir mesaj gönderin ve @userinfobot'tan kendi Chat ID'nizi öğrenin.");
            Add(t, "NotifyWin_Test", "Send Test", "Test Gönder");

            Add(t, "NotifyWin_ErrHost", "The SMTP server can't be empty.", "SMTP sunucusu boş olamaz.");
            Add(t, "NotifyWin_ErrTo", "Enter at least one recipient email address.", "En az bir alıcı e-posta adresi girin.");
            Add(t, "NotifyWin_ErrFrom", "Enter a user name or a from address.", "Kullanıcı adı ya da gönderen adres girin.");
            Add(t, "NotifyWin_ErrPort465", "SSL port 465 isn't supported. Use 587 (TLS).", "465 numaralı SSL portu desteklenmiyor; 587 (TLS) kullanın.");
            Add(t, "NotifyWin_ErrToken", "The Telegram bot token can't be empty.", "Telegram bot token'ı boş olamaz.");
            Add(t, "NotifyWin_ErrChat", "The Telegram Chat ID can't be empty.", "Telegram Chat ID boş olamaz.");
            Add(t, "NotifyWin_ErrNoChannel", "Turn on email or Telegram notifications first.", "Önce e-posta ya da Telegram bildirimini açın.");
            Add(t, "NotifyWin_TestSending", "Sending test notification...", "Test bildirimi gönderiliyor...");
            Add(t, "NotifyWin_TestSent", "Test notification sent ✓ Check your inbox or Telegram.", "Test bildirimi gönderildi ✓ — gelen kutunuzu / Telegram'ı kontrol edin.");
            Add(t, "NotifyWin_TestFailed", "Could not send: {0}", "Gönderilemedi: {0}");
        }

        private static void AddSecurity(Dictionary<string, string[]> t)
        {
            Add(t, "Pin_SetupPrompt", "Set a PIN to protect the app", "Uygulamayı korumak için bir PIN belirleyin");
            Add(t, "Pin_New", "New PIN", "Yeni PIN");
            Add(t, "Pin_NewConfirm", "New PIN (again)", "Yeni PIN (tekrar)");
            Add(t, "Pin_Create", "Create PIN", "PIN Oluştur");
            Add(t, "Pin_TotpLabel", "Authenticator code (6 digits)", "Authenticator kodu (6 haneli)");
            Add(t, "Pin_Unlock", "Unlock", "Kilidi Aç");
            Add(t, "Pin_CantSignIn", "I can't sign in", "Giriş yapamıyorum");
            Add(t, "Pin_RecoverPrompt", "Enter the recovery code you were shown during setup", "Kurulumda size gösterilen kurtarma kodunu girin");
            Add(t, "Pin_RecoveryCode", "Recovery code (e.g. 7F3K9-QRT4M-XW2LP)", "Kurtarma kodu (ör. 7F3K9-QRT4M-XW2LP)");
            Add(t, "Pin_RecoverNew", "New PIN to use from now on", "Bundan sonra kullanılacak yeni PIN");
            Add(t, "Pin_Reset", "Reset PIN", "PIN'i Sıfırla");
            Add(t, "Pin_Back", "Back", "Geri");
            Add(t, "Pin_ErrTooShort", "The PIN must be at least 4 digits.", "PIN en az 4 haneli olmalı.");
            Add(t, "Pin_ErrMismatch", "The PINs don't match.", "Girilen PIN'ler eşleşmiyor.");
            Add(t, "Pin_RecoveryMessage",
                "If you forget your PIN (or lose your authenticator), you'll use this code to get back in. Save it somewhere safe now, because it won't be shown again:\n\n{0}",
                "PIN'inizi unutursanız (veya authenticator'ınızı kaybederseniz) hesabınıza tekrar erişmek için bu kodu kullanacaksınız. Şimdi güvenli bir yere kaydedin — bu kod bir daha gösterilmeyecek:\n\n{0}");
            Add(t, "Pin_RecoveryTitle", "Your Recovery Code", "Kurtarma Kodunuz");
            Add(t, "Pin_ErrWrong", "Incorrect details. Please try again.", "Bilgiler hatalı, tekrar deneyin.");
            Add(t, "Pin_ErrNewTooShort", "The new PIN must be at least 4 digits.", "Yeni PIN en az 4 haneli olmalı.");
            Add(t, "Pin_ErrRecovery", "The recovery code could not be verified.", "Kurtarma kodu doğrulanamadı.");
            Add(t, "Pin_ResetDone",
                "Your PIN has been updated. The authenticator (if any) was turned off. You can set it up again in the Security settings.",
                "PIN'iniz güncellendi. Authenticator (varsa) kapatıldı — isterseniz Güvenlik ayarlarından yeniden kurabilirsiniz.");

            Add(t, "Sec_Title", "Security Settings", "Güvenlik Ayarları");
            Add(t, "Sec_PinEnabled", "Sign in with PIN", "PIN ile giriş");
            Add(t, "Sec_TotpTitle", "Sign in with authenticator (2FA)", "Authenticator ile giriş (2FA)");
            Add(t, "Sec_Enable", "Enable", "Etkinleştir");
            Add(t, "Sec_Remove", "Remove", "Kaldır");
            Add(t, "Sec_ScanPrompt", "Scan this code with an app such as Google Authenticator or Microsoft Authenticator:", "Google Authenticator, Microsoft Authenticator gibi bir uygulamayla bu kodu okutun:");
            Add(t, "Sec_ManualKey", "If you can't scan the code, enter this key manually:", "Kod okutamıyorsanız bu anahtarı elle girin:");
            Add(t, "Sec_ConfirmPrompt", "Enter the 6-digit code shown in the app to confirm:", "Uygulamada görünen 6 haneli kodu girip onaylayın:");
            Add(t, "Sec_CodeWatermark", "6-digit code", "6 haneli kod");
            Add(t, "Sec_Confirm", "Confirm", "Onayla");
            Add(t, "Sec_TotpOn", "Enabled ✓", "Etkin ✓");
            Add(t, "Sec_TotpOff", "Not enabled", "Etkin değil");
            Add(t, "Sec_TotpEnabledMessage", "Authenticator sign-in has been enabled.", "Authenticator ile giriş etkinleştirildi.");
            Add(t, "Sec_ErrCode", "The code could not be verified. Please try again.", "Kod doğrulanamadı, tekrar deneyin.");
            Add(t, "Sec_RemoveConfirm", "Are you sure you want to remove authenticator sign-in?", "Authenticator ile girişi kaldırmak istediğinize emin misiniz?");
            Add(t, "Sec_RemoveTitle", "Remove Authenticator", "Authenticator'ı Kaldır");
            Add(t, "Auth_ErrDisableTotp", "The authenticator can't be removed while the PIN is off. Enable the PIN first.", "PIN kapalıyken authenticator kaldırılamaz — önce PIN'i etkinleştirin.");
            Add(t, "Auth_ErrDisablePin", "The PIN can't be turned off while the authenticator isn't enabled. At least one sign-in method must stay on.", "Authenticator etkin değilken PIN kapatılamaz — en az bir giriş yöntemi açık kalmalı.");
        }

        private static void AddHistoryAndLogs(Dictionary<string, string[]> t)
        {
            Add(t, "History_Title", "Backup History", "Yedekleme Geçmişi");
            Add(t, "History_Empty", "No backups have run yet.", "Henüz hiç yedekleme çalışmadı.");
            Add(t, "History_Summary_One", "{0} run · {1} succeeded · {2} failed (newest first)", "{0} çalışma · {1} başarılı · {2} başarısız (en yeni üstte)");
            Add(t, "History_Summary_Other", "{0} runs · {1} succeeded · {2} failed (newest first)", "{0} çalışma · {1} başarılı · {2} başarısız (en yeni üstte)");
            Add(t, "History_Error", "Error: {0}", "Hata: {0}");
            Add(t, "History_Duration", "Duration: {0}", "Süre: {0}");
            Add(t, "History_Size", " · Size: {0}", " · Boyut: {0}");

            Add(t, "Logs_Title", "Logs", "Loglar");
            Add(t, "Logs_Empty", "There is no log file yet.", "Henüz bir log dosyası yok.");
            Add(t, "Logs_OpenFolder", "Open Log Folder", "Log Klasörünü Aç");
        }

        private static void AddHealth(Dictionary<string, string[]> t)
        {
            Add(t, "Health_Disabled", "Job disabled", "Görev devre dışı");
            Add(t, "Health_DisabledDetail", "Won't run automatically until it's enabled.", "Etkinleştirilene kadar otomatik çalışmaz.");
            Add(t, "Health_LastSuccess", "Last successful backup: {0}", "Son başarılı yedek: {0}");
            Add(t, "Health_NoSuccess", "No successful backup yet", "Hiç başarılı yedek yok");
            Add(t, "Health_Running", "Backing up now...", "Şu anda yedekleniyor...");
            Add(t, "Health_StaleSince", "No successful backup for {0}", "{0} boyunca başarılı yedek alınamadı");
            Add(t, "Health_StaleNever", "Job created {0} ago, no successful backup yet", "Görev {0} önce oluşturuldu, hiç başarılı yedek yok");
            Add(t, "Health_LastError", "Last error: {0}", "Son hata: {0}");
            Add(t, "Health_LastRunStale", "Last run: {0}. The scheduled task may not have run since then.", "Son çalışma: {0} — zamanlanmış görev o günden beri çalışmamış olabilir.");
            Add(t, "Health_NeverRanTask", "The scheduled task doesn't appear to have ever run.", "Zamanlanmış görev hiç çalışmamış görünüyor.");
            Add(t, "Health_LastFailed", "Last backup failed", "Son yedekleme başarısız");
            Add(t, "Health_ManualOnly", "Runs manually only", "Sadece elle çalışır");
            Add(t, "Health_ManualOnlyDetail", "Schedule and file watching are off.", "Zamanlama ve dosya izleme kapalı.");
            Add(t, "Health_NeverRan", "Hasn't run yet", "Henüz çalışmadı");
            Add(t, "Job_LastRun", "Last run: {0}", "Son çalışma: {0}");

            Add(t, "Schedule_WatchOnly", "Runs only when files change", "Sadece dosya değişince çalışır");
            Add(t, "Schedule_Off", "Schedule off", "Zamanlama kapalı");
            Add(t, "Schedule_Hourly", "Every hour, on the hour", "Her saat başı");
            Add(t, "Schedule_Weekly", "Every {0} at {1}", "Her {0} {1}");
            Add(t, "Schedule_Daily", "Every day at {0}", "Her gün {0}");
        }

        private static void AddEngine(Dictionary<string, string[]> t)
        {
            Add(t, "Engine_AlreadyRunning", "A backup is already running for this job, so this one was skipped.", "Bu görev için zaten bir yedekleme çalışıyor, atlandı.");
            Add(t, "Engine_Prepared_One", "Prepared: {0}, {1} file ({2}).", "Hazırlandı: {0}, {1} dosya ({2}).");
            Add(t, "Engine_Prepared_Other", "Prepared: {0}, {1} files ({2}).", "Hazırlandı: {0}, {1} dosya ({2}).");
            Add(t, "Engine_TimedOut",
                "Backup stopped: it couldn't finish within the allowed time (the internet connection may be very slow or down).",
                "Yedekleme durduruldu — izin verilen süre içinde tamamlanamadı (internet çok yavaş ya da kesik olabilir).");
            Add(t, "Engine_NoProvider", "No connected provider found for '{0}'.", "'{0}' için bağlı bir sağlayıcı bulunamadı.");
            Add(t, "Engine_Connecting", "{0}: connecting...", "{0}: bağlanılıyor...");
            Add(t, "Engine_Uploading", "{0}: uploading...", "{0}: yükleniyor...");
            Add(t, "Engine_UploadDone", "{0}: upload complete ({1}).", "{0}: yükleme tamamlandı ({1}).");
            Add(t, "Engine_Verifying", "{0}: verifying integrity...", "{0}: bütünlük doğrulanıyor...");
            Add(t, "Engine_HashMismatch", "The uploaded file's hash doesn't match the local file. The upload may be corrupted.", "Yüklenen dosyanın hash değeri yerel dosyayla eşleşmiyor — yükleme bozuk olabilir.");
            Add(t, "Engine_Verified", "{0}: verified.", "{0}: doğrulandı.");
            Add(t, "Engine_DestTimedOut", "Stopped because the time ran out.", "Süre dolduğu için durduruldu.");
            Add(t, "Engine_DestFailed", "{0}: failed: {1}", "{0}: başarısız — {1}");
            Add(t, "Engine_SourceMissing", "Source not found: {0}", "Kaynak bulunamadı: {0}");
            Add(t, "Engine_SourceEmpty", "The source folder is empty, so there's nothing to back up.", "Kaynak klasör boş — yedeklenecek dosya yok.");
            Add(t, "Engine_GbakMissing",
                "Firebird gbak.exe not found. Select the gbak path in the job settings, or turn off Firebird backup for this job.",
                "Firebird gbak.exe bulunamadı. Görev ayarlarından gbak yolunu seçin ya da bu görevde Firebird yedeklemeyi kapatın.");
            Add(t, "Engine_GbakPlan_One", "{0} Firebird database found. A consistent backup will be taken with gbak ({1}).", "{0} Firebird veritabanı bulundu; gbak ile tutarlı yedek alınacak ({1}).");
            Add(t, "Engine_GbakPlan_Other", "{0} Firebird databases found. A consistent backup will be taken with gbak ({1}).", "{0} Firebird veritabanı bulundu; gbak ile tutarlı yedek alınacak ({1}).");
            Add(t, "Engine_RawCopyWarning_One",
                "Warning: {0} Firebird database will be copied raw. This fails with a lock error if the program is open, and the copy may be inconsistent even if it's closed. Turning on gbak in the job settings is recommended.",
                "Uyarı: {0} Firebird veritabanı ham kopyalanacak — program açıksa kilit hatası verir, kapalıysa bile tutarsız olabilir. Görev ayarlarından gbak'ı açmanız önerilir.");
            Add(t, "Engine_RawCopyWarning_Other",
                "Warning: {0} Firebird databases will be copied raw. This fails with a lock error if the program is open, and the copies may be inconsistent even if it's closed. Turning on gbak in the job settings is recommended.",
                "Uyarı: {0} Firebird veritabanı ham kopyalanacak — program açıksa kilit hatası verir, kapalıysa bile tutarsız olabilir. Görev ayarlarından gbak'ı açmanız önerilir.");
            Add(t, "Engine_FirebirdStarting", "Backing up Firebird database: {0}", "Firebird yedeği alınıyor: {0}");
            Add(t, "Engine_FirebirdDone", "Firebird backup complete: {0}.fbk ({1}, {2}).", "Firebird yedeği alındı: {0}.fbk ({1}, {2}).");
            Add(t, "Engine_Zipping", "Compressing...", "Sıkıştırılıyor...");
            Add(t, "Engine_ZipDone", "Compression complete ({0}).", "Sıkıştırma tamamlandı ({0}).");
            Add(t, "Engine_NoTempSpace",
                "Not enough free space on the temp folder's disk (at least {0} needed, {1} free). Path: {2}",
                "Geçici klasörün bulunduğu diskte yeterli boş alan yok (gerekli en az {0}, boş {1}). Yol: {2}");
            Add(t, "Engine_LowTempSpace", "Warning: low free space on the temp disk ({0}); the source is {1}.", "Uyarı: geçici diskte boş alan az ({0}); kaynak {1}.");

            Add(t, "Firebird_AttemptFailed", "Attempt with '{0}' failed (exit code {1}): {2}", "'{0}' ile deneme başarısız (çıkış kodu {1}): {2}");
            Add(t, "Firebird_BackupFailed",
                "Could not back up the Firebird database ({0}). Check that the Firebird service is running and that the user name and password in the job settings are correct. Details: {1}",
                "Firebird yedeği alınamadı ({0}). Firebird servisinin çalıştığını ve görev ayarlarındaki kullanıcı adı/şifrenin doğru olduğunu kontrol edin. Ayrıntı: {1}");
            Add(t, "Firebird_NoOutput", "(no output)", "(çıktı yok)");

            Add(t, "Friendly_Timeout", "Timed out: the operation didn't finish in the expected time (the internet connection may be slow or down).", "Zaman aşımı — işlem beklenen sürede tamamlanamadı (internet yavaş ya da kesik olabilir).");
            Add(t, "Friendly_Network", "Internet or server connection error: {0}", "İnternet/sunucu bağlantı hatası: {0}");
            Add(t, "Friendly_AccessDenied", "Access denied: {0}", "Erişim reddedildi: {0}");

            Add(t, "Zip_LockedDatabase",
                "The database file '{0}' is in use by another program (Wolvox/Firebird is open). Live database files can't be copied raw. Turn on the \"Back up databases consistently with gbak\" option in the job settings.",
                "'{0}' veritabanı dosyası başka bir program tarafından kullanılıyor (Wolvox/Firebird açık). Canlı veritabanı dosyaları ham kopyalanamaz — görev ayarlarında \"Firebird veritabanlarını gbak ile yedekle\" seçeneğini açın.");
            Add(t, "Zip_LockedFile",
                "The file '{0}' couldn't be read because another program is using it. Close that program and try again.",
                "'{0}' dosyası başka bir program tarafından kullanıldığı için okunamadı. Dosyayı kullanan programı kapatıp tekrar deneyin.");

            Add(t, "Log_TempCleanupFailed", "Could not clean up temp folders: {0}", "Geçici klasör temizliği yapılamadı: {0}");
            Add(t, "Log_TempItemDeleted", "Deleted a temp item left over from an interrupted run: {0}", "Yarım kalmış bir çalışmadan artakalan geçici öğe silindi: {0}");
            Add(t, "Log_TempItemDeleteFailed", "Could not delete temp item ({0}): {1}", "Geçici öğe silinemedi ({0}): {1}");
            Add(t, "Log_RunSkippedBusy", "Backup skipped: a run is already in progress for '{0}'.", "Yedekleme atlandı — '{0}' için zaten bir çalışma sürüyor.");
            Add(t, "Log_RunStarted", "Backup started: '{0}' (jobId={1}).", "Yedekleme başladı: '{0}' (jobId={1}).");
            Add(t, "Log_RunTimedOut", "Backup timed out or was canceled: '{0}'", "Yedekleme zaman aşımı/iptal: '{0}'");
            Add(t, "Log_RunStartFailed", "The backup could not start: '{0}'", "Yedekleme başlatılamadı: '{0}'");
            Add(t, "Log_DestOk", "OK", "OK");
            Add(t, "Log_DestError", "ERROR", "HATA");
            Add(t, "Log_RunFinished",
                "Backup finished: '{0}', overall result={1}, destinations=[{2}], duration={3:0.0}s.",
                "Yedekleme bitti: '{0}', genel sonuç={1}, hedefler=[{2}], süre={3:0.0}sn.");
            Add(t, "Log_NotifyFailed", "An error occurred while sending the notification.", "Bildirim gönderilirken hata oluştu.");
            Add(t, "Log_PersistRunFailed", "Could not save the last run info.", "Son çalışma bilgisi kaydedilemedi.");
            Add(t, "Log_DestUploadFailed", "{0} upload failed.", "{0} yüklemesi başarısız oldu.");
            Add(t, "Log_ProviderQuotaFailed", "{0}: could not get storage info: {1}", "{0} alan bilgisi alınamadı: {1}");
            Add(t, "Log_StagingCleanupFailed", "Could not delete the temp backup folder ({0}): {1}", "Geçici yedek klasörü silinemedi ({0}): {1}");
        }

        private static void AddNotifications(Dictionary<string, string[]> t)
        {
            Add(t, "Notify_Subject", "[FalcrebCloudBackup] '{0}' backup {1}: {2}", "[FalcrebCloudBackup] '{0}' yedeklemesi {1} — {2}");
            Add(t, "Notify_StatusOk", "succeeded", "başarılı");
            Add(t, "Notify_StatusFailed", "FAILED", "BAŞARISIZ");
            Add(t, "Notify_BodyOk", "The backup completed successfully.", "Yedekleme başarıyla tamamlandı.");
            Add(t, "Notify_BodyFailed", "The backup FAILED. Please check it.", "Yedekleme BAŞARISIZ oldu — lütfen kontrol edin.");
            Add(t, "Notify_Job", "Job: {0}", "Görev: {0}");
            Add(t, "Notify_Server", "Server: {0}", "Sunucu: {0}");
            Add(t, "Notify_Started", "Started: {0}", "Başlangıç: {0}");
            Add(t, "Notify_Duration", "Duration: {0}", "Süre: {0}");
            Add(t, "Notify_Size", "Size: {0}", "Boyut: {0}");
            Add(t, "Notify_Error", "Error: {0}", "Hata: {0}");
            Add(t, "Notify_Destinations", "Destinations:", "Hedefler:");
            Add(t, "Notify_SeeDetails", "For details, see the \"History\" and \"Logs\" screens in the app.", "Ayrıntılar için uygulamadaki \"Geçmiş\" ve \"Loglar\" ekranlarına bakın.");
            Add(t, "Notify_TestSubject", "[FalcrebCloudBackup] Test notification", "[FalcrebCloudBackup] Test bildirimi");
            Add(t, "Notify_TestBody",
                "This is a test notification.\n\nServer: {0}\nTime: {1}\n\nIf you received this message, your notification settings are working.",
                "Bu bir test bildirimidir.\n\nSunucu: {0}\nZaman: {1}\n\nBu mesajı aldıysanız bildirim ayarları doğru çalışıyor.");
            Add(t, "Notify_TestEmailError", "Email: {0}", "E-posta: {0}");

            Add(t, "Quota_UsedNoLimit", "{0} used (no storage limit on this account)", "{0} kullanıldı (hesapta alan sınırı yok)");
            Add(t, "Quota_Summary", "{0} of {1} used ({2:0}%) · {3} free", "{0} / {1} kullanıldı (%{2:0}) · {3} boş");
            Add(t, "Quota_Header", "Cloud storage:", "Bulut alanı:");
            Add(t, "Quota_CriticalSuffix", "  ← CRITICAL, almost out of space", "  ← KRİTİK, alan bitmek üzere");
            Add(t, "Quota_LowSuffix", "  ← running low on space", "  ← alan azalıyor");

            Add(t, "Err_SmtpHostEmpty", "The SMTP server is empty.", "SMTP sunucusu boş.");
            Add(t, "Err_ToEmpty", "The recipient email address is empty.", "Alıcı e-posta adresi boş.");
            Add(t, "Err_FromEmpty", "The sender email address is empty.", "Gönderen e-posta adresi boş.");
            Add(t, "Err_TelegramTokenEmpty", "The Telegram bot token is empty.", "Telegram bot token'ı boş.");
            Add(t, "Err_TelegramChatEmpty", "The Telegram chat ID is empty.", "Telegram chat id boş.");
            Add(t, "Err_TelegramApi", "Telegram API error ({0}): {1}", "Telegram API hatası ({0}): {1}");

            Add(t, "Log_EmailSent", "Notification email sent: {0}", "Bildirim e-postası gönderildi: {0}");
            Add(t, "Log_EmailFailed", "Could not send the notification email.", "Bildirim e-postası gönderilemedi.");
            Add(t, "Log_TelegramSent", "Telegram notification sent: {0}", "Telegram bildirimi gönderildi: {0}");
            Add(t, "Log_TelegramFailed", "Could not send the Telegram notification.", "Telegram bildirimi gönderilemedi.");
        }

        private static void AddProviders(Dictionary<string, string[]> t)
        {
            Add(t, "Google_NotConnected",
                "Google Drive isn't connected on this computer. Open the app and connect to Google Drive under \"Cloud Connections\".",
                "Google Drive hesabı bu bilgisayarda bağlı değil. Uygulamayı açıp \"Bulut Bağlantıları\" ekranından Google Drive'a bağlanın.");
            Add(t, "Google_SetupMissing",
                "Google Drive setup is incomplete. Copy the \"Desktop app\" OAuth client file you downloaded from Google Cloud Console to: {0}\nSee the project's README for step-by-step instructions.",
                "Google Drive kurulumu eksik: Google Cloud Console'dan indirdiğiniz \"Masaüstü uygulaması\" OAuth istemci dosyasını şu konuma kopyalayın: {0}\nAdım adım anlatım için projenin README dosyasına bakın.");
            Add(t, "Google_SessionInvalid",
                "The Google Drive session is no longer valid ({0}). Open the app and reconnect to Google Drive under \"Cloud Connections\".",
                "Google Drive oturumu artık geçerli değil ({0}). Uygulamayı açıp \"Bulut Bağlantıları\" ekranından Google Drive'a yeniden bağlanın.");
            Add(t, "Google_UploadFailed", "The Google Drive upload didn't finish after {0} attempts: {1}", "Google Drive yüklemesi {0} denemede tamamlanamadı: {1}");
            Add(t, "Google_VerifyDownloadFailed", "Could not download the Google Drive file for verification: {0}", "Google Drive dosyası doğrulama için indirilemedi: {0}");
            Add(t, "Google_NotConnectedInternal", "Not connected to Google Drive. Connect first.", "Google Drive'a bağlı değil — önce bağlantı kurulmalı.");

            Add(t, "OneDrive_NotConnected",
                "OneDrive isn't connected on this computer, or its session has expired. Open the app and reconnect to OneDrive under \"Cloud Connections\".",
                "OneDrive hesabı bu bilgisayarda bağlı değil ya da oturumu dolmuş. Uygulamayı açıp \"Bulut Bağlantıları\" ekranından OneDrive'a yeniden bağlanın.");
            Add(t, "OneDrive_SetupMissing",
                "OneDrive setup is incomplete. Create a JSON file containing the application (client) ID you registered in the Azure portal at: {0}\nExample content: {{ \"ClientId\": \"...\" }}\nSee the project's README for step-by-step instructions.",
                "OneDrive kurulumu eksik: Azure portalında oluşturduğunuz uygulama kimliğini (Client ID) içeren bir JSON dosyasını şu konuma koyun: {0}\nÖrnek içerik: {{ \"ClientId\": \"...\" }}\nAdım adım anlatım için projenin README dosyasına bakın.");
            Add(t, "OneDrive_ClientIdMissing", "The 'ClientId' field was not found in '{0}'.", "'{0}' dosyasında 'ClientId' alanı bulunamadı.");
            Add(t, "OneDrive_FolderFailed", "Could not create the OneDrive folder ({0}): {1}", "OneDrive klasörü oluşturulamadı ({0}): {1}");
            Add(t, "OneDrive_UploadFailed", "OneDrive upload failed ({0}): {1}", "OneDrive yüklemesi başarısız ({0}): {1}");
            Add(t, "OneDrive_UploadFailedRepeatedly", "OneDrive upload failed {0} times in a row: {1}", "OneDrive yüklemesi art arda {0} kez başarısız oldu: {1}");
            Add(t, "OneDrive_SessionFailed", "Could not open the OneDrive upload session: {0}", "OneDrive yükleme oturumu açılamadı: {0}");
            Add(t, "OneDrive_VerifyDownloadFailed", "Could not download the OneDrive file for verification: {0}", "OneDrive dosyası doğrulama için indirilemedi: {0}");
            Add(t, "OneDrive_QuotaFailed", "Could not get OneDrive storage info: {0}", "OneDrive alan bilgisi alınamadı: {0}");
            Add(t, "OneDrive_EmptyResponse", "(empty response)", "(boş yanıt)");
            Add(t, "OneDrive_NotConnectedInternal", "Not connected to OneDrive. Connect first.", "OneDrive'a bağlı değil — önce bağlantı kurulmalı.");

            Add(t, "Browser_SuccessTitle", "Sign-in complete", "Giriş tamamlandı");
            Add(t, "Browser_SuccessBody", "You can close this tab and return to FalcrebCloudBackup.", "Bu sekmeyi kapatıp FalcrebCloudBackup'a dönebilirsiniz.");
            Add(t, "Browser_FailTitle", "Sign-in not completed", "Giriş tamamlanamadı");
            Add(t, "Browser_FailBody", "You can return to FalcrebCloudBackup and try again.", "FalcrebCloudBackup'a dönüp tekrar deneyebilirsiniz.");

            Add(t, "Log_BrowserOpenFailed", "Could not open a browser for sign-in; showing the address in the app instead: {0}", "Giriş için tarayıcı açılamadı, adres arayüzde gösteriliyor: {0}");
            Add(t, "Log_GoogleUploadRetry",
                "Google Drive upload error ({0}); attempt {2} in {1:0} s, resuming where it left off.",
                "Google Drive yükleme hatası ({0}); {1:0} sn sonra {2}. deneme, kaldığı yerden devam edilecek.");
            Add(t, "Log_OneDriveChunkRetry", "OneDrive chunk upload error ({0}); attempt {2} in {1:0} s.", "OneDrive parça yükleme hatası ({0}); {1:0} sn sonra {2}. deneme.");

            Add(t, "Err_Token_VerifyFailed", "The encrypted token could not be verified after saving.", "Şifreli token kaydedildikten sonra doğrulanamadı.");
            Add(t, "Log_Token_Unreadable", "Stored Google token could not be read ({0}). Reconnect under Cloud Connections.", "Kayıtlı Google token'ı okunamadı ({0}). Bulut Bağlantıları ekranından yeniden bağlanın.");
            Add(t, "Log_Token_Migrated", "The stored Google token was moved to encrypted storage and the plain file was removed.", "Kayıtlı Google token'ı şifreli depoya taşındı, düz metin dosya silindi.");
            Add(t, "Log_Token_MigrationFailed", "Could not move the stored Google token to encrypted storage; will try again next time: {0}", "Kayıtlı Google token'ı şifreli depoya taşınamadı, bir sonraki sefer yeniden denenecek: {0}");
            Add(t, "Log_Token_PlaintextDeleteFailed", "Could not delete the old plain Google token file: {0}", "Eski düz metin Google token dosyası silinemedi: {0}");
        }

        private static void AddSchedulingAndStorage(Dictionary<string, string[]> t)
        {
            Add(t, "Task_JobDescription", "FalcrebCloudBackup scheduled backup: {0}", "FalcrebCloudBackup zamanlı yedekleme: {0}");
            Add(t, "Task_CatchUpDescription", "FalcrebCloudBackup: checks for missed backups when the server starts.", "FalcrebCloudBackup: sunucu açıldığında kaçırılmış yedekleme olup olmadığını kontrol eder.");
            Add(t, "Task_AutoStartDescription", "Starts FalcrebCloudBackup when you sign in to Windows.", "FalcrebCloudBackup'ı Windows oturum açılışında başlatır.");
            Add(t, "Log_TaskRegistered", "Scheduled task registered: '{0}' ({1}).", "Zamanlanmış görev kaydedildi: '{0}' ({1}).");
            Add(t, "Log_TaskRegisterFailed", "Could not register the scheduled task: '{0}'", "Zamanlanmış görev kaydedilemedi: '{0}'");
            Add(t, "Log_CatchUpTaskRegistered", "Startup catch-up task registered.", "Açılış telafi görevi kaydedildi.");
            Add(t, "Log_CatchUpTaskRegisterFailed", "Could not register the startup catch-up task.", "Açılış telafi görevi kaydedilemedi.");
            Add(t, "Log_CatchUpTaskRemoveFailed", "Could not remove the startup catch-up task: {0}", "Açılış telafi görevi kaldırılamadı: {0}");
            Add(t, "Log_JobSyncFailed", "Could not sync the job (schedule/watcher): '{0}'", "Görev eşitlenemedi (zamanlama/izleme): '{0}'");
            Add(t, "Log_WatcherRunFailed", "A backup triggered by the file watcher ended with an unexpected error: '{0}'", "İzleyici tetiklemeli yedekleme beklenmedik hatayla bitti: '{0}'");
            Add(t, "Log_WatchStarted", "File watching started: '{0}' ({1}).", "Dosya izleme başladı: '{0}' ({1}).");
            Add(t, "Log_WatchThrottled",
                "File watcher trigger skipped (too frequent): '{0}': {1:0}s since the last trigger, waiting at least {2:0} minutes. The scheduled task will still cover this.",
                "Dosya izleme tetiklemesi atlandı (çok sık): '{0}' — son tetiklemeden bu yana {1:0}sn geçti, en az {2:0} dakika bekleniyor. Zamanlanmış görev bunu ayrıca yakalayacaktır.");
            Add(t, "Log_WatchTriggered", "File watcher triggered: '{0}'.", "Dosya izleme tetiklendi: '{0}'.");

            Add(t, "Err_JobsFileRead", "Could not read the job list file ({0}): {1}", "Görev listesi dosyası okunamadı ({0}): {1}");
            Add(t, "Err_StoreLockTimeout",
                "Could not get the settings file lock within {0:0} seconds ({1}). Another FalcrebCloudBackup process may be stuck.",
                "Ayar dosyası kilidi {0:0} saniye içinde alınamadı ({1}). Başka bir FalcrebCloudBackup işlemi takılı kalmış olabilir.");
            Add(t, "Log_HistoryCorrupt", "The history file is corrupted and is being set aside: {0}", "Geçmiş dosyası bozuk, kenara alınıyor: {0}");
            Add(t, "Log_NotifySettingsReadFailed", "Could not read the notification settings file; using defaults.", "Bildirim ayarları dosyası okunamadı, varsayılanlar kullanılıyor.");
            Add(t, "Log_QuotaFileReadFailed", "Could not read the cloud storage info file: {0}", "Bulut alan bilgisi dosyası okunamadı: {0}");
        }

        private static void AddAppLifecycle(Dictionary<string, string[]> t)
        {
            Add(t, "App_AlreadyRunning", "FalcrebCloudBackup is already running. Check the icon in the system tray.", "FalcrebCloudBackup zaten çalışıyor. Sistem tepsisindeki simgeyi kontrol edin.");
            Add(t, "Log_CatchUpCheckStarted", "Startup catch-up check started (Task Scheduler).", "Açılış telafi kontrolü başlatıldı (Task Scheduler).");
            Add(t, "Log_CatchUpCheckFinished", "Startup catch-up check finished, exit code={0}.", "Açılış telafi kontrolü bitti, çıkış kodu={0}.");
            Add(t, "Log_SilentStarted", "Silent backup started (Task Scheduler), jobId={0}", "Sessiz yedekleme başlatıldı (Task Scheduler), jobId={0}");
            Add(t, "Log_SilentFinished", "Silent backup finished, exit code={0}.", "Sessiz yedekleme sonlandı, çıkış kodu={0}.");
            Add(t, "Log_SecondInstanceBlocked", "The app is already running; a second copy was blocked from opening.", "Uygulama zaten çalışıyor — ikinci kopyanın açılması engellendi.");
            Add(t, "Log_AppStarted", "App started in interactive mode (v{0}).", "Uygulama etkileşimli modda başlatıldı (v{0}).");
            Add(t, "Log_UnhandledAppDomain", "Unhandled exception (AppDomain)", "Yakalanmamış istisna (AppDomain)");
            Add(t, "Log_UnhandledDispatcher", "Unhandled exception (Dispatcher)", "Yakalanmamış istisna (Dispatcher)");
            Add(t, "Log_UnobservedTask", "Unobserved task exception", "Gözlenmeyen görev istisnası");
            Add(t, "Log_SilentSkipped", "Silent backup skipped: job not found or disabled (jobId={0}).", "Sessiz yedekleme atlandı — görev bulunamadı veya etkin değil (jobId={0}).");
            Add(t, "Log_SilentCrashed", "The silent backup ended with an unexpected error.", "Sessiz yedekleme beklenmedik bir hatayla sonlandı.");
            Add(t, "Log_CatchUpNothing", "Startup catch-up check: no missed backups.", "Açılış telafi kontrolü: kaçırılmış yedekleme yok.");
            Add(t, "Log_CatchUpItem", "Startup catch-up: '{0}': {1}", "Açılış telafisi: '{0}' — {1}");
            Add(t, "Log_CatchUpCheckCrashed", "The startup catch-up check ended with an unexpected error.", "Açılış telafi kontrolü beklenmedik bir hatayla sonlandı.");
        }
    }
}
