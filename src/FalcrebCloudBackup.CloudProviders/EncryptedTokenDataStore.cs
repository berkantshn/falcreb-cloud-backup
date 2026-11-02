using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Security;
using FalcrebCloudBackup.Core.Storage;
using Google.Apis.Json;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;

namespace FalcrebCloudBackup.CloudProviders
{
    /// <summary>
    /// Google OAuth token'ını düz JSON yerine SecretProtector ile (DPAPI, LocalMachine kapsamı)
    /// şifreli saklayan veri deposu. LocalMachine, diğer sırlarla aynı gerekçeyle seçildi: sessiz
    /// yedekleme Task Scheduler'ın açtığı ayrı bir oturumda, kimse giriş yapmadan da çalışabilir.
    ///
    /// Eski sürümlerin bıraktığı düz metin dosya ilk okumada şifreliye taşınır. Düz metin yalnızca
    /// şifreli kopya yazılıp geri okunarak doğrulandıktan sonra silinir; arada bir şey ters giderse
    /// token kaybolmaz, bir sonraki çalışma yeniden dener.
    ///
    /// Okunamayan dosya (bozuk, başka bilgisayardan kopyalanmış) token yok sayılır: sessiz çalışma
    /// "bağlı değil" mesajıyla biter ve tarayıcı açmaz, kullanıcı arayüzden yeniden bağlanır.
    /// </summary>
    public sealed class EncryptedTokenDataStore : IDataStore
    {
        private const string EncryptedExtension = ".dpapi";
        private const string TempExtension = ".tmp";

        private readonly string _folder;

        public EncryptedTokenDataStore(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("Folder is required.", nameof(folder));
            }

            _folder = folder;
            Directory.CreateDirectory(_folder);
        }

        public Task StoreAsync<T>(string key, T value)
        {
            var storedKey = StoredKey<T>(key);
            var json = NewtonsoftJsonSerializer.Instance.Serialize(value);

            // Şifreleme başarısız olursa hata yukarı çıkar; düz metne geri dönüş bilerek yok.
            WriteEncryptedAndVerify(EncryptedPath(storedKey), json);
            TryDeletePlaintext(PlaintextPath(storedKey));
            return Task.CompletedTask;
        }

        public Task<T> GetAsync<T>(string key)
        {
            var storedKey = StoredKey<T>(key);
            var encryptedPath = EncryptedPath(storedKey);
            var plaintextPath = PlaintextPath(storedKey);

            if (TryReadEncrypted<T>(encryptedPath, out var value))
            {
                // Taşıma sırasında kapanma olmuşsa geride kalan düz metin burada temizlenir.
                TryDeletePlaintext(plaintextPath);
                return Task.FromResult(value);
            }

            if (File.Exists(plaintextPath))
            {
                return Task.FromResult(ReadPlaintextAndMigrate<T>(plaintextPath, encryptedPath));
            }

            if (File.Exists(encryptedPath))
            {
                AppLog.Warn(Loc.T("Log_Token_Unreadable", Path.GetFileName(encryptedPath)));
            }

            return Task.FromResult(default(T));
        }

        public Task DeleteAsync<T>(string key)
        {
            var storedKey = StoredKey<T>(key);
            File.Delete(EncryptedPath(storedKey));
            File.Delete(EncryptedPath(storedKey) + TempExtension);
            File.Delete(PlaintextPath(storedKey));
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            if (Directory.Exists(_folder))
            {
                foreach (var file in Directory.GetFiles(_folder))
                {
                    File.Delete(file);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Klasörde gerçekten okunabilir bir token var mı? Ağa çıkmaz ve taşıma yapmaz; arayüzdeki
        /// "bağlı" göstergesi içindir. Bozuk dosya "bağlı değil" sayılır ki kullanıcı yeniden bağlansın.
        /// </summary>
        public static bool HasReadableToken(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return false;
            }

            return Directory.GetFiles(folder)
                .Where(file => !file.EndsWith(TempExtension, StringComparison.OrdinalIgnoreCase))
                .Any(IsReadableTokenFile);
        }

        private static bool IsReadableTokenFile(string path)
        {
            try
            {
                var isEncrypted = path.EndsWith(EncryptedExtension, StringComparison.OrdinalIgnoreCase);
                var json = isEncrypted
                    ? SecretProtector.Unprotect(StoreFile.ReadAllTextOrNull(path))
                    : File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json) && JObject.Parse(json) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadEncrypted<T>(string path, out T value)
        {
            value = default(T);
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var json = SecretProtector.Unprotect(StoreFile.ReadAllTextOrNull(path));
                return TryDeserialize(json, out value);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static T ReadPlaintextAndMigrate<T>(string plaintextPath, string encryptedPath)
        {
            string json;
            try
            {
                json = File.ReadAllText(plaintextPath);
            }
            catch (Exception)
            {
                AppLog.Warn(Loc.T("Log_Token_Unreadable", Path.GetFileName(plaintextPath)));
                return default(T);
            }

            if (!TryDeserialize<T>(json, out var value))
            {
                // Dosya silinmez; yeniden bağlanınca StoreAsync yerine şifreli olanı yazıp temizler.
                AppLog.Warn(Loc.T("Log_Token_Unreadable", Path.GetFileName(plaintextPath)));
                return default(T);
            }

            try
            {
                WriteEncryptedAndVerify(encryptedPath, json);
                if (TryDeletePlaintext(plaintextPath))
                {
                    AppLog.Info(Loc.T("Log_Token_Migrated"));
                }
            }
            catch (Exception ex)
            {
                // Token geçerli: bu çalışma düz metinle sürer, bir sonraki okuma yeniden dener.
                AppLog.Warn(Loc.T("Log_Token_MigrationFailed", ex.Message));
            }

            return value;
        }

        /// <summary>Yazar, geri okuyup çözer ve karşılaştırır. Eşleşmezse hata fırlatır; çağıran düz metne dokunmaz.</summary>
        private static void WriteEncryptedAndVerify(string encryptedPath, string json)
        {
            var stored = StoreFile.WriteAllTextAndReadBack(encryptedPath, SecretProtector.Protect(json));
            var roundTrip = SecretProtector.Unprotect(stored);
            if (!string.Equals(roundTrip, json, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(Loc.T("Err_Token_VerifyFailed"));
            }
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            value = default(T);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                value = NewtonsoftJsonSerializer.Instance.Deserialize<T>(json);
                return value != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryDeletePlaintext(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_Token_PlaintextDeleteFailed", ex.Message));
                return false;
            }
        }

        private static string StoredKey<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key MUST have a value.", nameof(key));
            }

            // FileDataStore ile aynı dosya adı; eski düz metin dosyası bu adla bulunur.
            return FileDataStore.GenerateStoredKey(key, typeof(T));
        }

        private string PlaintextPath(string storedKey) => Path.Combine(_folder, storedKey);

        private string EncryptedPath(string storedKey) => Path.Combine(_folder, storedKey + EncryptedExtension);
    }
}
