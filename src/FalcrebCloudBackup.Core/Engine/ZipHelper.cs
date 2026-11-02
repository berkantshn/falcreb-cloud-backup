using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using FalcrebCloudBackup.Core.Localization;

namespace FalcrebCloudBackup.Core.Engine
{
    /// <summary>
    /// ZipFile.CreateFromDirectory yerine kendi zip'leme rutini: kaynak dosyalar paylaşımlı
    /// (ReadWrite) açılır ki başka bir programın açık tuttuğu sıradan dosyalar (log, ayar vb.)
    /// yedeği durdurmasın; kilitli kalan bir dosya olursa hata mesajı kullanıcının anlayacağı
    /// dille verilir. Büyük dosyalarda hızlı sıkıştırma seçilir — yüzlerce MB'lık bir
    /// veritabanı yedeğinde "en iyi" sıkıştırma dakikalarca fark yaratıp boyutta çok az kazandırır.
    /// </summary>
    public static class ZipHelper
    {
        private const long LargeFileThreshold = 64L * 1024 * 1024;

        public sealed class Entry
        {
            public string EntryName { get; set; }
            public string FilePath { get; set; }
        }

        public static void Create(string zipPath, IEnumerable<Entry> entries, CancellationToken cancellationToken)
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var info = new FileInfo(entry.FilePath);
                    var level = info.Length > LargeFileThreshold ? CompressionLevel.Fastest : CompressionLevel.Optimal;
                    var zipEntry = archive.CreateEntry(entry.EntryName, level);
                    zipEntry.LastWriteTime = ClampToZipRange(info.LastWriteTime);

                    using (var source = OpenShared(entry.FilePath))
                    using (var target = zipEntry.Open())
                    {
                        source.CopyTo(target, 1 << 20);
                    }
                }
            }
        }

        public static FileStream OpenShared(string path)
        {
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, FileOptions.SequentialScan);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                throw new IOException(DescribeLockedFile(path), ex);
            }
        }

        /// <summary>Win32 ERROR_SHARING_VIOLATION (32) / ERROR_LOCK_VIOLATION (33).</summary>
        public static bool IsSharingViolation(IOException ex)
        {
            var code = ex.HResult & 0xFFFF;
            return code == 32 || code == 33;
        }

        private static string DescribeLockedFile(string path)
        {
            var name = Path.GetFileName(path);
            return FirebirdBackupTool.IsDatabaseFile(path)
                ? Loc.T("Zip_LockedDatabase", name)
                : Loc.T("Zip_LockedFile", name);
        }

        private static DateTimeOffset ClampToZipRange(DateTime value)
        {
            // Zip biçimi 1980–2107 dışındaki tarihleri saklayamaz; ZipArchiveEntry aksi halde hata fırlatır.
            if (value.Year < 1980) return new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            if (value.Year > 2107) return new DateTimeOffset(2107, 12, 31, 23, 59, 58, TimeSpan.Zero);
            return value;
        }
    }
}
