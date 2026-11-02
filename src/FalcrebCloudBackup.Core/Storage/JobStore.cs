using System;
using System.Collections.Generic;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>Görev listesini AppPaths.JobsFilePath altında JSON olarak okur/yazar (süreçler arası kilitli, atomik yazım — bkz. StoreFile).</summary>
    public class JobStore
    {
        public List<BackupJob> LoadAll()
        {
            AppPaths.EnsureCreated();
            var json = StoreFile.ReadAllTextOrNull(AppPaths.JobsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<BackupJob>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<BackupJob>>(json) ?? new List<BackupJob>();
            }
            catch (JsonException ex)
            {
                // Görev listesi kaybolursa sessizce boş liste dönmek yedeklemelerin fark edilmeden
                // durması demektir — bunun yerine açıkça hata verilir, dosya kenara alınmaz.
                throw new InvalidOperationException(Loc.T("Err_JobsFileRead", AppPaths.JobsFilePath, ex.Message), ex);
            }
        }

        public void SaveAll(List<BackupJob> jobs)
        {
            AppPaths.EnsureCreated();
            StoreFile.WriteAllText(AppPaths.JobsFilePath, JsonConvert.SerializeObject(jobs, Formatting.Indented));
        }

        public void Add(BackupJob job)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(AppPaths.JobsFilePath, json =>
            {
                var jobs = Parse(json);
                jobs.Add(job);
                return JsonConvert.SerializeObject(jobs, Formatting.Indented);
            });
        }

        public void Remove(Guid jobId)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(AppPaths.JobsFilePath, json =>
            {
                var jobs = Parse(json);
                jobs.RemoveAll(j => j.Id == jobId);
                return JsonConvert.SerializeObject(jobs, Formatting.Indented);
            });
        }

        private static List<BackupJob> Parse(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? new List<BackupJob>()
                : JsonConvert.DeserializeObject<List<BackupJob>>(json) ?? new List<BackupJob>();

        /// <summary>
        /// Yalnızca tek bir görevi, dosyadaki güncel haliyle birleştirerek günceller. Arayüzdeki
        /// eski bir kopyanın, sessiz yedeklemenin bu arada yazdığı "son çalışma" bilgilerini
        /// ezmemesi için kullanılır.
        /// </summary>
        public void Update(Guid jobId, Action<BackupJob> mutate)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(AppPaths.JobsFilePath, json =>
            {
                var jobs = Parse(json);
                var stored = jobs.Find(j => j.Id == jobId);
                if (stored != null)
                {
                    mutate(stored);
                }

                return JsonConvert.SerializeObject(jobs, Formatting.Indented);
            });
        }
    }
}
