using System.Collections.ObjectModel;
using System.Windows;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    /// <summary>
    /// Live "what's happening right now" window for a manual "Şimdi Çalıştır" run — a step log
    /// (zipping, uploading to each destination, verifying) instead of the previous single
    /// end-of-run MessageBox, which showed unreadable raw file ids from the cloud APIs.
    /// </summary>
    public partial class BackupProgressWindow : MetroWindow
    {
        private readonly ObservableCollection<string> _lines = new ObservableCollection<string>();

        public BackupProgressWindow(string jobName)
        {
            InitializeComponent();
            JobNameText.Text = Loc.T("Progress_Running", jobName);
            LogListBox.ItemsSource = _lines;
        }

        /// <summary>Safe to pass directly as the callback for an IProgress&lt;string&gt; created on the UI thread — Progress&lt;T&gt; already marshals back here.</summary>
        public void AppendLine(string text)
        {
            _lines.Add(text);
            if (_lines.Count > 0)
            {
                LogListBox.ScrollIntoView(_lines[_lines.Count - 1]);
            }
        }

        public void ShowFinalResult(BackupRunResult result)
        {
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 100;

            AppendLine(result.FatalError != null
                ? Loc.T("Progress_StartFailed", result.FatalError)
                : result.OverallSuccess
                    ? Loc.T("Progress_AllOk")
                    : Loc.T("Progress_SomeFailed"));

            Title = result.OverallSuccess ? Loc.T("Progress_Done") : Loc.T("Progress_Failed");
            CloseButton.IsEnabled = true;
            CloseButton.Focus();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
