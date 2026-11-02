using System.Collections.Generic;

namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>Human-readable provider names for anything shown to the user — enum.ToString() gives "GoogleDrive", not "Google Drive".</summary>
    public static class CloudProviderDisplayNames
    {
        private static readonly Dictionary<CloudProviderType, string> Names = new Dictionary<CloudProviderType, string>
        {
            [CloudProviderType.GoogleDrive] = "Google Drive",
            [CloudProviderType.OneDrive] = "OneDrive",
            [CloudProviderType.Dropbox] = "Dropbox",
            [CloudProviderType.YandexDisk] = "Yandex Disk"
        };

        public static string Get(CloudProviderType provider) =>
            Names.TryGetValue(provider, out var name) ? name : provider.ToString();
    }
}
