using System;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Scheduling
{
    /// <summary>
    /// Creates/updates the one Windows Task Scheduler entry per job that fires the silent backup
    /// run (`FalcrebCloudBackup.App.exe --silent-backup &lt;jobId&gt;`) even when nobody is logged
    /// in — this is what makes scheduling work unattended on the server. Removing a job's task
    /// here only deletes the *scheduler registration*, never any backed-up data.
    /// </summary>
    public class TaskSchedulerService
    {
        private const string TaskFolder = "FalcrebCloudBackup";
        private const string AutoStartTaskName = "AutoStart";
        private const string CatchUpTaskName = "CatchUp";

        /// <summary>
        /// Sunucu açıldıktan sonra telafi kontrolünün ne kadar bekleyeceği. Açılışın hemen
        /// ardından Firebird servisi, ağ ve disk henüz hazır olmayabilir.
        /// </summary>
        public static readonly TimeSpan CatchUpBootDelay = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Bir yedekleme çalışmasının en fazla sürebileceği süre. Süresiz bırakıldığında takılan
        /// bir çalışma sonsuza kadar "çalışıyor" görünüp sonraki günlerin tetiklemelerini
        /// engelliyordu. BackupEngine bundan
        /// önce (bkz. App.RunSilentBackup) kendi zaman aşımıyla düzgün kapanır; bu, en son emniyet.
        /// </summary>
        public static readonly TimeSpan JobExecutionTimeLimit = TimeSpan.FromHours(4);

        private readonly string _exePath;

        public TaskSchedulerService(string exePath)
        {
            _exePath = exePath;
        }

        public void RegisterOrUpdateJobTask(BackupJob job)
        {
            try
            {
                using (var ts = new TaskService())
                {
                    var folder = ts.RootFolder.SubFolders.Exists(TaskFolder)
                        ? ts.RootFolder.SubFolders[TaskFolder]
                        : ts.RootFolder.CreateFolder(TaskFolder);

                    var taskName = TaskNameFor(job.Id);

                    if (!job.Schedule.Enabled)
                    {
                        RemoveIfExists(folder, taskName);
                        return;
                    }

                    // Yeniden kaydetmek, Windows'un o göreve ait "kaçırılan çalışma" bilgisini
                    // sıfırlar — StartWhenAvailable'ın telafi çalışmasını başlatma şansı kalmaz.
                    // Uygulama her açılışta görevleri eşitlediği için (MainWindow → SyncAll),
                    // sunucu elektrik kesintisinden sonra açıldığında telafi tam burada ölüyordu.
                    // Bu yüzden yalnızca gerçekten değişen bir şey varsa yeniden kaydediliyor.
                    var signature = BuildSignature(job);
                    if (IsAlreadyRegisteredWith(folder, taskName, signature))
                    {
                        return;
                    }

                    var td = ts.NewTask();
                    td.RegistrationInfo.Source = signature;
                    td.RegistrationInfo.Description = Loc.T("Task_JobDescription", job.Name);
                    td.Actions.Add(new ExecAction(_exePath, $"--silent-backup {job.Id}", null));

                    td.Triggers.Clear();
                    td.Triggers.Add(BuildTrigger(job.Schedule));

                    // S4U: runs whether a user is logged on or not, without needing to store the
                    // Windows account password on disk.
                    td.Principal.LogonType = TaskLogonType.S4U;
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    td.Principal.UserId = Environment.UserName;

                    td.Settings.StartWhenAvailable = true;
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.ExecutionTimeLimit = JobExecutionTimeLimit;

                    // Önceki çalışma hâlâ sürüyorsa yenisi başlatılmaz (üst üste binmesin);
                    // süre sınırı sayesinde bu durum en fazla 4 saat sürebilir.
                    td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;

                    // Uygulama başarısız bir yedeklemede sıfırdan farklı çıkış koduyla kapanır
                    // (bkz. App.RunSilentBackup); Görev Zamanlayıcı o zaman 1 saat arayla
                    // 2 kez daha dener — gece internetin kısa süre kesilmesi gibi geçici sorunlar
                    // için. Kalıcı bir sorunsa her deneme ayrıca bildirim üretir.
                    td.Settings.RestartCount = 2;
                    td.Settings.RestartInterval = TimeSpan.FromHours(1);

                    // Varsayılan "BelowNormal" öncelik, gbak + sıkıştırmayı gereksiz yavaşlatıyor.
                    td.Settings.Priority = ProcessPriorityClass.Normal;

                    folder.RegisterTaskDefinition(taskName, td);
                    AppLog.Info(Loc.T("Log_TaskRegistered", job.Name, job.Schedule.Frequency));
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_TaskRegisterFailed", job.Name), ex);
                throw;
            }
        }

        public void RemoveJobTask(Guid jobId)
        {
            using (var ts = new TaskService())
            {
                if (!ts.RootFolder.SubFolders.Exists(TaskFolder))
                {
                    return;
                }

                RemoveIfExists(ts.RootFolder.SubFolders[TaskFolder], TaskNameFor(jobId));
            }
        }

        /// <summary>Bir görevin zamanlanmış kaydının Windows'ta gerçekten var olup olmadığı (arayüzdeki durum göstergesi için).</summary>
        public bool IsJobTaskRegistered(Guid jobId)
        {
            try
            {
                using (var ts = new TaskService())
                {
                    return ts.RootFolder.SubFolders.Exists(TaskFolder)
                           && ts.RootFolder.SubFolders[TaskFolder].Tasks.Exists(TaskNameFor(jobId));
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sunucu her açıldığında, arayüz açılmadan "kaçırılmış yedekleme var mı?" diye bakan
        /// görev. Uygulamanın içindeki telafi yalnızca biri Windows'a giriş yaptığında devreye
        /// girer; elektrik kesintisinden sonra sunucu açılıp kimse giriş yapmazsa (hafta sonu,
        /// tatil) o yol çalışmaz. Bu görev S4U ile, kimse giriş yapmadan da çalışır.
        ///
        /// Kaçırılmış bir şey yoksa hiçbir şey yapmadan kapanır.
        /// </summary>
        public void RegisterOrUpdateCatchUpTask()
        {
            try
            {
                using (var ts = new TaskService())
                {
                    var folder = ts.RootFolder.SubFolders.Exists(TaskFolder)
                        ? ts.RootFolder.SubFolders[TaskFolder]
                        : ts.RootFolder.CreateFolder(TaskFolder);

                    var signature = string.Join("|", "fcb-catchup-v1", _exePath, CatchUpBootDelay.ToString());
                    if (IsAlreadyRegisteredWith(folder, CatchUpTaskName, signature))
                    {
                        return;
                    }

                    var td = ts.NewTask();
                    td.RegistrationInfo.Description = Loc.T("Task_CatchUpDescription");
                    td.RegistrationInfo.Source = signature;
                    td.Actions.Add(new ExecAction(_exePath, "--catch-up", null));

                    td.Triggers.Clear();
                    td.Triggers.Add(new BootTrigger { Delay = CatchUpBootDelay });

                    td.Principal.LogonType = TaskLogonType.S4U;
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    td.Principal.UserId = Environment.UserName;

                    td.Settings.StartWhenAvailable = true;
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.ExecutionTimeLimit = JobExecutionTimeLimit;
                    td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                    td.Settings.Priority = ProcessPriorityClass.Normal;

                    folder.RegisterTaskDefinition(CatchUpTaskName, td);
                    AppLog.Info(Loc.T("Log_CatchUpTaskRegistered"));
                }
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_CatchUpTaskRegisterFailed"), ex);
            }
        }

        public void RemoveCatchUpTask()
        {
            try
            {
                using (var ts = new TaskService())
                {
                    if (ts.RootFolder.SubFolders.Exists(TaskFolder))
                    {
                        RemoveIfExists(ts.RootFolder.SubFolders[TaskFolder], CatchUpTaskName);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_CatchUpTaskRemoveFailed", ex.Message));
            }
        }

        /// <summary>
        /// So the tray app (and its watchers/scheduling) come back up on their own after a server
        /// reboot, without anyone having to log in and double-click the exe.
        /// </summary>
        public void EnableAutoStart()
        {
            using (var ts = new TaskService())
            {
                var folder = ts.RootFolder.SubFolders.Exists(TaskFolder)
                    ? ts.RootFolder.SubFolders[TaskFolder]
                    : ts.RootFolder.CreateFolder(TaskFolder);

                var td = ts.NewTask();
                td.RegistrationInfo.Description = Loc.T("Task_AutoStartDescription");
                td.Actions.Add(new ExecAction(_exePath, null, null));

                td.Triggers.Clear();
                td.Triggers.Add(new LogonTrigger { UserId = Environment.UserName });

                // InteractiveToken, not S4U: this task has to show a window and a tray icon on
                // the user's desktop. S4U (used for the silent per-job tasks above, correctly)
                // creates a non-interactive logon — the app still runs, but with no visible
                // window or tray icon anywhere, which is exactly wrong for this one.
                td.Principal.LogonType = TaskLogonType.InteractiveToken;
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Principal.UserId = Environment.UserName;

                td.Settings.StartWhenAvailable = true;
                // Tepsi uygulaması süresiz çalışır; burada süre sınırı olmaması doğru.
                td.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                folder.RegisterTaskDefinition(AutoStartTaskName, td);
            }
        }

        public void DisableAutoStart()
        {
            using (var ts = new TaskService())
            {
                if (!ts.RootFolder.SubFolders.Exists(TaskFolder))
                {
                    return;
                }

                RemoveIfExists(ts.RootFolder.SubFolders[TaskFolder], AutoStartTaskName);
            }
        }

        public bool IsAutoStartEnabled()
        {
            using (var ts = new TaskService())
            {
                return ts.RootFolder.SubFolders.Exists(TaskFolder)
                       && ts.RootFolder.SubFolders[TaskFolder].Tasks.Exists(AutoStartTaskName);
            }
        }

        /// <summary>
        /// Görevin Windows'taki kaydı, elimizdeki tanımla birebir aynı mı? Karşılaştırma için
        /// görev kaydının "Source" alanına yazdığımız imza kullanılır. Kayıt yoksa, devre dışı
        /// bırakılmışsa ya da imza tutmuyorsa yeniden kaydedilmesi gerekir.
        /// </summary>
        private static bool IsAlreadyRegisteredWith(TaskFolder folder, string taskName, string signature)
        {
            if (!folder.Tasks.Exists(taskName))
            {
                return false;
            }

            try
            {
                using (var existing = folder.Tasks[taskName])
                {
                    return existing.Enabled
                           && string.Equals(existing.Definition.RegistrationInfo.Source, signature, StringComparison.Ordinal);
                }
            }
            catch (Exception)
            {
                // Kayıt okunamıyorsa (bozuk/erişilemez) yeniden kaydetmek doğru davranış.
                return false;
            }
        }

        /// <summary>Görevin Windows kaydını belirleyen her şey — biri değişirse yeniden kaydedilir.</summary>
        private string BuildSignature(BackupJob job)
        {
            var s = job.Schedule;
            return string.Join("|",
                "fcb-v2",
                _exePath,
                job.Id.ToString("N"),
                s.Frequency.ToString(),
                s.TimeOfDay.ToString(),
                s.DayOfWeek.ToString(),
                JobExecutionTimeLimit.ToString());
        }

        private static void RemoveIfExists(TaskFolder folder, string taskName)
        {
            if (folder.Tasks.Exists(taskName))
            {
                folder.DeleteTask(taskName);
            }
        }

        private static Trigger BuildTrigger(BackupSchedule schedule)
        {
            switch (schedule.Frequency)
            {
                case ScheduleFrequency.Hourly:
                    return new DailyTrigger
                    {
                        StartBoundary = DateTime.Today,
                        Repetition = { Interval = TimeSpan.FromHours(1), Duration = TimeSpan.FromDays(1) }
                    };

                case ScheduleFrequency.Weekly:
                    return new WeeklyTrigger
                    {
                        DaysOfWeek = ToDayOfWeekFlags(schedule.DayOfWeek),
                        StartBoundary = DateTime.Today.Add(schedule.TimeOfDay)
                    };

                case ScheduleFrequency.Daily:
                default:
                    return new DailyTrigger
                    {
                        StartBoundary = DateTime.Today.Add(schedule.TimeOfDay)
                    };
            }
        }

        private static DaysOfTheWeek ToDayOfWeekFlags(DayOfWeek day)
        {
            return (DaysOfTheWeek)(1 << (int)day);
        }

        private static string TaskNameFor(Guid jobId) => jobId.ToString("N");
    }
}
