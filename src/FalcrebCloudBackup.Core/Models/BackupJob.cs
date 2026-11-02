using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using FalcrebCloudBackup.Core.Localization;

namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>
    /// One backup definition: a local source (e.g. the accounting program's backup folder)
    /// and everywhere it should end up. There is deliberately no "delete" or "prune" setting
    /// anywhere on this model — see FalcrebCloudBackup.Core.Engine.BackupEngine for why.
    /// </summary>
    public class BackupJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }

        /// <summary>Folder to watch/back up. Can also point at a single file.</summary>
        public string SourcePath { get; set; }
        public bool SourceIsFolder { get; set; } = true;

        public List<BackupDestination> Destinations { get; set; } = new List<BackupDestination>();

        public BackupSchedule Schedule { get; set; } = new BackupSchedule();

        /// <summary>Canlı Firebird veritabanları için gbak ayarları (bkz. FirebirdBackupSettings).</summary>
        public FirebirdBackupSettings Firebird { get; set; } = new FirebirdBackupSettings();

        /// <summary>React immediately when the accounting software drops a new backup file in SourcePath.</summary>
        public bool WatchForChanges { get; set; } = true;

        /// <summary>
        /// Zamanlanmış çalışma kaçırıldıysa (sunucu o saatte kapalıydı — elektrik kesintisi, bakım)
        /// uygulama açıldıktan birkaç dakika sonra yedeklemeyi kendiliğinden telafi eder. Aksi halde
        /// bir sonraki güne kadar hiç yedek alınmaz.
        /// </summary>
        public bool CatchUpMissedRuns { get; set; } = true;

        /// <summary>Zip the source into a single archive before uploading.</summary>
        public bool ZipBeforeUpload { get; set; } = true;

        /// <summary>Compute a SHA-256 of what was uploaded and compare against the local file.</summary>
        public bool VerifyHashAfterUpload { get; set; } = true;

        /// <summary>
        /// Upload into a timestamped subfolder (e.g. "2026-08-25_14-00") instead of overwriting
        /// the previous run's files. This is what keeps prior backups intact without needing any
        /// delete/retention logic.
        /// </summary>
        public bool UseTimestampedFolders { get; set; } = true;

        public bool Enabled { get; set; } = true;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Son çalışma bilgileri — sessiz (Task Scheduler) çalışma da bunları yazar, arayüz buradan okur.
        public DateTime? LastRunUtc { get; set; }
        public bool? LastRunSuccess { get; set; }
        public string LastRunError { get; set; }
        public DateTime? LastSuccessUtc { get; set; }

        /// <summary>"Uzun süredir yedek yok" uyarısının en son ne zaman gönderildiği — günde bir kez gönderilir.</summary>
        public DateTime? LastStaleAlertUtc { get; set; }

        /// <summary>Kaçırılan çalışmanın en son ne zaman telafi edilmeye çalışıldığı — sürekli yeniden denemeyi önler.</summary>
        public DateTime? LastCatchUpUtc { get; set; }

        /// <summary>Display-only summary (e.g. "Google Drive, OneDrive"); not persisted.</summary>
        [JsonIgnore]
        public string DestinationsSummary => string.Join(", ", Destinations.Select(d => CloudProviderDisplayNames.Get(d.Provider)));

        [JsonIgnore]
        public string LastRunSummary => LastRunUtc.HasValue
            ? Loc.T("Job_LastRun", Loc.FormatDateTime(LastRunUtc.Value.ToLocalTime()))
            : Loc.T("Health_NeverRan");

        /// <summary>
        /// Yalnızca kullanıcının düzenleyebildiği alanları kopyalar; son çalışma bilgileri
        /// (LastRun*, LastSuccessUtc, LastStaleAlertUtc) olduğu gibi kalır. Düzenleme penceresi
        /// açıkken gece yedeklemesi çalışmış olabilir — kaydederken o bilgi ezilmemeli.
        /// </summary>
        public void CopySettingsFrom(BackupJob other)
        {
            Name = other.Name;
            SourcePath = other.SourcePath;
            SourceIsFolder = other.SourceIsFolder;
            Destinations = other.Destinations;
            Schedule = other.Schedule;
            Firebird = other.Firebird;
            WatchForChanges = other.WatchForChanges;
            CatchUpMissedRuns = other.CatchUpMissedRuns;
            ZipBeforeUpload = other.ZipBeforeUpload;
            VerifyHashAfterUpload = other.VerifyHashAfterUpload;
            UseTimestampedFolders = other.UseTimestampedFolders;
            Enabled = other.Enabled;
        }
    }
}
