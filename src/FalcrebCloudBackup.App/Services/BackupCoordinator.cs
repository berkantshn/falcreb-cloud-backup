using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.CloudProviders;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;
using FalcrebCloudBackup.Scheduling;

namespace FalcrebCloudBackup.App.Services
{
    /// <summary>
    /// Composition root for the running app: keeps one live BackupFileWatcher per
    /// watch-enabled job and mirrors every job's schedule into Windows Task Scheduler, so a job
    /// stays in sync the moment it's added, edited, enabled/disabled, or removed. This is the
    /// only place that owns watcher lifetimes and drives BackupEngine outside of a manual "run now".
    /// </summary>
    public class BackupCoordinator : IDisposable
    {
        private readonly Dictionary<Guid, BackupFileWatcher> _watchers = new Dictionary<Guid, BackupFileWatcher>();
        private readonly TaskSchedulerService _taskSchedulerService;
        private readonly BackupEngine _engine;

        /// <summary>Uygulama içinden başlatılan (izleyici veya "Şimdi Çalıştır") her çalışmanın sonucu — arka plan iş parçacığında tetiklenir.</summary>
        public event Action<BackupRunResult> RunCompleted;

        private int _activeRuns;

        /// <summary>Bu süreçte süren bir yedekleme varsa true; güncelleme onu kesmemek için bekler.</summary>
        public bool IsBusy => Volatile.Read(ref _activeRuns) > 0;

        public INotifier Notifier { get; }

        public BackupCoordinator(string exePath)
        {
            _taskSchedulerService = new TaskSchedulerService(exePath);
            Notifier = new AlertNotifier();

            // Yedekleme çalışmaları asla tarayıcı açmaz; giriş yalnızca Bulut Bağlantıları ekranından yapılır.
            var providers = new ICloudProvider[]
            {
                new GoogleDriveProvider(allowInteractiveLogin: false),
                new OneDriveProvider(allowInteractiveLogin: false)
            };
            _engine = new BackupEngine(providers, Notifier, new JobStore(), new HistoryStore());
        }

        /// <summary>Best-effort: one misconfigured job (e.g. a source folder that no longer exists) must not stop the rest from syncing at startup.</summary>
        public void SyncAll(IEnumerable<BackupJob> jobs)
        {
            var jobList = jobs as IList<BackupJob> ?? jobs.ToList();

            // Açılış telafi görevi, yalnızca onu isteyen en az bir görev varsa kayıtlı tutulur.
            if (jobList.Any(j => j.Enabled && j.CatchUpMissedRuns))
            {
                _taskSchedulerService.RegisterOrUpdateCatchUpTask();
            }
            else
            {
                _taskSchedulerService.RemoveCatchUpTask();
            }

            foreach (var job in jobList)
            {
                try
                {
                    SyncJob(job);
                }
                catch (Exception ex)
                {
                    // Kullanıcıya görevi bir sonraki düzenleyişinde TrySyncJob üzerinden gösterilir;
                    // ama log'da iz kalmalı — aksi halde "neden zamanlanmış görev yok?" sorusu cevapsız kalıyor.
                    AppLog.Error(Loc.T("Log_JobSyncFailed", job.Name), ex);
                }
            }
        }

        /// <summary>Call after every add/edit/enable-toggle so the watcher and the Task Scheduler entry reflect the current job state.</summary>
        public void SyncJob(BackupJob job)
        {
            StopWatcher(job.Id);

            if (!job.Enabled)
            {
                _taskSchedulerService.RemoveJobTask(job.Id);
                return;
            }

            _taskSchedulerService.RegisterOrUpdateJobTask(job);

            if (job.WatchForChanges)
            {
                var watcher = new BackupFileWatcher(job);
                watcher.Triggered += OnWatcherTriggered;
                watcher.Start();
                _watchers[job.Id] = watcher;
            }
        }

        public void RemoveJob(Guid jobId)
        {
            StopWatcher(jobId);
            _taskSchedulerService.RemoveJobTask(jobId);
        }

        public bool IsJobTaskRegistered(Guid jobId) => _taskSchedulerService.IsJobTaskRegistered(jobId);

        public bool IsAutoStartEnabled() => _taskSchedulerService.IsAutoStartEnabled();
        public void SetAutoStart(bool enabled)
        {
            if (enabled)
            {
                _taskSchedulerService.EnableAutoStart();
            }
            else
            {
                _taskSchedulerService.DisableAutoStart();
            }
        }

        public async Task<BackupRunResult> RunNowAsync(BackupJob job, CancellationToken cancellationToken, IProgress<string> statusProgress = null)
        {
            var result = await RunWithTimeoutAsync(job, cancellationToken, statusProgress).ConfigureAwait(false);
            RunCompleted?.Invoke(result);
            return result;
        }

        private async void OnWatcherTriggered(BackupJob job)
        {
            try
            {
                var result = await RunWithTimeoutAsync(job, CancellationToken.None, null).ConfigureAwait(false);
                RunCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                // async void: buradan kaçan bir istisna tüm uygulamayı düşürür.
                AppLog.Error(Loc.T("Log_WatcherRunFailed", job.Name), ex);
            }
        }

        /// <summary>Uygulama içi çalışmalar da sessiz çalışmayla aynı üst süre sınırına tabidir; hiçbir çalışma sonsuza kadar sürmez.</summary>
        private Task<BackupRunResult> RunWithTimeoutAsync(BackupJob job, CancellationToken cancellationToken, IProgress<string> statusProgress)
        {
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TaskSchedulerService.JobExecutionTimeLimit);
            Interlocked.Increment(ref _activeRuns);
            return _engine.RunAsync(job, linked.Token, statusProgress).ContinueWith(t =>
            {
                Interlocked.Decrement(ref _activeRuns);
                linked.Dispose();
                return t.GetAwaiter().GetResult();
            }, TaskScheduler.Default);
        }

        private void StopWatcher(Guid jobId)
        {
            if (_watchers.TryGetValue(jobId, out var watcher))
            {
                watcher.Triggered -= OnWatcherTriggered;
                watcher.Stop();
                watcher.Dispose();
                _watchers.Remove(jobId);
            }
        }

        public void Dispose()
        {
            foreach (var jobId in new List<Guid>(_watchers.Keys))
            {
                StopWatcher(jobId);
            }
        }
    }
}
