using System;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>
    /// Bir bulut hesabının doluluk durumu. Uygulama hiçbir yedeği silmediği için kullanılan alan
    /// sürekli artar; alan dolduğunda yüklemeler başarısız olmaya başlar. Bu yüzden doluluk
    /// takip edilip kullanıcıya önceden gösterilir.
    /// </summary>
    public class CloudStorageQuota
    {
        public CloudProviderType Provider { get; set; }

        public long UsedBytes { get; set; }

        /// <summary>null = sınırsız ya da sağlayıcıdan öğrenilemedi.</summary>
        public long? TotalBytes { get; set; }

        public DateTime FetchedUtc { get; set; }

        /// <summary>"Alan azalıyor" uyarısının en son ne zaman gönderildiği — günde bir kez gönderilir.</summary>
        public DateTime? LastAlertUtc { get; set; }

        /// <summary>%85 dolulukta uyarılır: günlük ~400 MB'lık bir yedekle bu, haftalarca öncesinden haber vermek demek.</summary>
        public const double WarnThreshold = 0.85;

        public const double CriticalThreshold = 0.95;

        [JsonIgnore]
        public double? UsedFraction =>
            TotalBytes.HasValue && TotalBytes.Value > 0
                ? Math.Min(1.0, (double)UsedBytes / TotalBytes.Value)
                : (double?)null;

        [JsonIgnore]
        public long? FreeBytes => TotalBytes.HasValue ? Math.Max(0, TotalBytes.Value - UsedBytes) : (long?)null;

        [JsonIgnore]
        public bool IsLow => UsedFraction.HasValue && UsedFraction.Value >= WarnThreshold;

        [JsonIgnore]
        public bool IsCritical => UsedFraction.HasValue && UsedFraction.Value >= CriticalThreshold;
    }
}
