using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>
    /// Bulut hesaplarının doluluk bilgisini saklar. Değer, her başarılı yüklemeden hemen sonra
    /// (bağlantı zaten açıkken, bedavaya) tazelenir; arayüz ve bildirimler bu kayıttan okur.
    /// Böylece ekranı her tazelediğinde ağa çıkan bir uygulama olmuyor.
    /// </summary>
    public static class CloudQuotaStore
    {
        private static string FilePath => Path.Combine(AppPaths.RootDirectory, "cloud_quota.json");

        public static List<CloudStorageQuota> LoadAll()
        {
            AppPaths.EnsureCreated();
            var json = StoreFile.ReadAllTextOrNull(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CloudStorageQuota>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<CloudStorageQuota>>(json) ?? new List<CloudStorageQuota>();
            }
            catch (JsonException ex)
            {
                AppLog.Warn(Loc.T("Log_QuotaFileReadFailed", ex.Message));
                return new List<CloudStorageQuota>();
            }
        }

        public static CloudStorageQuota Get(CloudProviderType provider) =>
            LoadAll().FirstOrDefault(q => q.Provider == provider);

        /// <summary>Sağlayıcının kaydını günceller; daha önce gönderilmiş uyarı zamanı korunur.</summary>
        public static void Save(CloudStorageQuota quota)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(FilePath, json =>
            {
                var all = Parse(json);
                var existing = all.FirstOrDefault(q => q.Provider == quota.Provider);
                if (existing != null)
                {
                    quota.LastAlertUtc = existing.LastAlertUtc;
                    all.Remove(existing);
                }

                all.Add(quota);
                return JsonConvert.SerializeObject(all, Formatting.Indented);
            });
        }

        public static void MarkAlerted(CloudProviderType provider, DateTime whenUtc)
        {
            AppPaths.EnsureCreated();
            StoreFile.Update(FilePath, json =>
            {
                var all = Parse(json);
                var existing = all.FirstOrDefault(q => q.Provider == provider);
                if (existing != null)
                {
                    existing.LastAlertUtc = whenUtc;
                }

                return JsonConvert.SerializeObject(all, Formatting.Indented);
            });
        }

        private static List<CloudStorageQuota> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CloudStorageQuota>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<CloudStorageQuota>>(json) ?? new List<CloudStorageQuota>();
            }
            catch (JsonException)
            {
                return new List<CloudStorageQuota>();
            }
        }
    }
}
