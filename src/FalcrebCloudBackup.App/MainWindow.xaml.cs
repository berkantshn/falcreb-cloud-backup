using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using ControlzEx.Theming;
using FalcrebCloudBackup.App.Services;
using FalcrebCloudBackup.Auth;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;
using Hardcodet.Wpf.TaskbarNotification;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    /// <summary>Liste satırı: görev + o anki sağlık durumu (renk, simge, açıklama).</summary>
    public class JobListItem
    {
        public BackupJob Job { get; set; }
        public JobHealth Health { get; set; }

        public string Name => Job.Name;
        public string SourcePath => Job.SourcePath;
        public string DestinationsSummary => Job.DestinationsSummary;
        public string ScheduleText => BackupHealth.ScheduleText(Job);
        public string StatusSummary => Health.Summary;
        public string StatusDetailWithSeparator => string.IsNullOrWhiteSpace(Health.Detail) ? string.Empty : " — " + Health.Detail;
        public string StatusGlyph { get; set; }
        public System.Windows.Media.Brush StatusBrush { get; set; }
    }

    /// <summary>Ana ekrandaki bulut alanı çubuğunun tek satırı.</summary>
    public class QuotaRowViewModel
    {
        public string ProviderName { get; set; }
        public double Percent { get; set; }
        public string Summary { get; set; }
        public System.Windows.Media.Brush BarBrush { get; set; }
        public System.Windows.Media.Brush TextBrush { get; set; }
    }

    public partial class MainWindow : MetroWindow
    {
        private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StaleAlertRepeatInterval = TimeSpan.FromHours(24);

        /// <summary>
        /// Açılıştan sonraki ilk sağlık kontrolü. Elektrik kesintisi sonrası sunucuda Firebird
        /// servisi, ağ ve disk daha oturuyor olabilir; telafi yedeklemesi bunun ortasında başlayıp
        /// boşuna başarısız olmasın diye kısa bir süre bekleniyor.
        /// </summary>
        private static readonly TimeSpan FirstHealthCheckDelay = TimeSpan.FromMinutes(5);

        private readonly JobStore _jobStore = new JobStore();
        private readonly BackupCoordinator _coordinator = new BackupCoordinator(Assembly.GetExecutingAssembly().Location);
        private readonly PinAuthProvider _authProvider = new PinAuthProvider();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _healthTimer;
        private readonly DispatcherTimer _lockTimer;
        private readonly SessionLock _sessionLock = new SessionLock();

        private bool _isExiting;
        private bool _isInitializing = true;
        private bool _lockWasUnlocked;

        public MainWindow()
        {
            InitializeComponent();
            var iconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "Assets", "app.ico");
            TrayIcon.Icon = System.IO.File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
            VersionText.Text = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

            try
            {
                JobRunInfoBackfill.Apply(_jobStore, new HistoryStore());
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_BackfillFailed"), ex);
            }

            var items = RefreshJobList();
            _coordinator.SyncAll(items.Select(i => i.Job));
            _coordinator.RunCompleted += OnBackgroundRunCompleted;

            UpdateThemeToggleLabel();
            LanguageCombo.SelectedItem = LanguageCombo.Items.OfType<System.Windows.Controls.ComboBoxItem>().First(i => (string)i.Tag == Loc.Current);

            AutoStartCheckBox.IsChecked = _coordinator.IsAutoStartEnabled();
            _isInitializing = false;

            // Sessiz (gece) yedeklemeler ayrı bir işlemde çalışıp jobs.json'ı günceller; açık duran
            // pencere bunu ancak dosyayı yeniden okuyarak görebilir — eski sürümdeki "Son çalışma:
            // 28.08" takılı kalması bundandı.
            _refreshTimer = new DispatcherTimer { Interval = ListRefreshInterval };
            _refreshTimer.Tick += (s, e) => RefreshJobList();
            _refreshTimer.Start();

            _healthTimer = new DispatcherTimer { Interval = FirstHealthCheckDelay };
            _healthTimer.Tick += (s, e) =>
            {
                _healthTimer.Interval = HealthCheckInterval;
                CheckHealthAndAlert();
            };
            _healthTimer.Start();

            // Kilit rozetindeki geri sayımı canlı tutar ve süre dolduğunda kilidi kapatır.
            _lockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lockTimer.Tick += (s, e) => UpdateLockUi();
            _lockTimer.Start();
            UpdateLockUi();
        }

        private void OnLockButtonClick(object sender, RoutedEventArgs e)
        {
            if (_sessionLock.IsUnlocked)
            {
                _sessionLock.Lock();
                AppLog.Info(Loc.T("Log_Lock_ClosedManually"));
                UpdateLockUi();
                return;
            }

            if (new PinLockWindow(_authProvider) { Owner = this }.ShowDialog() == true)
            {
                _sessionLock.Unlock();
                AppLog.Info(Loc.T("Log_Lock_Opened", SessionLock.Duration.TotalMinutes));
                UpdateLockUi();
            }
        }

        /// <summary>Kilidi hemen kapatır — pencere gizlendiğinde/kapatıldığında çağrılır.</summary>
        private void LockSession()
        {
            if (!_sessionLock.IsUnlocked)
            {
                return;
            }

            _sessionLock.Lock();
            AppLog.Info(Loc.T("Log_Lock_ClosedHidden"));
            UpdateLockUi();
        }

        private void UpdateLockUi()
        {
            var unlocked = _sessionLock.IsUnlocked;

            if (_lockWasUnlocked && !unlocked)
            {
                AppLog.Info(Loc.T("Log_Lock_Expired"));
            }
            _lockWasUnlocked = unlocked;

            if (unlocked)
            {
                LockGlyphText.Text = "\U0001F513";
                LockStatusText.Text = Loc.T("Main_Unlocked", _sessionLock.Remaining.ToString("m\\:ss"));
                LockButton.ToolTip = Loc.T("Main_UnlockedTip");
            }
            else
            {
                LockGlyphText.Text = "\U0001F512";
                LockStatusText.Text = Loc.T("Main_Locked");
                LockButton.ToolTip = Loc.T("Main_LockedTip", SessionLock.Duration.TotalMinutes);
            }
        }

        private void OnActivated(object sender, EventArgs e)
        {
            if (!_isInitializing)
            {
                RefreshJobList();
            }
        }

        private void OnAutoStartChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            try
            {
                _coordinator.SetAutoStart(AutoStartCheckBox.IsChecked == true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("Main_AutoStartError", ex.Message), Loc.T("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        {
            var current = ThemeManager.Current.DetectTheme(this);
            var goingDark = current?.BaseColorScheme != "Dark";

            // Applying the theme to Application.Current (not just this window) is what makes every
            // other open or later-opened window — JobEditWindow, CloudConnectionsWindow, etc. —
            // pick up the change immediately instead of staying stuck on whatever theme was active
            // when each was constructed.
            ThemeManager.Current.ChangeTheme(Application.Current, goingDark ? "Dark.Blue" : "Light.Blue");

            // Yalnızca tema değişir; kayıtlı dil korunur.
            var settings = UiSettingsStore.Load();
            settings.Theme = goingDark ? "Dark" : "Light";
            UiSettingsStore.Save(settings);
            UpdateThemeToggleLabel();
        }

        private void UpdateThemeToggleLabel()
        {
            var current = ThemeManager.Current.DetectTheme(this);
            var isDark = current?.BaseColorScheme == "Dark";
            ThemeToggleButton.Content = isDark ? Loc.T("Main_ThemeLight") : Loc.T("Main_ThemeDark");
        }

        /// <summary>
        /// Dil hemen uygulanır: XAML metinleri bağlama ile kendiliğinden güncellenir, koddan
        /// yazılan metinler (liste, durum, kilit, tema düğmesi) burada yeniden kurulur.
        /// </summary>
        private void OnLanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var language = (LanguageCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            if (_isInitializing || language == null || language == Loc.Current)
            {
                return;
            }

            Loc.SetLanguage(language);
            try
            {
                var settings = UiSettingsStore.Load();
                settings.Language = language;
                UiSettingsStore.Save(settings);
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_LanguageSaveFailed", ex.Message));
            }

            UpdateThemeToggleLabel();
            UpdateLockUi();
            RefreshJobList();
        }

        /// <summary>Listeyi dosyadan yeniden kurar; seçili görev korunur, sorun şeridi ve tepsi ipucu güncellenir.</summary>
        private List<JobListItem> RefreshJobList()
        {
            var selectedId = (JobsListBox.SelectedItem as JobListItem)?.Job.Id;

            List<BackupJob> jobs;
            try
            {
                jobs = _jobStore.LoadAll();
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_JobListReadFailed"), ex);
                SubtitleText.Text = Loc.T("Main_ListLoadFailed", ex.Message);
                return new List<JobListItem>();
            }

            var now = DateTime.UtcNow;
            var items = jobs.Select(job => ToListItem(job, BackupHealth.Evaluate(job, now, BackupEngine.IsJobRunning(job.Id)))).ToList();

            JobsListBox.ItemsSource = items;
            if (selectedId.HasValue)
            {
                JobsListBox.SelectedItem = items.FirstOrDefault(i => i.Job.Id == selectedId.Value);
            }

            var problems = items.Where(i => i.Health.IsProblem).ToList();
            SubtitleText.Text = items.Count == 0
                ? Loc.T("Main_Subtitle_Empty")
                : problems.Count == 0
                    ? Loc.Plural("Main_Subtitle_Ok", items.Count, items.Count)
                    : Loc.Plural("Main_Subtitle_Problems", items.Count, items.Count, problems.Count);

            RefreshQuotaRows();
            UpdateStatusBanner(problems);
            TrayIcon.ToolTipText = problems.Count == 0
                ? Loc.T("Tray_AllGood")
                : Loc.Plural("Tray_Problems", problems.Count, problems.Count);

            return items;
        }

        private static JobListItem ToListItem(BackupJob job, JobHealth health)
        {
            string glyph;
            System.Windows.Media.Color color;
            switch (health.State)
            {
                case HealthState.Ok: glyph = "✓"; color = System.Windows.Media.Color.FromRgb(0x2F, 0xB3, 0x44); break;
                case HealthState.Failed: glyph = "✕"; color = System.Windows.Media.Color.FromRgb(0xE0, 0x45, 0x45); break;
                case HealthState.Stale: glyph = "⚠"; color = System.Windows.Media.Color.FromRgb(0xE0, 0x8A, 0x1E); break;
                case HealthState.Running: glyph = "⟳"; color = System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0xE0); break;
                case HealthState.Disabled: glyph = "–"; color = System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0x9A); break;
                default: glyph = "○"; color = System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0x9A); break;
            }

            return new JobListItem
            {
                Job = job,
                Health = health,
                StatusGlyph = glyph,
                StatusBrush = new SolidColorBrush(color)
            };
        }

        /// <summary>Son yüklemede kaydedilen doluluk bilgisini ana ekranda çubuk olarak gösterir (ağa çıkmaz).</summary>
        private void RefreshQuotaRows()
        {
            List<CloudStorageQuota> quotas;
            try
            {
                quotas = CloudQuotaStore.LoadAll();
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_QuotaReadFailed", ex.Message));
                return;
            }

            var rows = quotas
                .Where(q => q.TotalBytes.HasValue && q.TotalBytes.Value > 0)
                .OrderBy(q => q.Provider)
                .Select(ToQuotaRow)
                .ToList();

            QuotaList.ItemsSource = rows;
            QuotaList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private static QuotaRowViewModel ToQuotaRow(CloudStorageQuota quota)
        {
            var accent = quota.IsCritical
                ? System.Windows.Media.Color.FromRgb(0xE0, 0x45, 0x45)
                : quota.IsLow
                    ? System.Windows.Media.Color.FromRgb(0xE0, 0x8A, 0x1E)
                    : System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0xE0);

            return new QuotaRowViewModel
            {
                ProviderName = CloudProviderDisplayNames.Get(quota.Provider),
                Percent = (quota.UsedFraction ?? 0) * 100,
                Summary = Loc.T("Main_QuotaRow", NotificationMessages.FormatSize(quota.UsedBytes), NotificationMessages.FormatSize(quota.TotalBytes.Value), NotificationMessages.FormatSize(quota.FreeBytes ?? 0)),
                BarBrush = new System.Windows.Media.SolidColorBrush(accent),
                TextBrush = new System.Windows.Media.SolidColorBrush(
                    quota.IsLow ? accent : System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0x9A))
            };
        }

        /// <summary>
        /// Bulut alanı azaldığında günde bir kez uyarır. Uygulama hiçbir yedeği silmediği için
        /// alan er geç dolar; dolduğu gün yedekleme durur. Erken haber vermek, bunun sessiz bir
        /// sürprize dönüşmesini engelleyen tek şey.
        /// </summary>
        private void CheckCloudSpaceAndAlert(DateTime nowUtc)
        {
            List<CloudStorageQuota> quotas;
            try
            {
                quotas = CloudQuotaStore.LoadAll();
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_QuotaAlertReadFailed", ex.Message));
                return;
            }

            foreach (var quota in quotas.Where(q => q.IsLow))
            {
                if (quota.LastAlertUtc.HasValue && nowUtc - quota.LastAlertUtc.Value < StaleAlertRepeatInterval)
                {
                    continue;
                }

                CloudQuotaStore.MarkAlerted(quota.Provider, nowUtc);

                var name = CloudProviderDisplayNames.Get(quota.Provider);
                var title = quota.IsCritical ? Loc.T("Quota_AlertCritical", name) : Loc.T("Quota_AlertLow", name);
                var detail = NotificationMessages.QuotaSummary(quota);

                AppLog.Warn(Loc.T("Log_QuotaAlert", title, detail));
                ShowBalloon(title, detail, quota.IsCritical ? BalloonIcon.Error : BalloonIcon.Warning);

                var body = Loc.T("Quota_AlertBody", detail, Environment.MachineName);
                _coordinator.Notifier.NotifyProblemAsync(title, body, CancellationToken.None)
                    .ContinueWith(t => AppLog.Error(Loc.T("Log_QuotaAlertFailed"), t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private void UpdateStatusBanner(List<JobListItem> problems)
        {
            if (problems.Count == 0)
            {
                StatusBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var worstIsFailure = problems.All(p => p.Health.State == HealthState.Failed);
            var accent = worstIsFailure ? "#E04545" : "#E08A1E";
            var brush = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(accent);
            StatusBanner.BorderBrush = brush;
            StatusBannerGlyph.Foreground = brush;
            StatusBanner.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(worstIsFailure ? "#26E04545" : "#26E08A1E");
            StatusBannerGlyph.Text = worstIsFailure ? "✕" : "⚠";

            StatusBannerTitle.Text = problems.Count == 1
                ? Loc.T("Banner_OneProblem", problems[0].Name)
                : Loc.T("Banner_ManyProblems", problems.Count);
            StatusBannerText.Text = string.Join(Environment.NewLine, problems.Select(p =>
                problems.Count == 1 ? p.Health.Summary + p.StatusDetailWithSeparator : $"• {p.Name}: {p.Health.Summary}"));

            StatusBanner.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Uzun süredir başarılı yedeği olmayan görevler için günde bir kez uyarı: tepsi balonu +
        /// (ayarlıysa) e-posta/Telegram. Gece yedeği hiç çalışmadığında hata da üretmediği için
        /// bunu yakalayacak tek yer burası.
        /// </summary>
        private void CheckHealthAndAlert()
        {
            List<BackupJob> jobs;
            try
            {
                jobs = _jobStore.LoadAll();
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_HealthJobsReadFailed"), ex);
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var job in jobs)
            {
                // Çalışma sürerken uyarı gönderilmez — aksi halde uzun bir aradan sonra başlayan
                // ilk (ve başarıyla bitecek) yedeklemenin ortasında yanlış alarm gidiyor.
                var health = BackupHealth.Evaluate(job, now, BackupEngine.IsJobRunning(job.Id));
                if (health.State != HealthState.Stale)
                {
                    continue;
                }

                // Önce telafi etmeyi dene: yedek alınabiliyorsa kullanıcıyı "yedek yok" diye
                // uyarmanın anlamı yok, birazdan "yedek alındı" bildirimi gidecek zaten.
                if (TryStartCatchUpRun(job, health, now))
                {
                    continue;
                }

                if (job.LastStaleAlertUtc.HasValue && now - job.LastStaleAlertUtc.Value < StaleAlertRepeatInterval)
                {
                    continue;
                }

                _jobStore.Update(job.Id, stored => stored.LastStaleAlertUtc = now);
                AppLog.Warn(Loc.T("Log_StaleWarning", job.Name, health.Summary, health.Detail));

                var title = Loc.T("Stale_Title", job.Name, health.Summary);
                var body = Loc.T("Stale_Body", health.Summary, health.Detail, Environment.MachineName, job.SourcePath);
                ShowBalloon(title, health.Detail, BalloonIcon.Warning);

                var notifier = _coordinator.Notifier;
                _ = notifier.NotifyProblemAsync(title, body, CancellationToken.None);
            }

            CheckCloudSpaceAndAlert(now);
            RefreshJobList();
        }

        /// <summary>
        /// Zamanlanmış çalışma kaçırıldıysa (sunucu o saatte kapalıydı — elektrik kesintisi, bakım)
        /// yedeklemeyi kendiliğinden telafi eder. Windows'un kendi "kaçırılanı sonra çalıştır"
        /// özelliği tek başına güvenilir değil; bu, uygulamanın kendi güvencesi.
        /// </summary>
        private bool TryStartCatchUpRun(BackupJob job, JobHealth health, DateTime nowUtc)
        {
            if (BackupEngine.IsJobRunning(job.Id) || !BackupHealth.ShouldCatchUp(job, health, nowUtc))
            {
                return false;
            }

            _jobStore.Update(job.Id, stored => stored.LastCatchUpUtc = nowUtc);
            AppLog.Info(Loc.T("Log_CatchUpStarting", job.Name, health.Summary));
            ShowBalloon(Loc.T("CatchUp_Balloon", job.Name), health.Summary, BalloonIcon.Info);

            // Sonuç, motorun kendi bildirimi ve RunCompleted olayı üzerinden kullanıcıya ulaşır.
            _coordinator.RunNowAsync(job, CancellationToken.None).ContinueWith(
                t => AppLog.Error(Loc.T("Log_CatchUpFailed", job.Name), t.Exception),
                TaskContinuationOptions.OnlyOnFaulted);

            return true;
        }

        private void OnBackgroundRunCompleted(BackupRunResult result)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshJobList();
                if (!result.OverallSuccess)
                {
                    var detail = result.FatalError ?? NotificationMessages.DestinationsLine(result);
                    ShowBalloon(Loc.T("Run_FailedBalloon", result.JobName), detail, BalloonIcon.Error);
                }
            }));
        }

        private void ShowBalloon(string title, string text, BalloonIcon icon)
        {
            try
            {
                TrayIcon.ShowBalloonTip(title, string.IsNullOrWhiteSpace(text) ? title : text, icon);
            }
            catch (Exception)
            {
                // Tepsi simgesi henüz hazır değilse (nadir) balon gösterilemez; log zaten yazıldı.
            }
        }

        private void OnJobSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var hasSelection = JobsListBox.SelectedItem != null;
            EditButton.IsEnabled = hasSelection;
            RemoveButton.IsEnabled = hasSelection;
            RunNowButton.IsEnabled = hasSelection;
        }

        private void OnJobDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (JobsListBox.SelectedItem != null)
            {
                OnEditJobClick(sender, e);
            }
        }

        /// <summary>
        /// Hassas bir işlemden (görev ekleme/düzenleme/kaldırma, güvenlik ve bulut ayarları, çıkış)
        /// önce kimlik doğrular — uygulama açılışta kilit sormadığı için (bkz. App.xaml.cs
        /// OnStartup) koruma burada.
        ///
        /// Kilit açıksa tekrar sorulmaz, yalnızca süre yenilenir; böylece art arda birkaç ayar
        /// değiştirirken her adımda doğrulayıcı istenmez. Kilit kapalıyken yapılan başarılı bir
        /// doğrulama da oturumu açar — kullanıcı açısından "sorulduğunda bir kez cevapla" demek.
        /// </summary>
        private bool ConfirmIdentity()
        {
            if (_sessionLock.IsUnlocked)
            {
                _sessionLock.Extend();
                UpdateLockUi();
                return true;
            }

            if (new PinLockWindow(_authProvider) { Owner = this }.ShowDialog() != true)
            {
                return false;
            }

            _sessionLock.Unlock();
            UpdateLockUi();
            return true;
        }

        private void OnNewJobClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmIdentity())
            {
                return;
            }

            var editWindow = new JobEditWindow(null) { Owner = this };
            if (editWindow.ShowDialog() != true)
            {
                return;
            }

            _jobStore.Add(editWindow.ResultJob);
            TrySyncJob(editWindow.ResultJob);
            RefreshJobList();
        }

        private void OnEditJobClick(object sender, RoutedEventArgs e)
        {
            if (!(JobsListBox.SelectedItem is JobListItem selected))
            {
                return;
            }

            if (!ConfirmIdentity())
            {
                return;
            }

            // Listedeki kopya 30 sn eski olabilir; düzenleme her zaman dosyadaki güncel kayıttan başlar.
            var fresh = _jobStore.LoadAll().FirstOrDefault(j => j.Id == selected.Job.Id);
            if (fresh == null)
            {
                RefreshJobList();
                return;
            }

            var editWindow = new JobEditWindow(fresh) { Owner = this };
            if (editWindow.ShowDialog() != true)
            {
                return;
            }

            _jobStore.Update(fresh.Id, stored => stored.CopySettingsFrom(editWindow.ResultJob));
            TrySyncJob(editWindow.ResultJob);
            RefreshJobList();
        }

        private void OnRemoveJobClick(object sender, RoutedEventArgs e)
        {
            if (!(JobsListBox.SelectedItem is JobListItem selected))
            {
                return;
            }

            if (!ConfirmIdentity())
            {
                return;
            }

            var confirm = MessageBox.Show(
                this,
                Loc.T("Job_RemoveConfirm", selected.Name),
                Loc.T("Job_RemoveTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _jobStore.Remove(selected.Job.Id);
            try
            {
                _coordinator.RemoveJob(selected.Job.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("Job_RemoveTaskError", ex.Message), Loc.T("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            RefreshJobList();
        }

        /// <summary>
        /// Registering a Task Scheduler entry needs administrator rights (RegisterTaskDefinition
        /// fails with E_ACCESSDENIED otherwise). app.manifest requests elevation for the whole app,
        /// but this still guards against edge cases like a restricted "log on as a batch job" policy.
        /// </summary>
        private void TrySyncJob(BackupJob job)
        {
            try
            {
                _coordinator.SyncJob(job);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    Loc.T("Job_SyncError", ex.Message),
                    Loc.T("Common_Warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void OnRunNowClick(object sender, RoutedEventArgs e)
        {
            if (!(JobsListBox.SelectedItem is JobListItem selected))
            {
                return;
            }

            var job = _jobStore.LoadAll().FirstOrDefault(j => j.Id == selected.Job.Id) ?? selected.Job;

            RunNowButton.IsEnabled = false;
            var progressWindow = new BackupProgressWindow(job.Name) { Owner = this };
            var progress = new Progress<string>(progressWindow.AppendLine);

            try
            {
                var runTask = _coordinator.RunNowAsync(job, CancellationToken.None, progress);
                progressWindow.Show();

                var result = await runTask;
                progressWindow.ShowFinalResult(result);
                RefreshJobList();
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_ManualRunFailed", job.Name), ex);
                progressWindow.AppendLine(Loc.T("Run_UnexpectedError", ex.Message));
            }
            finally
            {
                RunNowButton.IsEnabled = JobsListBox.SelectedItem != null;
            }
        }

        private void OnCloudConnectionsClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmIdentity())
            {
                return;
            }

            new CloudConnectionsWindow { Owner = this }.ShowDialog();
            RefreshJobList();
        }

        private void OnNotificationsClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmIdentity())
            {
                return;
            }

            new NotificationSettingsWindow { Owner = this }.ShowDialog();
        }

        private void OnHistoryClick(object sender, RoutedEventArgs e)
        {
            new HistoryWindow { Owner = this }.ShowDialog();
        }

        private void OnSupportLinkClick(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void OnLogsClick(object sender, RoutedEventArgs e)
        {
            new LogsWindow { Owner = this }.ShowDialog();
        }

        private void OnSecuritySettingsClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmIdentity())
            {
                return;
            }

            new SecuritySettingsWindow(_authProvider) { Owner = this }.ShowDialog();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            // Minimize to tray instead of the taskbar — the app is meant to sit quietly in the background.
            if (WindowState == WindowState.Minimized)
            {
                LockSession();
                Hide();
            }
        }

        private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            // Closing the window just hides it to tray; the app keeps running so watched-folder
            // triggers and the tray icon stay live. Use "Çıkış" from the tray menu to actually exit.
            if (_isExiting)
            {
                _refreshTimer.Stop();
                _healthTimer.Stop();
                _lockTimer.Stop();
                _coordinator.Dispose();
                return;
            }

            e.Cancel = true;
            LockSession();
            Hide();
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmIdentity())
            {
                return;
            }

            _isExiting = true;
            Application.Current.Shutdown();
        }
    }
}
