using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Storage;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Newtonsoft.Json.Linq;

namespace FalcrebCloudBackup.CloudProviders
{
    /// <summary>
    /// OneDrive via plain Microsoft Graph REST calls (no Microsoft.Graph SDK — that pulls in a much
    /// larger dependency tree than a handful of JSON PUT/GET calls need). Auth is MSAL.NET's
    /// interactive system-browser flow, the same shape as GoogleDriveProvider's, with the token
    /// cache persisted (DPAPI-protected, via Microsoft.Identity.Client.Extensions.Msal) under
    /// AppPaths.CloudTokensDirectory so later runs — including the silent one Task Scheduler
    /// triggers — reuse it without a browser.
    ///
    /// Tarayıcı yalnızca <c>allowInteractiveLogin</c> ile (Bulut Bağlantıları ekranından) açılır;
    /// yedekleme çalışmaları kayıtlı oturum yoksa açık bir hatayla durur, asla tarayıcı bekleyerek
    /// takılı kalmaz.
    ///
    /// Scope is Files.ReadWrite. Graph's narrower Files.ReadWrite.AppFolder scope (which would have
    /// matched Google's least-privilege DriveFile scope more closely) is still marked "(preview)"
    /// and its /drive/special/approot addressing proved unreliable in testing — accessDenied even
    /// with the permission correctly consented. Files.ReadWrite is the standard, stable scope every
    /// OneDrive-integrated app uses; everything this app creates still lives under one dedicated
    /// "FalcrebCloudBackup" folder at the root, just not OS-enforced as off-limits to the rest of
    /// the app's own permission the way AppFolder would have been.
    /// </summary>
    public class OneDriveProvider : ICloudProvider
    {
        private const string GraphRoot = "https://graph.microsoft.com/v1.0";
        private const string RootFolderName = "FalcrebCloudBackup";
        private const int MaxChunkAttempts = 5;
        private static readonly string[] Scopes = { "Files.ReadWrite" };

        private static string NotConnectedMessage => Loc.T("OneDrive_NotConnected");

        private static string ClientConfigPath => Path.Combine(AppPaths.RootDirectory, "onedrive_client_config.json");
        private static string TokenCacheDirectory => Path.Combine(AppPaths.CloudTokensDirectory, "onedrive");

        // Varsayılan 100 sn zaman aşımı, yavaş hatta 5 MB'lık bir parça için bile dolabiliyordu
        // ("Bir görev iptal edildi" hatası) — parça başına 10 dakika.
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        private readonly bool _allowInteractiveLogin;
        private string _accessToken;

        // The app-folder special path (/drive/special/approot) can't reliably be chained with
        // /children or a :/path: suffix — resolving it once to a plain item id up front and using
        // standard /drive/items/{id}/... addressing from then on is what actually works.
        private string _appFolderId;

        public OneDriveProvider(bool allowInteractiveLogin = false)
        {
            _allowInteractiveLogin = allowInteractiveLogin;
        }

        public CloudProviderType ProviderType => CloudProviderType.OneDrive;
        public bool IsConnected => _accessToken != null;

        /// <summary>
        /// Etkileşimli girişte kullanılacak Microsoft adresini arayüze bildirir; uygulama yönetici
        /// yetkisiyle çalıştığı için başlattığı tarayıcı bazen açılır açılmaz kapanıyor — o durumda
        /// kullanıcı adresi kopyalayıp elle açabilsin diye.
        /// </summary>
        public Action<string> AuthUrlReported { get; set; }

        public static bool HasClientConfigFile() => File.Exists(ClientConfigPath);
        public static string ClientConfigFilePath => ClientConfigPath;

        /// <summary>Forgets the locally stored token cache so the next connect prompts for sign-in again (e.g. to switch accounts). Only removes a cached credential, never any backup data.</summary>
        public static void Disconnect()
        {
            if (Directory.Exists(TokenCacheDirectory))
            {
                Directory.Delete(TokenCacheDirectory, recursive: true);
            }
        }

        public static bool HasStoredCredentials() =>
            Directory.Exists(TokenCacheDirectory) && Directory.GetFiles(TokenCacheDirectory).Any();

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(ClientConfigPath))
            {
                throw new InvalidOperationException(
                    Loc.T("OneDrive_SetupMissing", ClientConfigPath));
            }

            var clientId = JObject.Parse(File.ReadAllText(ClientConfigPath))["ClientId"]?.ToString();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException(Loc.T("OneDrive_ClientIdMissing", ClientConfigPath));
            }

            var app = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount)
                .WithRedirectUri("http://localhost")
                .Build();

            Directory.CreateDirectory(TokenCacheDirectory);
            var storageProperties = new StorageCreationPropertiesBuilder("token_cache.bin", TokenCacheDirectory).Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
            AuthenticationResult result;
            try
            {
                result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                    .ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MsalUiRequiredException ex)
            {
                if (!_allowInteractiveLogin)
                {
                    throw new InvalidOperationException(NotConnectedMessage, ex);
                }

                // Without this, MSAL defaults to an embedded WebBrowser control (Internet
                // Explorer's engine) — Windows Server's "IE Enhanced Security Configuration"
                // blocks Microsoft's own login CDN inside that view. Forcing the system browser
                // (the same approach GoogleDriveProvider uses) avoids IE entirely.
                var webViewOptions = new SystemWebViewOptions
                {
                    OpenBrowserAsync = uri =>
                    {
                        AuthUrlReported?.Invoke(uri.AbsoluteUri);
                        TryOpenBrowser(uri.AbsoluteUri);
                        return Task.CompletedTask;
                    },
                    HtmlMessageSuccess = "<!DOCTYPE html><html lang=\"" + Loc.Current + "\"><head><meta charset=\"utf-8\"><title>FalcrebCloudBackup</title></head>" +
                                         "<body style=\"font-family:Segoe UI,Arial,sans-serif;text-align:center;padding:64px 24px\">" +
                                         "<h2>" + Loc.T("Browser_SuccessTitle") + "</h2><p>" + Loc.T("Browser_SuccessBody") + "</p></body></html>"
                };

                result = await app.AcquireTokenInteractive(Scopes)
                    .WithUseEmbeddedWebView(false)
                    .WithSystemWebViewOptions(webViewOptions)
                    .ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }

            _accessToken = result.AccessToken;
            _appFolderId = null;
        }

        public async Task<string> EnsureFolderAsync(string folderPath, CancellationToken cancellationToken)
        {
            EnsureConnected();
            await EnsureAppFolderIdAsync(cancellationToken).ConfigureAwait(false);

            var parentId = _appFolderId;
            foreach (var segment in folderPath.Split('/').Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                parentId = await CreateOrGetFolderAsync(parentId, segment, cancellationToken).ConfigureAwait(false);
            }

            return parentId;
        }

        /// <summary>Resolves (and, on first-ever use, creates) the app's dedicated top-level folder to a concrete item id.</summary>
        private async Task EnsureAppFolderIdAsync(CancellationToken cancellationToken)
        {
            if (_appFolderId != null)
            {
                return;
            }

            _appFolderId = await CreateOrGetFolderAsync("root", RootFolderName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns the child folder's item id, creating it first if it doesn't exist yet.</summary>
        private async Task<string> CreateOrGetFolderAsync(string parentId, string folderName, CancellationToken cancellationToken)
        {
            var existingId = await TryGetChildIdAsync(parentId, folderName, cancellationToken).ConfigureAwait(false);
            if (existingId != null)
            {
                return existingId;
            }

            var body = new JObject
            {
                ["name"] = folderName,
                ["folder"] = new JObject(),
                ["@microsoft.graph.conflictBehavior"] = "rename"
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, $"{GraphRoot}/me/drive/items/{parentId}/children"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(Loc.T("OneDrive_FolderFailed", folderName, Summarize(responseBody)));
                    }

                    return JObject.Parse(responseBody)["id"]?.ToString();
                }
            }
        }

        private async Task<string> TryGetChildIdAsync(string parentId, string childName, CancellationToken cancellationToken)
        {
            var url = $"{GraphRoot}/me/drive/items/{parentId}:/{Uri.EscapeDataString(childName)}";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JObject.Parse(responseBody)["id"]?.ToString();
                }
            }
        }

        /// <summary>
        /// Parçalı (upload session) yükleme. Her parça geçici bir ağ hatasında / zaman aşımında
        /// en fazla 5 kez yeniden denenir; yeniden denemeden önce sunucuya "nerede kaldık" diye
        /// sorulur ki sunucuya ulaşmış ama cevabı kaybolmuş bir parça ikinci kez gönderilmesin.
        /// Böylece 12 dakikalık bir yükleme, tek bir hıçkırık yüzünden baştan başlamaz.
        /// </summary>
        public async Task<string> UploadFileAsync(string localFilePath, string remoteFolderPath, IProgress<double> progress, CancellationToken cancellationToken)
        {
            EnsureConnected();

            // Re-resolves the folder path to an id — cheap (each segment is a single cached-ish
            // lookup) and keeps this method self-sufficient even if a caller skips EnsureFolderAsync.
            var folderId = await EnsureFolderAsync(remoteFolderPath, cancellationToken).ConfigureAwait(false);
            var uploadUrl = await CreateUploadSessionAsync(folderId, Path.GetFileName(localFilePath), cancellationToken).ConfigureAwait(false);

            const int chunkSize = 327_680 * 16; // 5 MB; Graph requires a multiple of 320 KiB per chunk.
            using (var stream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan))
            {
                var totalLength = stream.Length;
                var buffer = new byte[chunkSize];
                long sent = 0;
                var failures = 0;
                string itemId = null;

                while (sent < totalLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    stream.Position = sent;
                    var toRead = (int)Math.Min(chunkSize, totalLength - sent);
                    var read = await ReadFullyAsync(stream, buffer, toRead, cancellationToken).ConfigureAwait(false);

                    var outcome = await SendChunkAsync(uploadUrl, buffer, read, sent, totalLength, cancellationToken).ConfigureAwait(false);
                    if (outcome.Success)
                    {
                        sent += read;
                        failures = 0;
                        progress?.Report((double)sent / totalLength);

                        // The final chunk's response contains the created DriveItem (with its id).
                        if (outcome.StatusCode == HttpStatusCode.Created || outcome.StatusCode == HttpStatusCode.OK)
                        {
                            itemId = JObject.Parse(outcome.Body)["id"]?.ToString();
                            sent = totalLength;
                        }
                        continue;
                    }

                    if (!outcome.Retryable)
                    {
                        throw new InvalidOperationException(Loc.T("OneDrive_UploadFailed", (int)outcome.StatusCode, Summarize(outcome.Body)));
                    }

                    failures++;
                    var reason = outcome.Error != null ? FriendlyErrors.Describe(outcome.Error) : $"HTTP {(int)outcome.StatusCode}";
                    if (failures >= MaxChunkAttempts)
                    {
                        throw new InvalidOperationException(Loc.T("OneDrive_UploadFailedRepeatedly", MaxChunkAttempts, reason));
                    }

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, failures) * 5); // 10, 20, 40, 80 sn
                    AppLog.Warn(Loc.T("Log_OneDriveChunkRetry", reason, delay.TotalSeconds, failures + 1));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                    var nextOffset = await TryGetNextExpectedOffsetAsync(uploadUrl, cancellationToken).ConfigureAwait(false);
                    if (nextOffset.HasValue)
                    {
                        sent = Math.Min(nextOffset.Value, totalLength);
                    }
                }

                return itemId ?? folderId;
            }
        }

        private sealed class ChunkOutcome
        {
            public bool Success { get; set; }
            public bool Retryable { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public string Body { get; set; }
            public Exception Error { get; set; }
        }

        private static async Task<ChunkOutcome> SendChunkAsync(string uploadUrl, byte[] buffer, int count, long offset, long totalLength, CancellationToken cancellationToken)
        {
            try
            {
                using (var content = new ByteArrayContent(buffer, 0, count))
                {
                    content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, totalLength);
                    using (var response = await Http.PutAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var status = (int)response.StatusCode;
                        return new ChunkOutcome
                        {
                            Success = response.IsSuccessStatusCode,
                            // 416: sunucu bu aralığı zaten almış → offset yeniden sorulup devam edilir.
                            Retryable = status == 416 || status == 408 || status == 429 || status >= 500,
                            StatusCode = response.StatusCode,
                            Body = body
                        };
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                return new ChunkOutcome { Retryable = true, Error = ex };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Kullanıcı/zamanlayıcı iptali değil, HttpClient zaman aşımı.
                return new ChunkOutcome { Retryable = true, Error = ex };
            }
        }

        private static async Task<long?> TryGetNextExpectedOffsetAsync(string uploadUrl, CancellationToken cancellationToken)
        {
            try
            {
                using (var response = await Http.GetAsync(uploadUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var firstRange = JObject.Parse(body)["nextExpectedRanges"]?.FirstOrDefault()?.ToString();
                    if (string.IsNullOrEmpty(firstRange))
                    {
                        return null;
                    }

                    var start = firstRange.Split('-')[0];
                    return long.TryParse(start, out var offset) ? offset : (long?)null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var total = 0;
            while (total < count)
            {
                var read = await stream.ReadAsync(buffer, total, count - total, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private async Task<string> CreateUploadSessionAsync(string folderId, string fileName, CancellationToken cancellationToken)
        {
            var url = $"{GraphRoot}/me/drive/items/{folderId}:/{Uri.EscapeDataString(fileName)}:/createUploadSession";
            var body = new JObject
            {
                ["item"] = new JObject { ["@microsoft.graph.conflictBehavior"] = "rename" }
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(Loc.T("OneDrive_SessionFailed", Summarize(responseBody)));
                    }

                    return JObject.Parse(responseBody)["uploadUrl"]?.ToString();
                }
            }
        }

        /// <summary>Doğrulama için dosyayı kapanınca kendini silen bir geçici dosyaya indirir (belleğe değil — bkz. GoogleDriveProvider).</summary>
        public async Task<Stream> OpenReadForVerificationAsync(string remoteFilePath, CancellationToken cancellationToken)
        {
            EnsureConnected();

            // remoteFilePath here is the DriveItem id UploadFileAsync returned.
            var url = $"{GraphRoot}/me/drive/items/{remoteFilePath}/content";
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException(Loc.T("OneDrive_VerifyDownloadFailed", Summarize(error)));
                    }

                    var tempDir = Path.Combine(Path.GetTempPath(), "FalcrebCloudBackup");
                    Directory.CreateDirectory(tempDir);
                    var tempPath = Path.Combine(tempDir, $"verify_od_{Guid.NewGuid():N}.tmp");
                    var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, FileOptions.DeleteOnClose);
                    try
                    {
                        using (var network = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            await network.CopyToAsync(fileStream, 1 << 20, cancellationToken).ConfigureAwait(false);
                        }

                        fileStream.Position = 0;
                        return fileStream;
                    }
                    catch
                    {
                        fileStream.Dispose();
                        throw;
                    }
                }
            }
        }

        public async Task<CloudStorageQuota> GetStorageQuotaAsync(CancellationToken cancellationToken)
        {
            EnsureConnected();

            using (var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphRoot}/me/drive?$select=quota"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                using (var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(Loc.T("OneDrive_QuotaFailed", Summarize(body)));
                    }

                    var quota = JObject.Parse(body)["quota"];
                    return new CloudStorageQuota
                    {
                        Provider = CloudProviderType.OneDrive,
                        UsedBytes = quota?["used"]?.Value<long>() ?? 0,
                        TotalBytes = quota?["total"]?.Value<long>(),
                        FetchedUtc = DateTime.UtcNow
                    };
                }
            }
        }

        /// <summary>Graph'ın uzun JSON hata gövdesinden yalnızca anlamlı mesajı çıkarır.</summary>
        private static string Summarize(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return Loc.T("OneDrive_EmptyResponse");
            }

            try
            {
                var error = JObject.Parse(responseBody)["error"];
                var message = error?["message"]?.ToString();
                var code = error?["code"]?.ToString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return string.IsNullOrWhiteSpace(code) ? message : $"{message} ({code})";
                }
            }
            catch (Exception)
            {
                // JSON değilse olduğu gibi göster.
            }

            return responseBody.Length > 300 ? responseBody.Substring(0, 300) + "…" : responseBody;
        }

        private static void TryOpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_BrowserOpenFailed", ex.Message));
            }
        }

        private void EnsureConnected()
        {
            if (_accessToken == null)
            {
                throw new InvalidOperationException(Loc.T("OneDrive_NotConnectedInternal"));
            }
        }
    }
}
