using System;
using System.Collections.Generic;

namespace FalcrebCloudBackup.Core.Models
{
    public class DestinationRunResult
    {
        public CloudProviderType Provider { get; set; }
        public bool Success { get; set; }
        public string RemotePath { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>One execution of a job — what got backed up, where, and whether each destination succeeded.</summary>
    public class BackupRunResult
    {
        public Guid JobId { get; set; }

        /// <summary>Snapshot of the job's name at run time — kept even if the job is later renamed or removed.</summary>
        public string JobName { get; set; }

        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }

        public bool OverallSuccess { get; set; }
        public int FilesProcessed { get; set; }
        public long TotalBytes { get; set; }

        public List<DestinationRunResult> DestinationResults { get; set; } = new List<DestinationRunResult>();

        /// <summary>Set when something failed before per-destination upload even started (e.g. hashing, zipping).</summary>
        public string FatalError { get; set; }
    }
}
