using System;
using System.IO;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>
    /// Everything lives under ProgramData rather than the user's AppData, because the silent
    /// backup run (launched by Task Scheduler) and the interactive UI must see the same job
    /// config regardless of which Windows session triggered them.
    /// </summary>
    public static class AppPaths
    {
        public static string RootDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FalcrebCloudBackup");

        public static string JobsFilePath => Path.Combine(RootDirectory, "jobs.json");

        public static string HistoryDirectory => Path.Combine(RootDirectory, "history");

        public static string AuthFilePath => Path.Combine(RootDirectory, "auth.dat");

        public static string CloudTokensDirectory => Path.Combine(RootDirectory, "tokens");

        public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

        public static void EnsureCreated()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(HistoryDirectory);
            Directory.CreateDirectory(CloudTokensDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }
    }
}
