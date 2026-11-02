using System.Linq;
using System.Windows;
using System.Windows.Media;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    public class HistoryItemViewModel
    {
        public string JobName { get; set; }
        public string WhenText { get; set; }
        public string MetaText { get; set; }
        public string DetailText { get; set; }
        public string StatusGlyph { get; set; }
        public Brush StatusColor { get; set; }
    }

    public partial class HistoryWindow : MetroWindow
    {
        public HistoryWindow()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            var results = new HistoryStore().LoadAll();
            results.Reverse();

            var failed = results.Count(r => !r.OverallSuccess);
            SubtitleText.Text = results.Count == 0
                ? Loc.T("History_Empty")
                : Loc.Plural("History_Summary", results.Count, results.Count, results.Count - failed, failed);

            HistoryListBox.ItemsSource = results.Select(ToViewModel).ToList();
        }

        private static HistoryItemViewModel ToViewModel(BackupRunResult result)
        {
            var detail = result.FatalError != null
                ? Loc.T("History_Error", result.FatalError)
                : NotificationMessages.DestinationsLine(result);

            var meta = Loc.T("History_Duration", NotificationMessages.FormatDuration(result.FinishedUtc - result.StartedUtc));
            if (result.TotalBytes > 0)
            {
                meta += Loc.T("History_Size", NotificationMessages.FormatSize(result.TotalBytes));
            }

            return new HistoryItemViewModel
            {
                JobName = result.JobName,
                WhenText = " · " + Loc.FormatDateTime(result.StartedUtc.ToLocalTime()),
                MetaText = meta,
                DetailText = detail,
                StatusGlyph = result.OverallSuccess ? "✓" : "✕",
                StatusColor = new SolidColorBrush(result.OverallSuccess
                    ? Color.FromRgb(0x2F, 0xB3, 0x44)
                    : Color.FromRgb(0xE0, 0x45, 0x45))
            };
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e) => Load();

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
