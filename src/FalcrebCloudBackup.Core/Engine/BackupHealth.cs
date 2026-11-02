using System;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Core.Engine
{
    public enum HealthState
    {
        Disabled,
        ManualOnly,
        NeverRan,
        Running,
        Ok,
        Failed,
        Stale
    }

    public sealed class JobHealth
    {
        public HealthState State { get; set; }

        /// <summary>Tek satırlık durum (liste ekranı için).</summary>
        public string Summary { get; set; }

        /// <summary>İkinci satır: son hata, son başarılı zaman vb.</summary>
        public string Detail { get; set; }

        public bool IsProblem => State == HealthState.Failed || State == HealthState.Stale;
    }

    /// <summary>
    /// Bir görevin "iyi durumda mı?" sorusuna cevap verir. Sadece son çalışmanın başarılı olup
    /// olmadığına değil, zamanlamaya göre beklenen aralıkta *hiç* başarılı yedek olup olmadığına
    /// bakar — takılı kalan ya da Görev Zamanlayıcı'nın hiç başlatmadığı bir görev, hata üretmediği
    /// halde günlerce sessizce yedek almamış olabilir; bu durum ancak böyle yakalanır.
    /// </summary>
    public static class BackupHealth
    {
        /// <summary>Telafi yedeklemesi başarısız olursa sürekli yeniden denenmesin diye iki deneme arası beklenen süre.</summary>
        public static readonly TimeSpan CatchUpCooldown = TimeSpan.FromHours(4);

        /// <summary>
        /// Kaçırılmış bir çalışma şimdi telafi edilmeli mi? Hem açık uygulama (MainWindow) hem de
        /// sunucu açılışında çalışan arayüzsüz kontrol (App --catch-up) aynı kararı versin diye
        /// tek bir yerde duruyor.
        /// </summary>
        public static bool ShouldCatchUp(BackupJob job, JobHealth health, DateTime utcNow)
        {
            if (!job.Enabled || !job.CatchUpMissedRuns || health.State != HealthState.Stale)
            {
                return false;
            }

            return !job.LastCatchUpUtc.HasValue || utcNow - job.LastCatchUpUtc.Value >= CatchUpCooldown;
        }

        public static JobHealth Evaluate(BackupJob job, DateTime utcNow, bool isRunning = false)
        {
            if (!job.Enabled)
            {
                return new JobHealth { State = HealthState.Disabled, Summary = Loc.T("Health_Disabled"), Detail = Loc.T("Health_DisabledDetail") };
            }

            var lastSuccessText = job.LastSuccessUtc.HasValue
                ? Loc.T("Health_LastSuccess", Loc.FormatDateTime(job.LastSuccessUtc.Value.ToLocalTime()))
                : Loc.T("Health_NoSuccess");

            // Çalışma sürerken "uzun süredir yedek yok" demek yanlış olur — hem ekranda hem de
            // bildirimde yanlış alarma yol açar. Sonuç, çalışma bittiğinde değerlendirilir.
            if (isRunning)
            {
                return new JobHealth { State = HealthState.Running, Summary = Loc.T("Health_Running"), Detail = lastSuccessText };
            }

            var interval = ExpectedInterval(job.Schedule);

            if (interval.HasValue)
            {
                // Tolerans: aralığın 1,5 katı + 1 saat (gece 01:00'deki yedek 12 dk sürebilir, sunucu
                // yeniden başlamış olabilir vb.). Günlükte ~37 saat, saatlikte ~2,5 saat, haftalıkta ~11,5 gün.
                var allowed = TimeSpan.FromTicks((long)(interval.Value.Ticks * 1.5)) + TimeSpan.FromHours(1);
                var reference = job.LastSuccessUtc ?? job.CreatedUtc;
                var age = utcNow - reference;

                if (age > allowed)
                {
                    var summary = job.LastSuccessUtc.HasValue
                        ? Loc.T("Health_StaleSince", Humanize(age))
                        : Loc.T("Health_StaleNever", Humanize(age));
                    var detail = job.LastRunSuccess == false && !string.IsNullOrWhiteSpace(job.LastRunError)
                        ? Loc.T("Health_LastError", job.LastRunError)
                        : job.LastRunUtc.HasValue
                            ? Loc.T("Health_LastRunStale", Loc.FormatDateTime(job.LastRunUtc.Value.ToLocalTime()))
                            : Loc.T("Health_NeverRanTask");
                    return new JobHealth { State = HealthState.Stale, Summary = summary, Detail = detail };
                }
            }

            if (job.LastRunSuccess == false)
            {
                return new JobHealth
                {
                    State = HealthState.Failed,
                    Summary = Loc.T("Health_LastFailed"),
                    Detail = (string.IsNullOrWhiteSpace(job.LastRunError) ? string.Empty : job.LastRunError + " · ") + lastSuccessText
                };
            }

            if (job.LastRunSuccess == true)
            {
                return new JobHealth { State = HealthState.Ok, Summary = lastSuccessText, Detail = ScheduleText(job) };
            }

            if (!interval.HasValue && !job.WatchForChanges)
            {
                return new JobHealth { State = HealthState.ManualOnly, Summary = Loc.T("Health_ManualOnly"), Detail = Loc.T("Health_ManualOnlyDetail") };
            }

            return new JobHealth { State = HealthState.NeverRan, Summary = Loc.T("Health_NeverRan"), Detail = ScheduleText(job) };
        }

        public static TimeSpan? ExpectedInterval(BackupSchedule schedule)
        {
            if (schedule == null || !schedule.Enabled)
            {
                return null;
            }

            switch (schedule.Frequency)
            {
                case ScheduleFrequency.Hourly: return TimeSpan.FromHours(1);
                case ScheduleFrequency.Weekly: return TimeSpan.FromDays(7);
                default: return TimeSpan.FromDays(1);
            }
        }

        public static string ScheduleText(BackupJob job)
        {
            if (job.Schedule == null || !job.Schedule.Enabled)
            {
                return job.WatchForChanges ? Loc.T("Schedule_WatchOnly") : Loc.T("Schedule_Off");
            }

            switch (job.Schedule.Frequency)
            {
                case ScheduleFrequency.Hourly: return Loc.T("Schedule_Hourly");
                case ScheduleFrequency.Weekly: return Loc.T("Schedule_Weekly", Loc.DayName(job.Schedule.DayOfWeek), job.Schedule.TimeOfDay.ToString("hh\\:mm"));
                default: return Loc.T("Schedule_Daily", job.Schedule.TimeOfDay.ToString("hh\\:mm"));
            }
        }

        public static string Humanize(TimeSpan span)
        {
            if (span.TotalDays >= 1)
            {
                var days = (long)Math.Floor(span.TotalDays);
                return Loc.Plural("Time_Day", days, days);
            }

            if (span.TotalHours >= 1)
            {
                var hours = (long)Math.Floor(span.TotalHours);
                return Loc.Plural("Time_Hour", hours, hours);
            }

            var minutes = (long)Math.Max(1, Math.Floor(span.TotalMinutes));
            return Loc.Plural("Time_Minute", minutes, minutes);
        }
    }
}
