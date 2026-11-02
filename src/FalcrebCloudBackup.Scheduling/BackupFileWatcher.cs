using System;
using System.IO;
using System.Threading;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Scheduling
{
    /// <summary>
    /// Watches a job's source folder and fires <see cref="Triggered"/> shortly after the
    /// accounting program finishes writing a new backup file — this is the "don't wait for the
    /// next scheduled tick" half of triggering, running alongside TaskSchedulerService rather than
    /// instead of it. Changes are debounced because software typically writes a backup file in
    /// several steps (create, write, close) that each raise their own event.
    ///
    /// Canlı Firebird veritabanı dosyalarındaki (.fdb/.gdb) değişiklikler yok sayılır: Wolvox
    /// çalışırken bu dosyalar sürekli yazılır ve her yazımda tam bir yedek başlatmak (27.08'de
    /// olduğu gibi, mesai boyunca 12'şer dakikalık yüklemeler) anlamsızdır — veritabanı için
    /// zamanlanmış çalışma yeterlidir.
    ///
    /// MinTriggerInterval is a second, coarser safety net on top of that debounce: if SourcePath is
    /// ever pointed at something that writes near-continuously, this stops it from firing a
    /// backup on every write — the scheduled trigger (TaskSchedulerService) still covers anything
    /// this throttles away.
    /// </summary>
    public class BackupFileWatcher : IDisposable
    {
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinTriggerInterval = TimeSpan.FromMinutes(5);

        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private DateTime? _lastTriggeredUtc;

        public event Action<BackupJob> Triggered;

        public BackupJob Job { get; }

        public BackupFileWatcher(BackupJob job)
        {
            Job = job;
            _debounceTimer = new Timer(_ => OnDebounceElapsed(), null, Timeout.Infinite, Timeout.Infinite);

            var watchPath = job.SourceIsFolder ? job.SourcePath : Path.GetDirectoryName(job.SourcePath);
            var filter = job.SourceIsFolder ? "*.*" : Path.GetFileName(job.SourcePath);

            _watcher = new FileSystemWatcher(watchPath, filter)
            {
                IncludeSubdirectories = job.SourceIsFolder,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Renamed += OnFileSystemEvent;
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
            AppLog.Info(Loc.T("Log_WatchStarted", Job.Name, _watcher.Path));
        }

        public void Stop() => _watcher.EnableRaisingEvents = false;

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            if (FirebirdBackupTool.IsDatabaseFile(e.FullPath))
            {
                return;
            }

            _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }

        private void OnDebounceElapsed()
        {
            if (_lastTriggeredUtc.HasValue && DateTime.UtcNow - _lastTriggeredUtc.Value < MinTriggerInterval)
            {
                AppLog.Info(Loc.T("Log_WatchThrottled", Job.Name, (DateTime.UtcNow - _lastTriggeredUtc.Value).TotalSeconds, MinTriggerInterval.TotalMinutes));
                return;
            }

            _lastTriggeredUtc = DateTime.UtcNow;
            AppLog.Info(Loc.T("Log_WatchTriggered", Job.Name));
            Triggered?.Invoke(Job);
        }

        public void Dispose()
        {
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Renamed -= OnFileSystemEvent;
            _watcher.Dispose();
            _debounceTimer.Dispose();
        }
    }
}
