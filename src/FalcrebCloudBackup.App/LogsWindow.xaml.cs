using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Storage;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    /// <summary>Shows the tail of today's (or the most recent) log file — the same file Serilog writes for both the interactive app and every silent Task-Scheduler-triggered run.</summary>
    public partial class LogsWindow : MetroWindow
    {
        private const int MaxLines = 1000;

        public LogsWindow()
        {
            InitializeComponent();
            LoadLatestLog();
        }

        private void LoadLatestLog()
        {
            var latestFile = Directory.Exists(AppPaths.LogsDirectory)
                ? Directory.GetFiles(AppPaths.LogsDirectory, "app-*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;

            if (latestFile == null)
            {
                SubtitleText.Text = Loc.T("Logs_Empty");
                LogText.Text = string.Empty;
                return;
            }

            SubtitleText.Text = latestFile;

            string[] lines;
            using (var stream = new FileStream(latestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            }

            var tail = lines.Length > MaxLines ? lines.Skip(lines.Length - MaxLines) : lines;
            LogText.Text = string.Join(Environment.NewLine, tail);

            LogScrollViewer.ScrollToBottom();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e) => LoadLatestLog();

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            AppPaths.EnsureCreated();
            Process.Start("explorer.exe", AppPaths.LogsDirectory);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
