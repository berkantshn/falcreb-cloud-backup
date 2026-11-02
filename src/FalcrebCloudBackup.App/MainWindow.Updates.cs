using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FalcrebCloudBackup.App.Services;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Updates;

namespace FalcrebCloudBackup.App
{
    /// <summary>
    /// Güncelleme denetimi ve kurulumu. Denetim yalnızca bildirir; indirip kurmak için kullanıcının
    /// tıklaması ve kimlik doğrulaması gerekir. Sessiz yedekleme çalışmaları güncellemeye bakmaz.
    /// </summary>
    public partial class MainWindow
    {
        private static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);

        private readonly UpdateClient _updateClient = new UpdateClient();
        private DispatcherTimer _updateTimer;
        private UpdateManifest _availableUpdate;
        private bool _updateInProgress;

        private void InitializeUpdates()
        {
            UpdateCheckCheckBox.IsChecked = UiSettingsStore.Load().CheckForUpdates;
            _updateTimer = new DispatcherTimer { Interval = FirstUpdateCheckDelay };
            _updateTimer.Tick += async (s, e) =>
            {
                _updateTimer.Interval = UpdateCheckInterval;
                await CheckForUpdateAsync();
            };
            _updateTimer.Start();
        }

        private async System.Threading.Tasks.Task CheckForUpdateAsync()
        {
            if (UpdateCheckCheckBox.IsChecked != true || _updateInProgress)
            {
                return;
            }

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    var current = Assembly.GetExecutingAssembly().GetName().Version;
                    _availableUpdate = await _updateClient.CheckAsync(current, cts.Token);
                }

                ShowUpdateNotice();
            }
            catch (Exception ex)
            {
                // Denetim başarısızsa yedeklemeye etkisi yok; kullanıcı rahatsız edilmez, log yeter.
                AppLog.Warn(Loc.T("Log_UpdateCheckFailed", ex.Message));
            }
        }

        private void ShowUpdateNotice()
        {
            if (_availableUpdate == null)
            {
                UpdateNotice.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateNoticeText.Text = Loc.T("Update_Available", _availableUpdate.Version);
            UpdateNotice.Visibility = Visibility.Visible;
        }

        private void OnUpdateCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            try
            {
                var settings = UiSettingsStore.Load();
                settings.CheckForUpdates = UpdateCheckCheckBox.IsChecked == true;
                UiSettingsStore.Save(settings);
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_UpdateSettingSaveFailed", ex.Message));
            }

            if (UpdateCheckCheckBox.IsChecked != true)
            {
                _availableUpdate = null;
                ShowUpdateNotice();
            }
        }

        /// <summary>Bu kopya dışında çalışan süreç: Görev Zamanlayıcı'nın başlattığı sessiz yedeklemedir.</summary>
        private static bool IsSilentBackupRunning()
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName);
            try
            {
                return others.Any(p => p.Id != current.Id);
            }
            finally
            {
                foreach (var p in others)
                {
                    p.Dispose();
                }
            }
        }

        private async void OnInstallUpdateClick(object sender, RoutedEventArgs e)
        {
            var update = _availableUpdate;
            if (update == null || _updateInProgress || !ConfirmIdentity())
            {
                return;
            }

            if (_coordinator.IsBusy || IsSilentBackupRunning())
            {
                MessageBox.Show(this, Loc.T("Update_BackupRunning"), Loc.T("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(this, Loc.T("Update_Confirm", update.Version), "FalcrebCloudBackup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _updateInProgress = true;
            UpdateInstallLink.IsEnabled = false;
            UpdateNoticeText.Text = Loc.T("Update_Downloading", update.Version);
            VerifiedInstaller installer = null;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                {
                    installer = await _updateClient.DownloadAsync(update, cts.Token);
                }

                // İndirme uzun sürmüş olabilir: bu arada bir yedekleme başladıysa kesilmesin.
                if (_coordinator.IsBusy || IsSilentBackupRunning())
                {
                    MessageBox.Show(this, Loc.T("Update_BackupRunning"), Loc.T("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                AppLog.Info(Loc.T("Log_UpdateInstalling", update.Version));
                installer.Start();
                _isExiting = true;
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_UpdateFailed"), ex);
                MessageBox.Show(this, Loc.T("Update_Failed", ex.Message), Loc.T("Common_Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                installer?.Dispose();
                _updateInProgress = false;
                UpdateInstallLink.IsEnabled = true;
                ShowUpdateNotice();
            }
        }
    }
}
