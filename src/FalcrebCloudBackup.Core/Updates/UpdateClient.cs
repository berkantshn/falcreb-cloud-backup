using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Storage;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Updates
{
    /// <summary>Doğrulanmış ve kilitli kurulum dosyası: kilit, dosya çalıştırılana kadar değiştirilmesini engeller.</summary>
    public sealed class VerifiedInstaller : IDisposable
    {
        private readonly FileStream _lock;

        internal VerifiedInstaller(string path, FileStream fileLock)
        {
            Path = path;
            _lock = fileLock;
        }

        public string Path { get; }

        /// <summary>
        /// Kurulumu sessizce başlatır. Dosyalar değişirken uygulama kapanmış olmalıdır.
        /// Uygulama kapanacağı için sonucu sistemdeki cmd.exe bekler: kurulum başarısız olursa
        /// veya hiç başlamazsa uygulamayı yeniden açar. Başarılı kurulumda açma işi kurulumundur.
        /// </summary>
        public void Start(string appExePath)
        {
            var log = System.IO.Path.Combine(AppPaths.LogsDirectory, "update-install.log");
            var system = Environment.SystemDirectory;
            Process.Start(new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(system, "cmd.exe"),
                Arguments = BuildCommandArguments(Path, $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LOG=\"{log}\"", appExePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = system,
            })?.Dispose();
        }

        public static string BuildCommandArguments(string installerPath, string installerArguments, string appExePath) =>
            $"/d /s /c \"start \"\" /wait \"{installerPath}\" {installerArguments} & if errorlevel 1 start \"\" \"{appExePath}\" {UpdateAttemptStore.FailedArgument}\"";

        public void Dispose() => _lock.Dispose();
    }

    /// <summary>Yeni sürümü denetler ve indirir. Kurulum kararı her zaman kullanıcınındır.</summary>
    public class UpdateClient
    {
        public const string ManifestUrl = "https://github.com/berkantshn/falcreb-cloud-backup/releases/latest/download/update.json";

        /// <summary>Sürüm imzalarını doğrulayan açık anahtar. Özel eşi kaynak kodda bulunmaz.</summary>
        public const string PublicKey = "jGc+47BAisfJtqnatQ81ShVYj0grUtUBUDIrAQ9EopJXv/Hvwb6wQj5Rd7zKjKQnlbkww2j+sT1sASar4IR19w==";

        private const long MaxInstallerSize = 200L * 1024 * 1024;
        private const int MaxManifestSize = 64 * 1024;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

        private readonly string _publicKey;
        private readonly string _manifestUrl;

        public UpdateClient(string publicKey = PublicKey, string manifestUrl = ManifestUrl)
        {
            _publicKey = publicKey;
            _manifestUrl = manifestUrl;
        }

        private static string UpdatesDirectory => Path.Combine(AppPaths.RootDirectory, "updates");

        /// <summary>Doğrulanmış ve daha yeni bir sürüm varsa onu, yoksa null döner.</summary>
        public async Task<UpdateManifest> CheckAsync(Version current, CancellationToken cancellationToken)
        {
            using (var response = await Http.GetAsync(_manifestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxManifestSize)
                {
                    throw new InvalidDataException("Update manifest is too large.");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
                var problem = UpdateVerifier.Validate(manifest, _publicKey);
                if (problem != null)
                {
                    throw new InvalidDataException("Update manifest rejected: " + problem);
                }

                return UpdateVerifier.IsNewer(manifest.Version, current) ? manifest : null;
            }
        }

        /// <summary>Kurulum dosyasını indirir; boyut ve özeti tutmazsa dosyayı siler ve hata verir.</summary>
        public async Task<VerifiedInstaller> DownloadAsync(UpdateManifest manifest, CancellationToken cancellationToken)
        {
            var problem = UpdateVerifier.Validate(manifest, _publicKey);
            if (problem != null)
            {
                throw new InvalidDataException("Update manifest rejected: " + problem);
            }

            if (manifest.Size > MaxInstallerSize)
            {
                throw new InvalidDataException("Update installer is too large.");
            }

            var directory = PrepareDirectory();
            var path = Path.Combine(directory, "FalcrebCloudBackup-Setup.exe");
            using (var response = await Http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > manifest.Size)
                        {
                            throw new InvalidDataException("Downloaded file is larger than expected.");
                        }

                        await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Doğrulama, dosya yazmaya kapalı bir kilit altında yapılır; kilit çalıştırılana
            // kadar sürer, yani doğrulanan dosya ile çalıştırılan dosya aynıdır.
            var fileLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                var hash = UpdateVerifier.ComputeSha256(fileLock);
                if (fileLock.Length != manifest.Size || !string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Downloaded file failed verification.");
                }
            }
            catch
            {
                fileLock.Dispose();
                File.Delete(path);
                throw;
            }

            return new VerifiedInstaller(path, fileLock);
        }

        /// <summary>
        /// Uygulama yönetici olarak çalıştığı için kurulum dosyası da yönetici olarak açılır.
        /// Bu yüzden indirme klasörüne yalnızca Administrators ve SYSTEM yazabilir.
        /// </summary>
        private static string PrepareDirectory()
        {
            Directory.CreateDirectory(UpdatesDirectory);
            foreach (var old in Directory.GetDirectories(UpdatesDirectory))
            {
                try
                {
                    Directory.Delete(old, true);
                }
                catch (IOException)
                {
                    // Eski bir kurulum hâlâ çalışıyor olabilir; bir sonraki denetimde silinir.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(sid, null),
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }

            var path = Path.Combine(UpdatesDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path, security);
            return path;
        }
    }
}
