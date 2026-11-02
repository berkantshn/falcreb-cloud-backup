using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ControlzEx.Theming;
using FalcrebCloudBackup.App.Services;
using FalcrebCloudBackup.Auth;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;

namespace FalcrebCloudBackup.App
{
    public partial class App : Application
    {
        // Global\ (not Local\) so this is enforced across every Terminal Server / RDP session on
        // the machine, not just the current one — kept alive for the app's lifetime since a Mutex
        // held by a process is automatically released when that process exits.
        private const string SingleInstanceMutexName = @"Global\FalcrebCloudBackup_SingleInstance";

        /// <summary>
        /// Sessiz çalışmanın kendi iç süre sınırı: Görev Zamanlayıcı'nın 4 saatlik sert sınırından
        /// (TaskSchedulerService.JobExecutionTimeLimit) önce dolar ki işlem düzgün kapansın —
        /// geçmişe kayıt düşsün, bildirim gitsin, geçici dosyalar silinsin. Sert sınır sadece
        /// bunun da işlemediği durumlar için emniyet.
        /// </summary>
        private static readonly TimeSpan SilentRunTimeout = TimeSpan.FromHours(3);

        private Mutex _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppPaths.EnsureCreated();
            AppLog.Initialize();
            RegisterCrashLogging();
            ApplySavedLanguage();

            // Windows Server 2012 doesn't default WinHTTP/.NET to TLS 1.2 for every configuration;
            // Google/Microsoft's OAuth endpoints require it, so it's forced here unconditionally.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Elektrik kesintisi gibi nedenlerle yarım kalan çalışmalardan geriye kalan yüzlerce
            // MB'lık geçici dosyalar birikmesin diye her başlangıçta (sessiz çalışma dahil) temizlenir.
            BackupEngine.CleanupStaleTempFolders();

            if (e.Args.Contains("--catch-up"))
            {
                AppLog.Info(Loc.T("Log_CatchUpCheckStarted"));
                var catchUpCode = RunCatchUpCheck();
                AppLog.Info(Loc.T("Log_CatchUpCheckFinished", catchUpCode));
                AppLog.Shutdown();
                Shutdown(catchUpCode);
                return;
            }

            var jobId = TryParseSilentBackupArg(e.Args);
            if (jobId.HasValue)
            {
                AppLog.Info(Loc.T("Log_SilentStarted", jobId.Value));
                var exitCode = RunSilentBackup(jobId.Value);
                AppLog.Info(Loc.T("Log_SilentFinished", exitCode));
                AppLog.Shutdown();
                Shutdown(exitCode);
                return;
            }

            // Two interactive copies running at once each register their own file watchers for
            // the same jobs — one file change then triggers a backup twice (or more). This is
            // exactly what happened in the field: several "Uygulama başlatıldı" log lines in a
            // row with no matching exit meant several copies were watching the same folder.
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
            if (!isFirstInstance && e.Args.Contains(FalcrebCloudBackup.Core.Updates.UpdateAttemptStore.FailedArgument))
            {
                // Kurulum çok çabuk başarısız olursa eski kopya henüz kapanıyor olabilir: onu bekle.
                isFirstInstance = WaitForOtherInstanceToExit();
                if (!isFirstInstance)
                {
                    // Başka bir kopya zaten açık, kullanıcının önünde pencere var: ikinci uyarı gereksiz.
                    Shutdown();
                    return;
                }
            }

            if (!isFirstInstance)
            {
                AppLog.Warn(Loc.T("Log_SecondInstanceBlocked"));
                MessageBox.Show(
                    Loc.T("App_AlreadyRunning"),
                    "FalcrebCloudBackup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            AppLog.Info(Loc.T("Log_AppStarted", typeof(App).Assembly.GetName().Version.ToString(3)));
            base.OnStartup(e);

            var uiSettings = UiSettingsStore.Load();
            ThemeManager.Current.ChangeTheme(this, $"{uiSettings.Theme}.Blue");
            PersistResolvedLanguage(uiSettings);

            // The lock only ever gates the *first-ever* run (setting up a PIN in the first place).
            // On every later launch — including an unattended one after a reboot/power outage —
            // the app must come up fully active with no one around to unlock it: scheduled tasks
            // already bypass this entirely (see RunSilentBackup below), and now so does the file
            // watcher, which only starts once MainWindow exists. PIN/authenticator are asked for
            // per action instead — see MainWindow.ConfirmIdentity.
            var authProvider = new PinAuthProvider();
            if (authProvider.State == AuthSetupState.NotConfigured)
            {
                var setupWindow = new PinLockWindow(authProvider);
                if (setupWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            new MainWindow().Show();
        }

        private bool WaitForOtherInstanceToExit()
        {
            try
            {
                return _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                // Eski kopya beklenmedik şekilde kapanmış; kilit artık bu kopyanın.
                return true;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLog.Shutdown();
            base.OnExit(e);
        }

        /// <summary>
        /// Kayıtlı dili uygular; sessiz çalışma ve telafi kontrolü de buradan geçer, böylece bildirimler
        /// ve hata metinleri seçili dilde çıkar. Ayar okunamazsa yedekleme durmaz, sistem dili kullanılır.
        /// </summary>
        private static void ApplySavedLanguage()
        {
            try
            {
                Loc.SetLanguage(UiSettingsStore.Load().Language);
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_LanguageLoadFailed", ex.Message));
            }
        }

        /// <summary>
        /// İlk açılışta belirlenen dil kaydedilir. Sessiz çalışma ayrı bir oturumda farklı bir sistem
        /// diline düşebileceği için bundan sonra arayüzle aynı kayıtlı dili kullanır.
        /// </summary>
        private static void PersistResolvedLanguage(UiSettings uiSettings)
        {
            if (!string.IsNullOrEmpty(uiSettings.Language))
            {
                return;
            }

            try
            {
                uiSettings.Language = Loc.Current;
                UiSettingsStore.Save(uiSettings);
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_LanguageSaveFailed", ex.Message));
            }
        }

        /// <summary>So a crash that happens with nobody watching (unattended server) still leaves a trace in the log file.</summary>
        private void RegisterCrashLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                AppLog.Error(Loc.T("Log_UnhandledAppDomain"), e.ExceptionObject as Exception);
                AppLog.Shutdown();
            };

            DispatcherUnhandledException += (s, e) =>
            {
                AppLog.Error(Loc.T("Log_UnhandledDispatcher"), e.Exception);
                // Without this, WPF rethrows after the handler runs and the whole app exits — for
                // a background tool whose job is to keep watchers/schedules alive, logging and
                // carrying on beats disappearing silently over one bad UI-thread exception.
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                AppLog.Error(Loc.T("Log_UnobservedTask"), e.Exception);
                e.SetObserved();
            };
        }

        private static Guid? TryParseSilentBackupArg(string[] args)
        {
            var flagIndex = Array.IndexOf(args, "--silent-backup");
            if (flagIndex < 0 || flagIndex + 1 >= args.Length)
            {
                return null;
            }

            return Guid.TryParse(args[flagIndex + 1], out var jobId) ? jobId : (Guid?)null;
        }

        /// <summary>
        /// Çıkış kodu Görev Zamanlayıcı'ya "başarısız oldu, yeniden dene" demenin tek yolu:
        /// 0 = başarılı (ya da görev yok/devre dışı — yeniden denemenin anlamı yok), 1 = yedekleme
        /// başarısız, 2 = beklenmedik çökme.
        /// </summary>
        private static int RunSilentBackup(Guid jobId)
        {
            try
            {
                var jobStore = new JobStore();
                var job = jobStore.LoadAll().FirstOrDefault(j => j.Id == jobId);
                if (job == null || !job.Enabled)
                {
                    AppLog.Warn(Loc.T("Log_SilentSkipped", jobId));
                    return 0;
                }

                // allowInteractiveLogin: false — kimsenin olmadığı sunucuda tarayıcı açıp beklemek
                // yerine "yeniden bağlanın" hatasıyla biter (bkz. sağlayıcı sınıflarının açıklaması).
                var engine = CreateEngine(jobStore, new HistoryStore());
                using (var timeout = new CancellationTokenSource(SilentRunTimeout))
                {
                    var result = engine.RunAsync(job, timeout.Token).GetAwaiter().GetResult();
                    return result.OverallSuccess ? 0 : 1;
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_SilentCrashed"), ex);
                return 2;
            }
        }

        /// <summary>
        /// Sunucu açılışında çalışan arayüzsüz telafi kontrolü (--catch-up). Kaçırılmış bir
        /// yedekleme varsa alır, yoksa hiçbir şey yapmadan kapanır.
        ///
        /// Uygulamanın içindeki telafiden farkı: bu, kimse Windows'a giriş yapmasa da çalışır.
        /// Elektrik kesintisinden sonra sunucu kendiliğinden açılıp hafta sonu kimse giriş
        /// yapmazsa, kaçırılan yedeği yakalayan tek şey budur.
        /// </summary>
        private static int RunCatchUpCheck()
        {
            try
            {
                var jobStore = new JobStore();
                var nowUtc = DateTime.UtcNow;

                var pending = jobStore.LoadAll()
                    .Where(job => !BackupEngine.IsJobRunning(job.Id))
                    .Select(job => new { Job = job, Health = BackupHealth.Evaluate(job, nowUtc) })
                    .Where(x => BackupHealth.ShouldCatchUp(x.Job, x.Health, nowUtc))
                    .ToList();

                if (pending.Count == 0)
                {
                    AppLog.Info(Loc.T("Log_CatchUpNothing"));
                    return 0;
                }

                var engine = CreateEngine(jobStore, new HistoryStore());
                var anyFailed = false;

                foreach (var item in pending)
                {
                    AppLog.Info(Loc.T("Log_CatchUpItem", item.Job.Name, item.Health.Summary));
                    jobStore.Update(item.Job.Id, stored => stored.LastCatchUpUtc = nowUtc);

                    using (var timeout = new CancellationTokenSource(SilentRunTimeout))
                    {
                        var result = engine.RunAsync(item.Job, timeout.Token).GetAwaiter().GetResult();
                        if (!result.OverallSuccess)
                        {
                            anyFailed = true;
                        }
                    }
                }

                return anyFailed ? 1 : 0;
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_CatchUpCheckCrashed"), ex);
                return 2;
            }
        }

        /// <summary>
        /// allowInteractiveLogin: false — kimsenin başında olmadığı sunucuda tarayıcı açıp
        /// sonsuza kadar beklemek yerine "yeniden bağlanın" hatasıyla biter.
        /// </summary>
        private static BackupEngine CreateEngine(JobStore jobStore, HistoryStore historyStore)
        {
            var providers = new ICloudProvider[]
            {
                new FalcrebCloudBackup.CloudProviders.GoogleDriveProvider(allowInteractiveLogin: false),
                new FalcrebCloudBackup.CloudProviders.OneDriveProvider(allowInteractiveLogin: false)
            };

            return new BackupEngine(providers, new AlertNotifier(), jobStore, historyStore);
        }
    }
}
