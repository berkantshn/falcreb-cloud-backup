using System;
using System.Linq;
using System.Text;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Storage;

namespace FalcrebCloudBackup.Core.Notifications
{
    /// <summary>E-posta/Telegram metinleri — düz metin, telefonda da okunabilir kısa satırlar.</summary>
    public static class NotificationMessages
    {
        public static string SubjectForRun(BackupRunResult result)
        {
            var status = result.OverallSuccess ? Loc.T("Notify_StatusOk") : Loc.T("Notify_StatusFailed");
            return Loc.T("Notify_Subject", result.JobName, status, Loc.FormatDateTime(result.StartedUtc.ToLocalTime()));
        }

        public static string BodyForRun(BackupRunResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(result.OverallSuccess ? Loc.T("Notify_BodyOk") : Loc.T("Notify_BodyFailed"));
            sb.AppendLine();
            sb.AppendLine(Loc.T("Notify_Job", result.JobName));
            sb.AppendLine(Loc.T("Notify_Server", Environment.MachineName));
            sb.AppendLine(Loc.T("Notify_Started", Loc.FormatDateTime(result.StartedUtc.ToLocalTime())));
            sb.AppendLine(Loc.T("Notify_Duration", FormatDuration(result.FinishedUtc - result.StartedUtc)));
            if (result.TotalBytes > 0)
            {
                sb.AppendLine(Loc.T("Notify_Size", FormatSize(result.TotalBytes)));
            }

            if (!string.IsNullOrWhiteSpace(result.FatalError))
            {
                sb.AppendLine();
                sb.AppendLine(Loc.T("Notify_Error", result.FatalError));
            }

            if (result.DestinationResults.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(Loc.T("Notify_Destinations"));
                foreach (var d in result.DestinationResults)
                {
                    var line = d.Success ? Loc.T("Result_Success") : Loc.T("Result_FailedWithReason", d.ErrorMessage);
                    sb.AppendLine($"  • {CloudProviderDisplayNames.Get(d.Provider)}: {line}");
                }
            }

            var quotaLines = QuotaLines();
            if (quotaLines != null)
            {
                sb.AppendLine();
                sb.Append(quotaLines);
            }

            sb.AppendLine();
            sb.AppendLine(Loc.T("Notify_SeeDetails"));
            return sb.ToString();
        }

        /// <summary>"12,4 GB / 100 GB kullanıldı (%12) · 87,6 GB boş" — alan öğrenilemediyse null.</summary>
        public static string QuotaSummary(CloudStorageQuota quota)
        {
            if (quota == null)
            {
                return null;
            }

            if (!quota.TotalBytes.HasValue)
            {
                return Loc.T("Quota_UsedNoLimit", FormatSize(quota.UsedBytes));
            }

            var percent = (quota.UsedFraction ?? 0) * 100;
            return Loc.T("Quota_Summary", FormatSize(quota.UsedBytes), FormatSize(quota.TotalBytes.Value), percent, FormatSize(quota.FreeBytes ?? 0));
        }

        /// <summary>
        /// Bildirim gövdesine eklenen bulut alanı özeti. Uygulama hiçbir yedeği silmediği için
        /// alan sürekli artar — kullanıcının bunu her bildirimde görmesi, dolmadan önce önlem
        /// alabilmesinin en doğal yolu.
        /// </summary>
        public static string QuotaLines()
        {
            var quotas = CloudQuotaStore.LoadAll();
            if (quotas.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Loc.T("Quota_Header"));
            foreach (var quota in quotas.OrderBy(q => q.Provider))
            {
                var warning = quota.IsCritical ? Loc.T("Quota_CriticalSuffix")
                    : quota.IsLow ? Loc.T("Quota_LowSuffix")
                    : string.Empty;
                sb.AppendLine($"  • {CloudProviderDisplayNames.Get(quota.Provider)}: {QuotaSummary(quota)}{warning}");
            }

            return sb.ToString();
        }

        public static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1) return Loc.T("Duration_HoursMinutes", (int)span.TotalHours, span.Minutes);
            if (span.TotalMinutes >= 1) return Loc.T("Duration_MinutesSeconds", (int)span.TotalMinutes, span.Seconds);
            return Loc.T("Duration_Seconds", Math.Max(0, (int)span.TotalSeconds));
        }

        public static string FormatSize(long bytes)
        {
            const long kb = 1024, mb = kb * 1024, gb = mb * 1024;
            if (bytes >= gb) return string.Format(Loc.Culture, "{0:0.##} GB", bytes / (double)gb);
            if (bytes >= mb) return string.Format(Loc.Culture, "{0:0.#} MB", bytes / (double)mb);
            if (bytes >= kb) return string.Format(Loc.Culture, "{0:0.#} KB", bytes / (double)kb);
            return Loc.T("Size_Bytes", bytes);
        }

        /// <summary>Geçmiş ekranındaki tek satırlık hedef özeti: "Google Drive: başarılı · OneDrive: başarısız — ...".</summary>
        public static string DestinationsLine(BackupRunResult result) =>
            string.Join(" · ", result.DestinationResults.Select(d =>
                $"{CloudProviderDisplayNames.Get(d.Provider)}: {(d.Success ? Loc.T("Result_Success") : Loc.T("Result_FailedWithReason", d.ErrorMessage))}"));
    }
}
