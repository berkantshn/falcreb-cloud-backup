using System;

namespace FalcrebCloudBackup.Core.Models
{
    public enum ScheduleFrequency
    {
        Hourly,
        Daily,
        Weekly
    }

    /// <summary>
    /// The "check on a timer" half of triggering. This is combined with (not a replacement for)
    /// <see cref="BackupJob.WatchForChanges"/>, which reacts the moment the accounting software
    /// writes a new backup file instead of waiting for the next scheduled tick.
    /// </summary>
    public class BackupSchedule
    {
        public bool Enabled { get; set; }
        public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

        /// <summary>Used only when Frequency == Weekly.</summary>
        public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

        /// <summary>Time of day the task fires (Daily/Weekly). Ignored for Hourly.</summary>
        public TimeSpan TimeOfDay { get; set; } = new TimeSpan(1, 0, 0);
    }
}
