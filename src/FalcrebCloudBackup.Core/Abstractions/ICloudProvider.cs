using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Core.Abstractions
{
    /// <summary>
    /// One connected cloud destination (Google Drive, OneDrive, ...). Implementations only ever
    /// add data — there is intentionally no Delete/Remove method on this interface. Adding a new
    /// provider (Dropbox, Yandex Disk) means adding one new class here, nothing else changes.
    /// </summary>
    public interface ICloudProvider
    {
        CloudProviderType ProviderType { get; }

        /// <summary>Runs the OAuth flow (or refreshes an existing token) so calls below can be made.</summary>
        Task ConnectAsync(CancellationToken cancellationToken);

        bool IsConnected { get; }

        /// <summary>Creates the folder if missing and returns its remote path/id. Safe to call every run.</summary>
        Task<string> EnsureFolderAsync(string folderPath, CancellationToken cancellationToken);

        /// <summary>Uploads one local file under the given remote folder, returning the remote file identifier/path.</summary>
        Task<string> UploadFileAsync(string localFilePath, string remoteFolderPath, IProgress<double> progress, CancellationToken cancellationToken);

        /// <summary>Downloads the remote file's bytes back for a post-upload integrity check (see HashVerifier).</summary>
        Task<Stream> OpenReadForVerificationAsync(string remoteFilePath, CancellationToken cancellationToken);

        /// <summary>
        /// Hesabın doluluk durumu. Uygulama hiçbir yedeği silmediği için kullanılan alan sürekli
        /// artar; alan dolduğunda yüklemeler başarısız olur. Her başarılı yüklemeden sonra
        /// çağrılıp saklanır, kullanıcı önceden uyarılabilsin diye.
        /// </summary>
        Task<CloudStorageQuota> GetStorageQuotaAsync(CancellationToken cancellationToken);
    }
}
