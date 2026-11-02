using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;

namespace FalcrebCloudBackup.Core.Engine
{
    /// <summary>
    /// Runs one BackupJob end to end: package the source, push it to every configured
    /// destination, optionally verify, then report. There is no delete/prune method anywhere in
    /// this class on purpose — a run can only add a new timestamped copy, never remove one.
    /// </summary>
    public class BackupEngine
    {
        private readonly IReadOnlyDictionary<CloudProviderType, ICloudProvider> _providersByType;
        private readonly INotifier _notifier;
        private readonly JobStore _jobStore;
        private readonly HistoryStore _historyStore;

        public BackupEngine(IEnumerable<ICloudProvider> availableProviders, INotifier notifier, JobStore jobStore, HistoryStore historyStore)
        {
            _providersByType = availableProviders.ToDictionary(p => p.ProviderType);
            _notifier = notifier;
            _jobStore = jobStore;
            _historyStore = historyStore;
        }

        /// <summary>
        /// A cross-process named mutex per job id — belt-and-suspenders alongside the app's
        /// single-instance guard (App.xaml.cs) so the *same* job can never run twice at once (a
        /// manual "Şimdi Çalıştır" overlapping a scheduled/watcher trigger, say), regardless of
        /// which process(es) they're running in.
        ///
        /// Mutex is thread-affine — whichever thread calls WaitOne must be the one that calls
        /// ReleaseMutex, or .NET throws. An `await` inside the guarded region can resume on a
        /// different pool thread, so the whole guarded call is pinned to one thread via Task.Run +
        /// GetAwaiter().GetResult() instead of being awaited directly.
        /// </summary>
        private static string JobMutexName(Guid jobId) => $@"Global\FalcrebCloudBackup_Job_{jobId:N}";

        private static string TempRootDirectory => Path.Combine(Path.GetTempPath(), "FalcrebCloudBackup");

        /// <summary>
        /// Yarım kalmış çalışmalardan (elektrik kesintisi, zorla kapatma, çökme) artakalan geçici
        /// çalışma klasörlerini siler. Her yedekleme yüzlerce MB'lık bir zip ürettiği için bunlar
        /// birikirse sistem diski dolar ve sonraki yedeklemeler de başarısız olmaya başlar.
        ///
        /// Yalnızca 24 saatten eski klasörlere dokunulur: bir çalışma en fazla 4 saat sürebildiği
        /// için (bkz. TaskSchedulerService.JobExecutionTimeLimit) o anda süren bir yedeklemenin
        /// klasörü bu yaşa asla ulaşmaz. Burada silinen şeyler yalnızca bu uygulamanın kendi
        /// ürettiği geçici dosyalardır — buluta ulaşmış hiçbir yedeğe dokunulmaz.
        /// </summary>
        public static void CleanupStaleTempFolders()
        {
            try
            {
                if (!Directory.Exists(TempRootDirectory))
                {
                    return;
                }

                var cutoffUtc = DateTime.UtcNow - TimeSpan.FromHours(24);

                foreach (var directory in Directory.GetDirectories(TempRootDirectory))
                {
                    TryDeleteStale(directory, cutoffUtc, isDirectory: true);
                }

                foreach (var file in Directory.GetFiles(TempRootDirectory, "*.tmp"))
                {
                    TryDeleteStale(file, cutoffUtc, isDirectory: false);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_TempCleanupFailed", ex.Message));
            }
        }

        private static void TryDeleteStale(string path, DateTime cutoffUtc, bool isDirectory)
        {
            try
            {
                var lastWriteUtc = isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc > cutoffUtc)
                {
                    return;
                }

                if (isDirectory)
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }

                AppLog.Info(Loc.T("Log_TempItemDeleted", path));
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_TempItemDeleteFailed", path, ex.Message));
            }
        }

        /// <summary>
        /// O anda bu görev için bir yedekleme çalışıyor mu — sessiz (Task Scheduler) çalışma ayrı
        /// bir işlem olduğu için arayüz bunu ancak paylaşılan kilidin varlığına bakarak anlayabilir.
        /// Kilit yalnızca sorgulanır, alınmaz; başlamak üzere olan bir çalışmayı engellemez.
        /// </summary>
        public static bool IsJobRunning(Guid jobId)
        {
            try
            {
                if (Mutex.TryOpenExisting(JobMutexName(jobId), out var existing))
                {
                    existing.Dispose();
                    return true;
                }
            }
            catch (Exception)
            {
                // Kilit sorgulanamıyorsa (yetki vb.) "çalışmıyor" varsayılır — bu yalnızca durum
                // göstergesini etkiler, yedeklemenin kendisini değil.
            }

            return false;
        }

        public Task<BackupRunResult> RunAsync(BackupJob job, CancellationToken cancellationToken, IProgress<string> statusProgress = null)
        {
            return Task.Run(() =>
            {
                using (var jobMutex = new Mutex(false, JobMutexName(job.Id)))
                {
                    var acquired = false;
                    try
                    {
                        acquired = jobMutex.WaitOne(TimeSpan.Zero);
                    }
                    catch (AbandonedMutexException)
                    {
                        // Önceki çalışma kilidi bırakmadan (çökerek) bitmiş; kilit artık bizde.
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        AppLog.Warn(Loc.T("Log_RunSkippedBusy", job.Name));
                        return new BackupRunResult
                        {
                            JobId = job.Id,
                            JobName = job.Name,
                            StartedUtc = DateTime.UtcNow,
                            FinishedUtc = DateTime.UtcNow,
                            FatalError = Loc.T("Engine_AlreadyRunning")
                        };
                    }

                    try
                    {
                        return RunAsyncCore(job, cancellationToken, statusProgress).GetAwaiter().GetResult();
                    }
                    finally
                    {
                        jobMutex.ReleaseMutex();
                    }
                }
            }, cancellationToken);
        }

        private async Task<BackupRunResult> RunAsyncCore(BackupJob job, CancellationToken cancellationToken, IProgress<string> statusProgress)
        {
            var result = new BackupRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                StartedUtc = DateTime.UtcNow
            };

            AppLog.Info(Loc.T("Log_RunStarted", job.Name, job.Id));

            // Her aşama hem arayüze hem log dosyasına yazılır: sessiz çalışma bir yerde takılırsa
            // log dosyasından *nerede* takıldığı görülebilsin.
            void Report(string message)
            {
                AppLog.Info($"[{job.Name}] {message}");
                statusProgress?.Report(message);
            }

            var runStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            StagedSource staged = null;
            try
            {
                staged = StageSourceForUpload(job, runStamp, cancellationToken, Report);
                result.FilesProcessed = staged.FileCount;
                result.TotalBytes = new FileInfo(staged.UploadPath).Length;
                Report(Loc.Plural("Engine_Prepared", staged.FileCount, NotificationMessages.FormatSize(result.TotalBytes), staged.FileCount, NotificationMessages.FormatDuration(DateTime.UtcNow - result.StartedUtc)));

                var remoteFolderSuffix = job.UseTimestampedFolders ? runStamp : null;

                foreach (var destination in job.Destinations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destResult = await RunOneDestinationAsync(destination, staged.UploadPath, remoteFolderSuffix, job.VerifyHashAfterUpload, Report, cancellationToken)
                        .ConfigureAwait(false);
                    result.DestinationResults.Add(destResult);
                }

                result.OverallSuccess = result.DestinationResults.Count > 0 && result.DestinationResults.All(d => d.Success);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.OverallSuccess = false;
                result.FatalError = Loc.T("Engine_TimedOut");
                AppLog.Error(Loc.T("Log_RunTimedOut", job.Name));
            }
            catch (Exception ex)
            {
                result.OverallSuccess = false;
                result.FatalError = FriendlyErrors.Describe(ex);
                AppLog.Error(Loc.T("Log_RunStartFailed", job.Name), ex);
            }
            finally
            {
                result.FinishedUtc = DateTime.UtcNow;
                // Sadece bu çalışmanın ürettiği geçici zip/fbk dosyaları silinir; kaynak veya
                // buluta ulaşmış bir yedek asla değil.
                staged?.Cleanup();
            }

            PersistLastRun(job.Id, result);
            _historyStore.Append(result);

            var destinationsSummary = string.Join(", ", result.DestinationResults.Select(d =>
                $"{CloudProviderDisplayNames.Get(d.Provider)}={(d.Success ? Loc.T("Log_DestOk") : Loc.T("Log_DestError"))}"));
            AppLog.Info(Loc.T("Log_RunFinished", job.Name, result.OverallSuccess ? Loc.T("Result_Success") : Loc.T("Result_Failed"), destinationsSummary, (result.FinishedUtc - result.StartedUtc).TotalSeconds));

            try
            {
                await _notifier.NotifyRunCompletedAsync(result, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_NotifyFailed"), ex);
            }

            return result;
        }

        /// <summary>
        /// Sadece son çalışma alanlarını, dosyadaki güncel görev kaydıyla birleştirerek yazar.
        /// Sessiz (Task Scheduler) çalışma da bu yoldan geçer — arayüz açık olsun olmasın,
        /// jobs.json tek ortak gerçek kaynaktır.
        /// </summary>
        private void PersistLastRun(Guid jobId, BackupRunResult result)
        {
            try
            {
                _jobStore.Update(jobId, stored =>
                {
                    stored.LastRunUtc = result.FinishedUtc;
                    stored.LastRunSuccess = result.OverallSuccess;
                    stored.LastRunError = result.OverallSuccess
                        ? null
                        : result.FatalError ?? result.DestinationResults.FirstOrDefault(d => !d.Success)?.ErrorMessage;
                    if (result.OverallSuccess)
                    {
                        stored.LastSuccessUtc = result.FinishedUtc;
                    }
                });
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_PersistRunFailed"), ex);
            }
        }

        private async Task<DestinationRunResult> RunOneDestinationAsync(
            BackupDestination destination,
            string localFilePath,
            string remoteFolderSuffix,
            bool verifyHash,
            Action<string> report,
            CancellationToken cancellationToken)
        {
            var destResult = new DestinationRunResult { Provider = destination.Provider };
            var displayName = CloudProviderDisplayNames.Get(destination.Provider);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!_providersByType.TryGetValue(destination.Provider, out var provider))
                {
                    throw new InvalidOperationException(Loc.T("Engine_NoProvider", displayName));
                }

                report(Loc.T("Engine_Connecting", displayName));

                // Each run gets a fresh provider instance (see BackupCoordinator/App.xaml.cs), so
                // IsConnected always starts false here. ConnectAsync reuses the stored refresh
                // token silently; it never opens a browser from here (see providers' ctor flag).
                if (!provider.IsConnected)
                {
                    await provider.ConnectAsync(cancellationToken).ConfigureAwait(false);
                }

                var remoteFolder = remoteFolderSuffix == null
                    ? destination.RemoteFolderName
                    : $"{destination.RemoteFolderName}/{remoteFolderSuffix}";

                await provider.EnsureFolderAsync(remoteFolder, cancellationToken).ConfigureAwait(false);

                report(Loc.T("Engine_Uploading", displayName));
                var remotePath = await provider.UploadFileAsync(localFilePath, remoteFolder, progress: null, cancellationToken).ConfigureAwait(false);
                report(Loc.T("Engine_UploadDone", displayName, NotificationMessages.FormatDuration(stopwatch.Elapsed)));

                if (verifyHash)
                {
                    report(Loc.T("Engine_Verifying", displayName));
                    var localHash = HashVerifier.ComputeSha256(localFilePath);
                    using (var remoteStream = await provider.OpenReadForVerificationAsync(remotePath, cancellationToken).ConfigureAwait(false))
                    {
                        var remoteHash = HashVerifier.ComputeSha256(remoteStream);
                        if (!string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(Loc.T("Engine_HashMismatch"));
                        }
                    }
                    report(Loc.T("Engine_Verified", displayName));
                }

                destResult.RemotePath = remotePath;
                destResult.Success = true;

                await RefreshQuotaAsync(provider, displayName, report, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Genel zaman aşımı: hedef sonucu kaydedilir, üst katman "durduruldu" olarak işaretler.
                destResult.Success = false;
                destResult.ErrorMessage = Loc.T("Engine_DestTimedOut");
                throw;
            }
            catch (Exception ex)
            {
                destResult.Success = false;
                destResult.ErrorMessage = FriendlyErrors.Describe(ex);
                report(Loc.T("Engine_DestFailed", displayName, destResult.ErrorMessage));
                AppLog.Error(Loc.T("Log_DestUploadFailed", displayName), ex);
            }

            return destResult;
        }

        /// <summary>
        /// Yükleme bittikten sonra hesabın doluluğunu da okuyup saklar — bağlantı zaten açık
        /// olduğu için neredeyse bedavaya gelir ve arayüz/bildirimler ağa çıkmadan bu kayıttan
        /// okuyabilir. Başarısız olması yedeklemeyi etkilemez; yalnızca bilgi amaçlıdır.
        /// </summary>
        private static async Task RefreshQuotaAsync(ICloudProvider provider, string displayName, Action<string> report, CancellationToken cancellationToken)
        {
            try
            {
                var quota = await provider.GetStorageQuotaAsync(cancellationToken).ConfigureAwait(false);
                CloudQuotaStore.Save(quota);

                var summary = NotificationMessages.QuotaSummary(quota);
                if (summary != null)
                {
                    report($"{displayName}: {summary}");
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_ProviderQuotaFailed", displayName, ex.Message));
            }
        }

        /// <summary>
        /// Yüklenecek tek dosyayı hazırlar. Kaynaktaki Firebird veritabanları (gbak açıksa) önce
        /// tutarlı .fbk yedeğine çevrilir; sonra her şey tek bir zip'e alınır (klasörler her zaman
        /// zip'lenir, tek dosya kaynaklarda kullanıcı zip'i kapatabilir).
        /// </summary>
        private static StagedSource StageSourceForUpload(BackupJob job, string runStamp, CancellationToken cancellationToken, Action<string> report)
        {
            if (job.SourceIsFolder ? !Directory.Exists(job.SourcePath) : !File.Exists(job.SourcePath))
            {
                throw new InvalidOperationException(Loc.T("Engine_SourceMissing", job.SourcePath));
            }

            var sourceRoot = job.SourceIsFolder ? job.SourcePath : Path.GetDirectoryName(job.SourcePath);
            var sourceFiles = job.SourceIsFolder
                ? Directory.EnumerateFiles(job.SourcePath, "*", SearchOption.AllDirectories).ToList()
                : new List<string> { job.SourcePath };

            if (sourceFiles.Count == 0)
            {
                throw new InvalidOperationException(Loc.T("Engine_SourceEmpty"));
            }

            var databaseFiles = new HashSet<string>(sourceFiles.Where(FirebirdBackupTool.IsDatabaseFile), StringComparer.OrdinalIgnoreCase);
            string gbakPath = null;
            var useGbak = false;
            if (databaseFiles.Count > 0)
            {
                gbakPath = FirebirdBackupTool.ResolveGbakPath(job.Firebird, job.SourcePath);
                useGbak = job.Firebird?.UseGbak ?? (gbakPath != null);

                if (useGbak && gbakPath == null)
                {
                    throw new InvalidOperationException(Loc.T("Engine_GbakMissing"));
                }

                report(useGbak
                    ? Loc.Plural("Engine_GbakPlan", databaseFiles.Count, databaseFiles.Count, gbakPath)
                    : Loc.Plural("Engine_RawCopyWarning", databaseFiles.Count, databaseFiles.Count));
            }

            var workDir = Path.Combine(Path.GetTempPath(), "FalcrebCloudBackup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            var staged = new StagedSource(workDir);

            try
            {
                CheckFreeSpace(workDir, sourceFiles, report);

                var entries = new List<ZipHelper.Entry>();
                foreach (var file in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = MakeRelative(sourceRoot, file);

                    if (useGbak && databaseFiles.Contains(file))
                    {
                        var fbkPath = Path.Combine(workDir, "fbk", relative + ".fbk");
                        report(Loc.T("Engine_FirebirdStarting", relative));
                        var sw = Stopwatch.StartNew();
                        FirebirdBackupTool.Backup(gbakPath, file, fbkPath, job.Firebird.User, job.Firebird.Password, cancellationToken, report);
                        report(Loc.T("Engine_FirebirdDone", relative, NotificationMessages.FormatSize(new FileInfo(fbkPath).Length), NotificationMessages.FormatDuration(sw.Elapsed)));
                        entries.Add(new ZipHelper.Entry { EntryName = relative.Replace('\\', '/') + ".fbk", FilePath = fbkPath });
                    }
                    else
                    {
                        entries.Add(new ZipHelper.Entry { EntryName = relative.Replace('\\', '/'), FilePath = file });
                    }
                }

                staged.FileCount = entries.Count;

                if (!job.ZipBeforeUpload && !job.SourceIsFolder && entries.Count == 1)
                {
                    staged.UploadPath = entries[0].FilePath;
                    return staged;
                }

                var zipPath = Path.Combine(workDir, $"{SanitizeFileName(job.Name)}_{runStamp}.zip");
                report(Loc.T("Engine_Zipping"));
                var zipWatch = Stopwatch.StartNew();
                ZipHelper.Create(zipPath, entries, cancellationToken);
                report(Loc.T("Engine_ZipDone", NotificationMessages.FormatDuration(zipWatch.Elapsed)));

                staged.UploadPath = zipPath;
                return staged;
            }
            catch
            {
                staged.Cleanup();
                throw;
            }
        }

        private static void CheckFreeSpace(string workDir, List<string> sourceFiles, Action<string> report)
        {
            long sourceBytes = 0;
            foreach (var file in sourceFiles)
            {
                try { sourceBytes += new FileInfo(file).Length; } catch (Exception) { /* boyut alınamayan dosya sayılmaz */ }
            }

            long free;
            try
            {
                free = new DriveInfo(Path.GetPathRoot(workDir)).AvailableFreeSpace;
            }
            catch (Exception)
            {
                return;
            }

            if (free < sourceBytes)
            {
                throw new InvalidOperationException(
                    Loc.T("Engine_NoTempSpace", NotificationMessages.FormatSize(sourceBytes), NotificationMessages.FormatSize(free), workDir));
            }

            if (free < sourceBytes * 2)
            {
                report(Loc.T("Engine_LowTempSpace", NotificationMessages.FormatSize(free), NotificationMessages.FormatSize(sourceBytes)));
            }
        }

        private static string MakeRelative(string root, string fullPath)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(normalizedRoot.Length)
                : Path.GetFileName(fullPath);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string((name ?? "yedek").Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return cleaned.Length == 0 ? "yedek" : cleaned;
        }

        /// <summary>Bu çalışmaya ait geçici klasör; Cleanup yalnızca onu siler.</summary>
        private sealed class StagedSource
        {
            private readonly string _workDir;

            public StagedSource(string workDir)
            {
                _workDir = workDir;
            }

            public string UploadPath { get; set; }
            public int FileCount { get; set; }

            public void Cleanup()
            {
                try
                {
                    if (Directory.Exists(_workDir))
                    {
                        Directory.Delete(_workDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn(Loc.T("Log_StagingCleanupFailed", _workDir, ex.Message));
                }
            }
        }
    }
}
