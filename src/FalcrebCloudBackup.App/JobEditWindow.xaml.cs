using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FalcrebCloudBackup.CloudProviders;
using FalcrebCloudBackup.Core.Engine;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace FalcrebCloudBackup.App
{
    public partial class JobEditWindow : MetroWindow
    {
        private readonly BackupJob _job;
        private bool _sourceIsFolder = true;
        private int _databaseFileCount;

        public BackupJob ResultJob { get; private set; }

        public JobEditWindow(BackupJob existingJob)
        {
            InitializeComponent();

            for (var h = 0; h < 24; h++)
            {
                HourCombo.Items.Add(new ComboBoxItem { Content = h.ToString("00") });
            }
            foreach (var m in new[] { 0, 15, 30, 45 })
            {
                MinuteCombo.Items.Add(new ComboBoxItem { Content = m.ToString("00") });
            }

            _job = existingJob ?? new BackupJob();
            PopulateFromJob(_job);
            RefreshDestinationStatuses();
        }

        private void RefreshDestinationStatuses()
        {
            GoogleDriveStatusText.Text = !GoogleDriveProvider.HasClientSecretFile()
                ? Loc.T("JobEdit_DestSetupMissing")
                : GoogleDriveProvider.HasStoredCredentials() ? Loc.T("JobEdit_DestConnected") : Loc.T("JobEdit_DestNotConnected");

            OneDriveStatusText.Text = !OneDriveProvider.HasClientConfigFile()
                ? Loc.T("JobEdit_DestSetupMissing")
                : OneDriveProvider.HasStoredCredentials() ? Loc.T("JobEdit_DestConnected") : Loc.T("JobEdit_DestNotConnected");
        }

        private void UpdateSourceSizePreview()
        {
            if (string.IsNullOrWhiteSpace(SourcePathBox.Text))
            {
                SourceSizeText.Text = string.Empty;
                return;
            }

            try
            {
                if (_sourceIsFolder)
                {
                    var files = Directory.GetFiles(SourcePathBox.Text, "*", SearchOption.AllDirectories);
                    var totalBytes = files.Sum(f => new FileInfo(f).Length);
                    SourceSizeText.Text = Loc.Plural("JobEdit_SourceSize", files.Length, files.Length, NotificationMessages.FormatSize(totalBytes));
                }
                else
                {
                    SourceSizeText.Text = NotificationMessages.FormatSize(new FileInfo(SourcePathBox.Text).Length);
                }
            }
            catch (Exception ex)
            {
                SourceSizeText.Text = Loc.T("JobEdit_SourceSizeError", ex.Message);
            }
        }

        /// <summary>
        /// Kaynakta Firebird veritabanı varsa gbak bölümünü açar; yeni görevde (ya da hiç ayar
        /// yapılmamışsa) gbak bulunduğu sürece seçeneği açık getirir — kullanıcının ham kopya
        /// tuzağına bilmeden düşmemesi için.
        /// </summary>
        private void UpdateFirebirdPanel(bool applyDefaults)
        {
            var databases = FirebirdBackupTool.FindDatabaseFiles(SourcePathBox.Text, _sourceIsFolder);
            _databaseFileCount = databases.Count;

            if (databases.Count == 0)
            {
                FirebirdPanel.Visibility = Visibility.Collapsed;
                WatchHintText.Visibility = Visibility.Collapsed;
                return;
            }

            FirebirdPanel.Visibility = Visibility.Visible;
            WatchHintText.Visibility = Visibility.Visible;
            FirebirdInfoText.Text = databases.Count == 1
                ? Loc.T("Firebird_InfoOne", Path.GetFileName(databases[0]))
                : Loc.T("Firebird_InfoMany", databases.Count);

            if (string.IsNullOrWhiteSpace(GbakPathBox.Text))
            {
                GbakPathBox.Text = FirebirdBackupTool.FindGbak(SourcePathBox.Text) ?? string.Empty;
            }

            if (applyDefaults)
            {
                GbakCheck.IsChecked = _job.Firebird.UseGbak ?? !string.IsNullOrWhiteSpace(GbakPathBox.Text);
            }

            UpdateGbakStatus();
            OnGbakToggled(this, new RoutedEventArgs());
        }

        private void UpdateGbakStatus()
        {
            var path = GbakPathBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                GbakStatusText.Text = Loc.T("Firebird_GbakNotFoundStatus");
                GbakStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else if (File.Exists(path))
            {
                GbakStatusText.Text = Loc.T("Firebird_GbakFound");
                GbakStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0xB3, 0x44));
            }
            else
            {
                GbakStatusText.Text = Loc.T("Firebird_GbakNotHere");
                GbakStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private void PopulateFromJob(BackupJob job)
        {
            NameBox.Text = job.Name;
            SourcePathBox.Text = job.SourcePath;
            _sourceIsFolder = job.SourceIsFolder;

            foreach (var dest in job.Destinations)
            {
                if (dest.Provider == CloudProviderType.GoogleDrive)
                {
                    GoogleDriveCheck.IsChecked = true;
                    GoogleDriveFolderBox.Text = dest.RemoteFolderName;
                }
                else if (dest.Provider == CloudProviderType.OneDrive)
                {
                    OneDriveCheck.IsChecked = true;
                    OneDriveFolderBox.Text = dest.RemoteFolderName;
                }
            }

            SelectComboByTag(FrequencyCombo, job.Schedule.Frequency.ToString());
            SelectComboByTag(DayOfWeekCombo, job.Schedule.DayOfWeek.ToString());
            SelectComboByContent(HourCombo, job.Schedule.TimeOfDay.Hours.ToString("00"));
            SelectComboByContent(MinuteCombo, (job.Schedule.TimeOfDay.Minutes / 15 * 15).ToString("00"));

            EnabledCheck.IsChecked = job.Enabled;
            WatchCheck.IsChecked = job.WatchForChanges;
            CatchUpCheck.IsChecked = job.CatchUpMissedRuns;
            ZipCheck.IsChecked = job.ZipBeforeUpload;
            VerifyCheck.IsChecked = job.VerifyHashAfterUpload;
            TimestampCheck.IsChecked = job.UseTimestampedFolders;

            GbakPathBox.Text = job.Firebird.GbakPath ?? string.Empty;
            FbUserBox.Text = string.IsNullOrWhiteSpace(job.Firebird.User) ? "SYSDBA" : job.Firebird.User;
            FbPasswordBox.Password = job.Firebird.Password;

            OnDestinationToggled(this, new RoutedEventArgs());
            UpdateScheduleFieldVisibility();
            UpdateSourceSizePreview();
            UpdateFirebirdPanel(applyDefaults: true);
        }

        private void OnPickFolderClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (!string.IsNullOrWhiteSpace(SourcePathBox.Text) && Directory.Exists(SourcePathBox.Text))
                {
                    dialog.SelectedPath = SourcePathBox.Text;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SourcePathBox.Text = dialog.SelectedPath;
                    _sourceIsFolder = true;
                    UpdateSourceSizePreview();
                    UpdateFirebirdPanel(applyDefaults: true);
                }
            }
        }

        private void OnPickFileClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Multiselect = false };
            if (dialog.ShowDialog(this) == true)
            {
                SourcePathBox.Text = dialog.FileName;
                _sourceIsFolder = false;
                UpdateSourceSizePreview();
                UpdateFirebirdPanel(applyDefaults: true);
            }
        }

        private void OnGbakToggled(object sender, RoutedEventArgs e)
        {
            GbakDetailsPanel.Visibility = GbakCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnGbakPathChanged(object sender, TextChangedEventArgs e) => UpdateGbakStatus();

        private void OnFindGbakClick(object sender, RoutedEventArgs e)
        {
            var found = FirebirdBackupTool.FindGbak(SourcePathBox.Text);
            if (found == null)
            {
                MessageBox.Show(this,
                    Loc.T("Firebird_GbakNotFoundMessage"),
                    Loc.T("Firebird_GbakNotFoundTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            GbakPathBox.Text = found;
        }

        private void OnBrowseGbakClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "gbak.exe|gbak.exe|" + Loc.T("Common_AllFiles") + "|*.*",
                FileName = "gbak.exe",
                Multiselect = false
            };
            if (dialog.ShowDialog(this) == true)
            {
                GbakPathBox.Text = dialog.FileName;
            }
        }

        private void OnDestinationToggled(object sender, RoutedEventArgs e)
        {
            GoogleDriveFolderBox.IsEnabled = GoogleDriveCheck.IsChecked == true;
            OneDriveFolderBox.IsEnabled = OneDriveCheck.IsChecked == true;
        }

        private void OnFrequencyChanged(object sender, SelectionChangedEventArgs e) => UpdateScheduleFieldVisibility();

        private void UpdateScheduleFieldVisibility()
        {
            var frequency = SelectedTag(FrequencyCombo);
            DayOfWeekPanel.Visibility = frequency == "Weekly" ? Visibility.Visible : Visibility.Collapsed;
            TimeOfDayPanel.Visibility = frequency == "Hourly" ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Text = Loc.T("JobEdit_ErrNameEmpty");
                return;
            }

            if (string.IsNullOrWhiteSpace(SourcePathBox.Text))
            {
                ErrorText.Text = Loc.T("JobEdit_ErrNoSource");
                return;
            }

            if (_sourceIsFolder ? !Directory.Exists(SourcePathBox.Text) : !File.Exists(SourcePathBox.Text))
            {
                ErrorText.Text = Loc.T("JobEdit_ErrSourceMissing", SourcePathBox.Text);
                return;
            }

            var destinations = new List<BackupDestination>();
            if (GoogleDriveCheck.IsChecked == true)
            {
                destinations.Add(new BackupDestination
                {
                    Provider = CloudProviderType.GoogleDrive,
                    RemoteFolderName = string.IsNullOrWhiteSpace(GoogleDriveFolderBox.Text) ? NameBox.Text.Trim() : GoogleDriveFolderBox.Text.Trim()
                });
            }
            if (OneDriveCheck.IsChecked == true)
            {
                destinations.Add(new BackupDestination
                {
                    Provider = CloudProviderType.OneDrive,
                    RemoteFolderName = string.IsNullOrWhiteSpace(OneDriveFolderBox.Text) ? NameBox.Text.Trim() : OneDriveFolderBox.Text.Trim()
                });
            }

            if (destinations.Count == 0)
            {
                ErrorText.Text = Loc.T("JobEdit_ErrNoDestination");
                return;
            }

            var frequencyTag = SelectedTag(FrequencyCombo);
            if (frequencyTag == null)
            {
                ErrorText.Text = Loc.T("JobEdit_ErrNoFrequency");
                return;
            }

            if (_databaseFileCount > 0 && GbakCheck.IsChecked == true)
            {
                var gbakPath = GbakPathBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(gbakPath) || !File.Exists(gbakPath))
                {
                    ErrorText.Text = Loc.T("JobEdit_ErrGbak");
                    return;
                }
            }

            _job.Name = NameBox.Text.Trim();
            _job.SourcePath = SourcePathBox.Text;
            _job.SourceIsFolder = _sourceIsFolder;
            _job.Destinations = destinations;

            _job.Schedule.Frequency = (ScheduleFrequency)Enum.Parse(typeof(ScheduleFrequency), frequencyTag);
            _job.Schedule.Enabled = true;
            _job.Schedule.DayOfWeek = Enum.TryParse<DayOfWeek>(SelectedTag(DayOfWeekCombo), out var day) ? day : DayOfWeek.Monday;

            var hour = int.TryParse(SelectedContent(HourCombo), out var h) ? h : 1;
            var minute = int.TryParse(SelectedContent(MinuteCombo), out var m) ? m : 0;
            _job.Schedule.TimeOfDay = new TimeSpan(hour, minute, 0);

            _job.Enabled = EnabledCheck.IsChecked == true;
            _job.WatchForChanges = WatchCheck.IsChecked == true;
            _job.CatchUpMissedRuns = CatchUpCheck.IsChecked == true;
            _job.ZipBeforeUpload = ZipCheck.IsChecked == true;
            _job.VerifyHashAfterUpload = VerifyCheck.IsChecked == true;
            _job.UseTimestampedFolders = TimestampCheck.IsChecked == true;

            // Kaynakta veritabanı yoksa tercih "otomatik" (null) kalır; ileride klasöre .fdb düşerse
            // gbak bulunduğu sürece kendiliğinden devreye girer.
            _job.Firebird.UseGbak = _databaseFileCount > 0 ? GbakCheck.IsChecked == true : (bool?)null;
            _job.Firebird.GbakPath = string.IsNullOrWhiteSpace(GbakPathBox.Text) ? null : GbakPathBox.Text.Trim();
            _job.Firebird.User = string.IsNullOrWhiteSpace(FbUserBox.Text) ? "SYSDBA" : FbUserBox.Text.Trim();
            _job.Firebird.Password = FbPasswordBox.Password;

            ResultJob = _job;
            DialogResult = true;
            Close();
        }

        private static string SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        private static string SelectedContent(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Content as string;

        private static void SelectComboByTag(ComboBox comboBox, string tag)
        {
            var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag);
            comboBox.SelectedItem = item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        }

        private static void SelectComboByContent(ComboBox comboBox, string content)
        {
            var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Content == content);
            comboBox.SelectedItem = item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        }
    }
}
