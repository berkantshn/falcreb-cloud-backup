using System;
using System.IO;
using System.Threading;
using FalcrebCloudBackup.Core.Localization;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>
    /// Ortak JSON dosyaları (jobs.json, history.json, bildirim ayarları) için süreçler arası
    /// güvenli okuma/yazma. Etkileşimli uygulama ile Task Scheduler'ın başlattığı sessiz
    /// yedekleme ayrı işlemlerdir ve aynı dosyaya aynı anda yazabilir; C#'taki sıradan
    /// <c>lock</c> yalnızca tek işlem içinde geçerli olduğundan burada adlandırılmış (Global)
    /// bir Mutex kullanılır. Yazma işlemi önce geçici dosyaya yapılıp sonra yerine konur —
    /// böylece yazma sırasında elektrik kesilse bile yarım/bozuk bir dosya kalmaz.
    /// </summary>
    public static class StoreFile
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

        public static string ReadAllTextOrNull(string path)
        {
            using (AcquireLock(path))
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
        }

        public static void WriteAllText(string path, string content)
        {
            using (AcquireLock(path))
            {
                WriteAtomic(path, content);
            }
        }

        /// <summary>Yazar ve aynı kilit altında geri okur; arada başka bir işlem dosyayı değiştiremez (doğrulama için).</summary>
        public static string WriteAllTextAndReadBack(string path, string content)
        {
            using (AcquireLock(path))
            {
                WriteAtomic(path, content);
                return File.ReadAllText(path);
            }
        }

        /// <summary>Oku-değiştir-yaz döngüsünü tek kilit altında çalıştırır (örn. geçmişe kayıt ekleme).</summary>
        public static void Update(string path, Func<string, string> transform)
        {
            using (AcquireLock(path))
            {
                var current = File.Exists(path) ? File.ReadAllText(path) : null;
                WriteAtomic(path, transform(current));
            }
        }

        /// <summary>Bozuk (yarım yazılmış) bir dosyayı silmek yerine kenara alır; hiçbir veri kaybolmaz, sadece yeniden başlanır.</summary>
        public static void QuarantineCorruptFile(string path)
        {
            using (AcquireLock(path))
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var quarantined = $"{path}.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Move(path, quarantined);
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, content);

            if (File.Exists(path))
            {
                // File.Replace NTFS üzerinde atomiktir: ya eski dosya kalır ya yenisi, arası yoktur.
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private static IDisposable AcquireLock(string path)
        {
            // Mutex adı dosya adına göre türetilir; yol ayırıcıları mutex adında geçersiz olduğu için temizlenir.
            var name = @"Global\FalcrebCloudBackup_Store_" + Path.GetFileName(path).Replace('\\', '_').Replace('/', '_');
            return new NamedLock(name, LockTimeout);
        }

        private sealed class NamedLock : IDisposable
        {
            private readonly Mutex _mutex;

            public NamedLock(string name, TimeSpan timeout)
            {
                _mutex = new Mutex(false, name);
                try
                {
                    if (!_mutex.WaitOne(timeout))
                    {
                        _mutex.Dispose();
                        throw new TimeoutException(Loc.T("Err_StoreLockTimeout", timeout.TotalSeconds, name));
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Önceki sahibi kilidi bırakmadan kapanmış (çökmüş); kilit artık bizde, devam edilebilir.
                }
            }

            public void Dispose()
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
