using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Storage;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace FalcrebCloudBackup.CloudProviders
{
    /// <summary>
    /// Google Drive via the official Drive v3 SDK. Uses the "installed app" OAuth flow: the first
    /// connect (from the Bulut Bağlantıları window, with <c>allowInteractiveLogin</c>) opens the
    /// user's browser for consent, then the refresh token is cached (DPAPI-encrypted, see
    /// EncryptedTokenDataStore) under AppPaths.CloudTokensDirectory so every later run reuses it
    /// without any browser or prompt.
    ///
    /// Yedekleme çalışmaları (zamanlanmış, izleyici, "Şimdi Çalıştır") tarayıcı açmaya *asla*
    /// izin vermez: gece 01:00'de kimsenin olmadığı sunucuda tarayıcı beklemek, işlemin sonsuza
    /// kadar takılı kalması demektir Kayıtlı oturum yoksa ya da geçersizse
    /// açık bir hata mesajıyla başarısız olur, kullanıcı arayüzden yeniden bağlanır.
    ///
    /// Scope is deliberately DriveFile, not full Drive access — this app can only see/manage the
    /// files and folders it creates itself, never the rest of the user's Drive.
    /// </summary>
    public class GoogleDriveProvider : ICloudProvider
    {
        private const string ApplicationName = "FalcrebCloudBackup";
        private const string TokenUserKey = "user";
        private const int MaxUploadAttempts = 5;
        private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(10);

        private static string NotConnectedMessage => Loc.T("Google_NotConnected");

        private static string ClientSecretPath => Path.Combine(AppPaths.RootDirectory, "google_client_secret.json");
        private static string TokenStorePath => Path.Combine(AppPaths.CloudTokensDirectory, "google_drive");

        private readonly bool _allowInteractiveLogin;
        private DriveService _driveService;

        public GoogleDriveProvider(bool allowInteractiveLogin = false)
        {
            _allowInteractiveLogin = allowInteractiveLogin;
        }

        public CloudProviderType ProviderType => CloudProviderType.GoogleDrive;
        public bool IsConnected => _driveService != null;

        /// <summary>
        /// Etkileşimli girişte kullanılacak Google adresini arayüze bildirir; tarayıcı kendiliğinden
        /// açılamazsa kullanıcı adresi kopyalayıp elle açabilsin diye (bkz. LocalBrowserCodeReceiver).
        /// </summary>
        public Action<string> AuthUrlReported { get; set; }

        /// <summary>A readable stored refresh token doesn't guarantee it's still valid (it may have been revoked), but it's a fair "looks connected" signal for a status display without making a network call. An unreadable token file counts as not connected, so the user is prompted to reconnect.</summary>
        public static bool HasStoredCredentials() => EncryptedTokenDataStore.HasReadableToken(TokenStorePath);

        public static bool HasClientSecretFile() => File.Exists(ClientSecretPath);

        public static string ClientSecretFilePath => ClientSecretPath;

        /// <summary>Forgets the locally stored refresh token so the next connect prompts for sign-in again (e.g. to switch accounts). Only removes a cached credential, never any backup data.</summary>
        public static void Disconnect()
        {
            if (Directory.Exists(TokenStorePath))
            {
                Directory.Delete(TokenStorePath, recursive: true);
            }
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(ClientSecretPath))
            {
                throw new InvalidOperationException(
                    Loc.T("Google_SetupMissing", ClientSecretPath));
            }

            AppPaths.EnsureCreated();

            ClientSecrets secrets;
            using (var stream = new FileStream(ClientSecretPath, FileMode.Open, FileAccess.Read))
            {
                secrets = GoogleClientSecrets.FromStream(stream).Secrets;
            }

            var dataStore = new EncryptedTokenDataStore(TokenStorePath);
            UserCredential credential;

            if (_allowInteractiveLogin)
            {
                var receiver = new LocalBrowserCodeReceiver(AuthUrlReported);
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, Scopes, TokenUserKey, cancellationToken, dataStore, receiver).ConfigureAwait(false);
            }
            else
            {
                // AuthorizeAsync'in tarayıcı açan dalını atlayıp yalnızca kayıtlı token'ı kullanır.
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = secrets,
                    Scopes = Scopes,
                    DataStore = dataStore
                });

                var token = await flow.LoadTokenAsync(TokenUserKey, cancellationToken).ConfigureAwait(false);
                if (token == null || (string.IsNullOrEmpty(token.RefreshToken) && token.IsStale))
                {
                    throw new InvalidOperationException(NotConnectedMessage);
                }

                credential = new UserCredential(flow, TokenUserKey, token);
                try
                {
                    // Erken doğrulama: yenileme token'ı iptal edilmişse sorunu yüklemenin ortasında
                    // değil burada, anlaşılır bir mesajla yakala.
                    await credential.GetAccessTokenForRequestAsync(null, cancellationToken).ConfigureAwait(false);
                }
                catch (TokenResponseException ex)
                {
                    throw new InvalidOperationException(
                        Loc.T("Google_SessionInvalid", ex.Error?.Error ?? ex.Message), ex);
                }
            }

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
            // Varsayılan 100 sn, yavaş bir hatta 8 MB'lık tek parça için bile yetmeyebiliyor.
            _driveService.HttpClient.Timeout = HttpTimeout;
        }

        public async Task<string> EnsureFolderAsync(string folderPath, CancellationToken cancellationToken)
        {
            EnsureConnected();
            return await ResolveFolderIdAsync(folderPath, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> UploadFileAsync(string localFilePath, string remoteFolderPath, IProgress<double> progress, CancellationToken cancellationToken)
        {
            EnsureConnected();

            // Idempotent: works even if a caller uploads without calling EnsureFolderAsync first —
            // the second "find or create" here just finds what's already there.
            var folderId = await ResolveFolderIdAsync(remoteFolderPath, cancellationToken).ConfigureAwait(false);

            var fileMetadata = new DriveFile
            {
                Name = Path.GetFileName(localFilePath),
                Parents = new List<string> { folderId }
            };

            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan))
            {
                var request = _driveService.Files.Create(fileMetadata, fileStream, GuessMimeType(localFilePath));
                request.Fields = "id";
                // 256 KB'ın katı olmalı; 8 MB, 10 dakikalık zaman aşımıyla çok yavaş hatlarda bile güvenli.
                request.ChunkSize = 32 * ResumableUpload.MinimumChunkSize;

                if (progress != null)
                {
                    var totalBytes = fileStream.Length;
                    request.ProgressChanged += p => progress.Report(totalBytes == 0 ? 1.0 : (double)p.BytesSent / totalBytes);
                }

                var upload = await request.UploadAsync(cancellationToken).ConfigureAwait(false);
                var attempt = 1;
                while (upload.Status != UploadStatus.Completed)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (attempt >= MaxUploadAttempts)
                    {
                        throw new InvalidOperationException(
                            Loc.T("Google_UploadFailed", MaxUploadAttempts, FriendlyErrors.Describe(upload.Exception ?? new Exception(Loc.T("Common_UnknownError")))));
                    }

                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 5); // 20, 40, 80, 160 sn
                    AppLog.Warn(Loc.T("Log_GoogleUploadRetry", upload.Exception?.Message, delay.TotalSeconds, attempt));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                    // Aynı yükleme oturumunu sürdürür; oturum hiç açılamamışsa SDK baştan başlar.
                    upload = await request.ResumeAsync(cancellationToken).ConfigureAwait(false);
                }

                return request.ResponseBody.Id;
            }
        }

        /// <summary>
        /// Doğrulama için dosyayı belleğe değil, kapanınca kendini silen bir geçici dosyaya indirir —
        /// GB boyutundaki bir veritabanı yedeğini MemoryStream'e almak 2 GB sınırında çöker.
        /// </summary>
        public async Task<Stream> OpenReadForVerificationAsync(string remoteFilePath, CancellationToken cancellationToken)
        {
            EnsureConnected();

            var tempDir = Path.Combine(Path.GetTempPath(), "FalcrebCloudBackup");
            Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, $"verify_gd_{Guid.NewGuid():N}.tmp");
            var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, FileOptions.DeleteOnClose);
            try
            {
                // remoteFilePath is the Drive file id UploadFileAsync returned.
                var download = await _driveService.Files.Get(remoteFilePath).DownloadAsync(stream, cancellationToken).ConfigureAwait(false);
                if (download.Status != DownloadStatus.Completed)
                {
                    throw new InvalidOperationException(Loc.T("Google_VerifyDownloadFailed", FriendlyErrors.Describe(download.Exception ?? new Exception(Loc.T("Common_UnknownError")))));
                }

                stream.Position = 0;
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public async Task<CloudStorageQuota> GetStorageQuotaAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();

            var request = _driveService.About.Get();
            request.Fields = "storageQuota";
            var about = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            return new CloudStorageQuota
            {
                Provider = CloudProviderType.GoogleDrive,
                // Usage, Drive + Gmail + Fotoğraflar toplamıdır — hesabın gerçek doluluğu budur.
                UsedBytes = about.StorageQuota?.Usage ?? 0,
                // Limit null ise hesap sınırsızdır (bazı kurumsal hesaplar).
                TotalBytes = about.StorageQuota?.Limit,
                FetchedUtc = DateTime.UtcNow
            };
        }

        private async Task<string> ResolveFolderIdAsync(string folderPath, CancellationToken cancellationToken)
        {
            var parentId = "root";
            foreach (var segment in folderPath.Split('/').Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                parentId = await FindOrCreateFolderAsync(segment, parentId, cancellationToken).ConfigureAwait(false);
            }
            return parentId;
        }

        private async Task<string> FindOrCreateFolderAsync(string name, string parentId, CancellationToken cancellationToken)
        {
            var listRequest = _driveService.Files.List();
            listRequest.Q = $"name = '{EscapeForQuery(name)}' and '{parentId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            listRequest.Fields = "files(id, name)";
            listRequest.Spaces = "drive";

            var listResult = await listRequest.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            var existing = listResult.Files?.FirstOrDefault();
            if (existing != null)
            {
                return existing.Id;
            }

            var folderMetadata = new DriveFile
            {
                Name = name,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { parentId }
            };

            var createRequest = _driveService.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var created = await createRequest.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return created.Id;
        }

        private static string EscapeForQuery(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

        private static string GuessMimeType(string filePath) =>
            Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                ? "application/zip"
                : "application/octet-stream";

        private void EnsureConnected()
        {
            if (_driveService == null)
            {
                throw new InvalidOperationException(Loc.T("Google_NotConnectedInternal"));
            }
        }
    }
}
