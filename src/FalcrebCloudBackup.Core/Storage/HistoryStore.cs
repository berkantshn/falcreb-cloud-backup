using System.Collections.Generic;
using System.IO;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>
    /// Yalnızca ekleme yapılan çalışma geçmişi — her BackupEngine çalışması buraya bir kayıt
    /// ekler, hiçbir şey silmez (uygulamanın "yedekle ilgili hiçbir şeyi silme" ilkesiyle uyumlu).
    /// </summary>
    public class HistoryStore
    {
        private static string FilePath => Path.Combine(AppPaths.HistoryDirectory, "history.json");

        public List<BackupRunResult> LoadAll()
        {
            AppPaths.EnsureCreated();
            var json = StoreFile.ReadAllTextOrNull(FilePath);
            return Parse(json);
        }

        public void Append(BackupRunResult result)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(FilePath, json =>
            {
                var all = Parse(json);
                all.Add(result);
                return JsonConvert.SerializeObject(all, Formatting.Indented);
            });
        }

        private static List<BackupRunResult> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<BackupRunResult>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<BackupRunResult>>(json) ?? new List<BackupRunResult>();
            }
            catch (JsonException ex)
            {
                // Geçmiş bilgi amaçlıdır; bozuk bir dosya yüzünden yedekleme motorunun durması kabul
                // edilemez. Dosya kenara alınır (silinmez), geçmiş sıfırdan başlar.
                AppLog.Error(Loc.T("Log_HistoryCorrupt", FilePath), ex);
                StoreFile.QuarantineCorruptFile(FilePath);
                return new List<BackupRunResult>();
            }
        }
    }
}
