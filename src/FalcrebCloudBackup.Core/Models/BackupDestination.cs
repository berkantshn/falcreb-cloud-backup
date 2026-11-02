namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>One cloud target a job uploads to. A job can have several of these at once.</summary>
    public class BackupDestination
    {
        public CloudProviderType Provider { get; set; }

        /// <summary>Which connected account to use (a job may want a specific one if the user links more than one account per provider).</summary>
        public string AccountId { get; set; }

        /// <summary>Remote folder path/name under the provider root, e.g. "Muhasebe Yedekleri".</summary>
        public string RemoteFolderName { get; set; }
    }
}
