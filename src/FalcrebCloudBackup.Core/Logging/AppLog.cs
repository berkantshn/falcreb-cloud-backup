using System;
using System.IO;
using FalcrebCloudBackup.Core.Storage;
using Serilog;

namespace FalcrebCloudBackup.Core.Logging
{
    /// <summary>
    /// One rolling log file per day under AppPaths.LogsDirectory, written with `shared: true` since
    /// the interactive app and every Task-Scheduler-triggered silent backup are separate OS
    /// processes that can be writing to the same file at once. This is what the user reviews
    /// in-app (LogsWindow) or sends over for diagnosing a problem that happened unattended.
    /// </summary>
    public static class AppLog
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            AppPaths.EnsureCreated();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(AppPaths.LogsDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _initialized = true;
        }

        /// <summary>Süreç kapanmadan önce tamponlanmış son satırların diske ulaşmasını garantiler (sessiz çalışmanın "çıkış kodu" satırı gibi).</summary>
        public static void Shutdown() => Log.CloseAndFlush();

        public static void Info(string message) => Log.Information(message);

        public static void Warn(string message) => Log.Warning(message);

        public static void Error(string message, Exception exception = null)
        {
            if (exception != null)
            {
                Log.Error(exception, message);
            }
            else
            {
                Log.Error(message);
            }
        }
    }
}
