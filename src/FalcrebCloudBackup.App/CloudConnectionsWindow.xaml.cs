using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FalcrebCloudBackup.CloudProviders;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    public partial class CloudConnectionsWindow : MetroWindow
    {
        /// <summary>
        /// Etkileşimli girişin en fazla bekleyeceği süre. Sonsuz beklemek, tarayıcı açılıp anında
        /// kapandığında pencerenin kalıcı olarak kilitlenmesine yol açıyordu (sahada yaşandı);
        /// 2FA/hesap seçimi için rahat yeten bir süre bırakılıp sonra vazgeçiliyor.
        /// </summary>
        private static readonly TimeSpan InteractiveLoginTimeout = TimeSpan.FromMinutes(5);

        private readonly JobStore _jobStore = new JobStore();
        private CancellationTokenSource _connectCts;

        public CloudConnectionsWindow()
        {
            InitializeComponent();
            RefreshGoogleDriveStatus();
            RefreshOneDriveStatus();
            RefreshQuotaDisplay();
        }

        /// <summary>Son yüklemede kaydedilen doluluk bilgisini gösterir — bu metot ağa çıkmaz.</summary>
        private void RefreshQuotaDisplay()
        {
            ApplyQuota(CloudQuotaStore.Get(CloudProviderType.GoogleDrive), GoogleDriveQuotaBar, GoogleDriveQuotaText);
            ApplyQuota(CloudQuotaStore.Get(CloudProviderType.OneDrive), OneDriveQuotaBar, OneDriveQuotaText);
        }

        private static void ApplyQuota(CloudStorageQuota quota, ProgressBar bar, TextBlock text)
        {
            if (quota == null || !quota.TotalBytes.HasValue || quota.TotalBytes.Value <= 0)
            {
                bar.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Collapsed;
                return;
            }

            var accent = quota.IsCritical
                ? Color.FromRgb(0xE0, 0x45, 0x45)
                : quota.IsLow
                    ? Color.FromRgb(0xE0, 0x8A, 0x1E)
                    : Color.FromRgb(0x2E, 0x8B, 0xE0);

            bar.Value = (quota.UsedFraction ?? 0) * 100;
            bar.Foreground = new SolidColorBrush(accent);
            bar.Visibility = Visibility.Visible;

            text.Text = NotificationMessages.QuotaSummary(quota) +
                        Loc.T("Cloud_QuotaAsOf", Loc.FormatDateTime(quota.FetchedUtc.ToLocalTime()));
            if (quota.IsLow)
            {
                text.Foreground = new SolidColorBrush(accent);
            }
            else
            {
                text.ClearValue(TextBlock.ForegroundProperty);
            }
            text.Visibility = Visibility.Visible;
        }

        /// <summary>Bağlı hesaplar için doluluk bilgisini sağlayıcıdan yeniden okur (tarayıcı açmaz).</summary>
        private async void OnRefreshQuotaClick(object sender, RoutedEventArgs e)
        {
            RefreshQuotaButton.IsEnabled = false;
            InfoText.Text = Loc.T("Cloud_QuotaFetching");
            var errors = new List<string>();

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                {
                    if (GoogleDriveProvider.HasClientSecretFile() && GoogleDriveProvider.HasStoredCredentials())
                    {
                        await FetchQuotaAsync(new GoogleDriveProvider(allowInteractiveLogin: false), "Google Drive", errors, cts.Token).ConfigureAwait(true);
                    }

                    if (OneDriveProvider.HasClientConfigFile() && OneDriveProvider.HasStoredCredentials())
                    {
                        await FetchQuotaAsync(new OneDriveProvider(allowInteractiveLogin: false), "OneDrive", errors, cts.Token).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                RefreshQuotaDisplay();
                RefreshQuotaButton.IsEnabled = true;
                InfoText.Text = errors.Count == 0
                    ? Loc.T("Cloud_QuotaUpdated")
                    : string.Join("\n", errors);
            }
        }

        private static async Task FetchQuotaAsync(ICloudProvider provider, string displayName, List<string> errors, CancellationToken token)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await provider.ConnectAsync(token).ConfigureAwait(false);
                    CloudQuotaStore.Save(await provider.GetStorageQuotaAsync(token).ConfigureAwait(false));
                }, token).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppLog.Warn(Loc.T("Log_ProviderQuotaFailed", displayName, ex.Message));
                errors.Add($"{displayName}: {ex.Message}");
            }
        }

        private void RefreshGoogleDriveStatus()
        {
            if (!GoogleDriveProvider.HasClientSecretFile())
            {
                GoogleDriveStatusText.Text = Loc.T("Cloud_SetupMissing");
                InfoText.Text = Loc.T("Cloud_GoogleSetupInfo", GoogleDriveProvider.ClientSecretFilePath);
                SetGoogleDriveConnected(false);
                return;
            }

            var connected = GoogleDriveProvider.HasStoredCredentials();
            GoogleDriveStatusText.Text = connected ? Loc.T("Cloud_Connected") : Loc.T("Cloud_NotConnected");
            SetGoogleDriveConnected(connected);
        }

        private void SetGoogleDriveConnected(bool connected)
        {
            GoogleDriveCheckmark.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            GoogleDriveDisconnectButton.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            GoogleDriveConnectButton.Content = connected ? Loc.T("Cloud_Reconnect") : Loc.T("Cloud_Connect");
        }

        private void RefreshOneDriveStatus()
        {
            if (!OneDriveProvider.HasClientConfigFile())
            {
                OneDriveStatusText.Text = Loc.T("Cloud_SetupMissing");
                SetOneDriveConnected(false);
                return;
            }

            var connected = OneDriveProvider.HasStoredCredentials();
            OneDriveStatusText.Text = connected ? Loc.T("Cloud_Connected") : Loc.T("Cloud_NotConnected");
            SetOneDriveConnected(connected);
        }

        private void SetOneDriveConnected(bool connected)
        {
            OneDriveCheckmark.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            OneDriveDisconnectButton.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            OneDriveConnectButton.Content = connected ? Loc.T("Cloud_Reconnect") : Loc.T("Cloud_Connect");
        }

        private async void OnConnectGoogleDriveClick(object sender, RoutedEventArgs e) => await ConnectAsync(google: true);

        private async void OnConnectOneDriveClick(object sender, RoutedEventArgs e) => await ConnectAsync(google: false);

        /// <summary>
        /// İki sağlayıcı için ortak giriş akışı: düğmeleri kilitler, giriş adresini panelde gösterir,
        /// süre sınırı ve "Vazgeç" ile her durumda sonlanmasını garantiler.
        /// </summary>
        private async Task ConnectAsync(bool google)
        {
            var displayName = google ? "Google Drive" : "OneDrive";
            var statusText = google ? GoogleDriveStatusText : OneDriveStatusText;

            SetButtonsBusy(true);
            InfoText.Text = string.Empty;
            statusText.Text = Loc.T("Cloud_WaitingForBrowser", displayName);
            ShowAuthPanel(null);

            var cts = new CancellationTokenSource(InteractiveLoginTimeout);
            _connectCts = cts;
            try
            {
                Action<string> report = url => Dispatcher.BeginInvoke(new Action(() => ShowAuthPanel(url)));
                var token = cts.Token;

                ICloudProvider provider;
                if (google)
                {
                    provider = new GoogleDriveProvider(allowInteractiveLogin: true) { AuthUrlReported = report };
                }
                else
                {
                    provider = new OneDriveProvider(allowInteractiveLogin: true) { AuthUrlReported = report };
                }

                await Task.Run(() => provider.ConnectAsync(token), token).ConfigureAwait(true);

                // Bağlantı kurulur kurulmaz doluluğu da oku ki kullanıcı alanını hemen görsün.
                var quotaErrors = new List<string>();
                await FetchQuotaAsync(provider, displayName, quotaErrors, token).ConfigureAwait(true);
                RefreshQuotaDisplay();

                statusText.Text = Loc.T("Cloud_Connected");
                InfoText.Text = Loc.T("Cloud_ConnectedInfo", displayName);
                AppLog.Info(Loc.T("Cloud_ConnectedInfo", displayName));
                if (google) { SetGoogleDriveConnected(true); } else { SetOneDriveConnected(true); }
            }
            catch (Exception ex)
            {
                var cancelled = cts.IsCancellationRequested || ex is OperationCanceledException;
                if (cancelled)
                {
                    statusText.Text = Loc.T("Cloud_NotCompleted");
                    InfoText.Text = Loc.T("Cloud_Aborted");
                    AppLog.Warn(Loc.T("Log_ConnectAborted", displayName));
                }
                else
                {
                    statusText.Text = Loc.T("Cloud_Failed");
                    InfoText.Text = ex.Message;
                    AppLog.Error(Loc.T("Log_ConnectFailed", displayName), ex);
                }

                if (google)
                {
                    SetGoogleDriveConnected(GoogleDriveProvider.HasStoredCredentials());
                }
                else
                {
                    SetOneDriveConnected(OneDriveProvider.HasStoredCredentials());
                }
            }
            finally
            {
                _connectCts = null;
                cts.Dispose();
                HideAuthPanel();
                SetButtonsBusy(false);
            }
        }

        private void SetButtonsBusy(bool busy)
        {
            GoogleDriveConnectButton.IsEnabled = !busy;
            OneDriveConnectButton.IsEnabled = !busy;
            GoogleDriveDisconnectButton.IsEnabled = !busy;
            OneDriveDisconnectButton.IsEnabled = !busy;
        }

        private void ShowAuthPanel(string authUrl)
        {
            AuthUrlPanel.Visibility = Visibility.Visible;
            AuthUrlBox.Text = authUrl ?? string.Empty;
            CopyAuthUrlButton.IsEnabled = !string.IsNullOrEmpty(authUrl);
        }

        private void HideAuthPanel()
        {
            AuthUrlPanel.Visibility = Visibility.Collapsed;
            AuthUrlBox.Text = string.Empty;
            CopyAuthUrlButton.IsEnabled = false;
        }

        private void OnCopyAuthUrlClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(AuthUrlBox.Text);
                InfoText.Text = Loc.T("Cloud_UrlCopied");
            }
            catch (Exception ex)
            {
                // Pano başka bir uygulama tarafından kilitlenmiş olabilir; metin zaten ekranda seçilebilir durumda.
                InfoText.Text = Loc.T("Cloud_UrlCopyFailed", ex.Message);
            }
        }

        private void OnCancelConnectClick(object sender, RoutedEventArgs e)
        {
            _connectCts?.Cancel();
        }

        /// <summary>Pencere kapatılırken süren bir giriş varsa iptal edilir — arkada asılı kalmasın.</summary>
        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _connectCts?.Cancel();
        }

        private void OnDisconnectGoogleDriveClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDisconnect(CloudProviderType.GoogleDrive, "Google Drive", Loc.T("Cloud_RemotePhrase_Google")))
            {
                return;
            }

            GoogleDriveProvider.Disconnect();
            RefreshGoogleDriveStatus();
        }

        private void OnDisconnectOneDriveClick(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDisconnect(CloudProviderType.OneDrive, "OneDrive", Loc.T("Cloud_RemotePhrase_OneDrive")))
            {
                return;
            }

            OneDriveProvider.Disconnect();
            RefreshOneDriveStatus();
        }

        /// <summary>
        /// Bağlantıyı kaldırmadan önce onay alır ve — asıl önemlisi — bu sağlayıcıyı hedef olarak
        /// kullanan görevleri bulup kullanıcıya sorar. Aksi halde görev tanımı sağlayıcıyı istemeye
        /// devam eder ve her gece "bağlı değil" hatası verir; kullanıcı bağlantıyı bilerek kaldırmış
        /// olsa bile yedekleme "başarısız" görünür (sahada tam olarak bu yaşandı).
        /// </summary>
        /// <returns>Bağlantının kaldırılmasına devam edilecekse true.</returns>
        private bool ConfirmDisconnect(CloudProviderType provider, string displayName, string remoteBackupsPhrase)
        {
            List<BackupJob> jobs;
            try
            {
                jobs = _jobStore.LoadAll();
            }
            catch (Exception ex)
            {
                AppLog.Error(Loc.T("Log_DisconnectJobsReadFailed"), ex);
                jobs = new List<BackupJob>();
            }

            var usingJobs = jobs.Where(j => j.Destinations.Any(d => d.Provider == provider)).ToList();
            var baseMessage = Loc.T("Cloud_DisconnectBase", displayName, remoteBackupsPhrase);

            if (usingJobs.Count == 0)
            {
                return MessageBox.Show(this, baseMessage, Loc.T("Cloud_DisconnectTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            }

            // Tek hedefi bu sağlayıcı olan görevlerden hedefi çıkaramayız: hedefsiz bir görev her
            // çalışmada başarısız olur. Bunlar ayrıca belirtilip kullanıcıya bırakılır.
            var removable = usingJobs.Where(j => j.Destinations.Count > 1).ToList();
            var onlyDestination = usingJobs.Where(j => j.Destinations.Count == 1).ToList();

            var message = baseMessage + "\n\n" +
                Loc.T("Cloud_DisconnectJobs", displayName, string.Join(", ", usingJobs.Select(j => "'" + j.Name + "'")));

            if (removable.Count > 0)
            {
                message += Loc.T("Cloud_DisconnectOptionsRemovable", displayName);
            }
            else
            {
                message += Loc.T("Cloud_DisconnectOptionsSimple");
            }

            if (onlyDestination.Count > 0)
            {
                message += Loc.T("Cloud_DisconnectNote", string.Join(", ", onlyDestination.Select(j => "'" + j.Name + "'")), displayName);
            }

            var buttons = removable.Count > 0 ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
            var answer = MessageBox.Show(this, message, Loc.T("Cloud_DisconnectTitle"), buttons, MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None)
            {
                return false;
            }

            if (removable.Count == 0)
            {
                return answer == MessageBoxResult.Yes;
            }

            if (answer == MessageBoxResult.Yes)
            {
                foreach (var job in removable)
                {
                    _jobStore.Update(job.Id, stored => stored.Destinations.RemoveAll(d => d.Provider == provider));
                    AppLog.Info(Loc.T("Log_DestinationRemoved", job.Name, displayName));
                }
            }

            return true;
        }
    }
}
