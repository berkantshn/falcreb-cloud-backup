using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FalcrebCloudBackup.Auth;
using FalcrebCloudBackup.Core.Localization;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    public partial class SecuritySettingsWindow : MetroWindow
    {
        private readonly PinAuthProvider _authProvider;
        private bool _isInitializing = true;

        public SecuritySettingsWindow(PinAuthProvider authProvider)
        {
            InitializeComponent();
            _authProvider = authProvider;

            var (pinRequired, _) = _authProvider.GetLoginRequirements();
            PinEnabledCheck.IsChecked = pinRequired;
            _isInitializing = false;

            RefreshTotpStatus();
        }

        private void RefreshTotpStatus()
        {
            var enabled = _authProvider.IsTotpEnabled;
            TotpStatusText.Text = enabled ? Loc.T("Sec_TotpOn") : Loc.T("Sec_TotpOff");
            EnableTotpButton.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            DisableTotpButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPinEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            try
            {
                _authProvider.SetPinEnabled(PinEnabledCheck.IsChecked == true);
                GeneralErrorText.Text = string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                GeneralErrorText.Text = ex.Message;
                _isInitializing = true;
                PinEnabledCheck.IsChecked = !PinEnabledCheck.IsChecked;
                _isInitializing = false;
            }
        }

        private void OnEnableTotpClick(object sender, RoutedEventArgs e)
        {
            var qrPngBytes = _authProvider.BeginTotpSetup(out var manualKey);

            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(qrPngBytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();

            QrImage.Source = bitmap;
            ManualKeyText.Text = manualKey;
            ConfirmCodeBox.Text = string.Empty;
            TotpErrorText.Text = string.Empty;
            TotpSetupPanel.Visibility = Visibility.Visible;
            EnableTotpButton.Visibility = Visibility.Collapsed;
        }

        private void OnConfirmTotpClick(object sender, RoutedEventArgs e)
        {
            if (_authProvider.ConfirmTotpSetup(ConfirmCodeBox.Text))
            {
                TotpSetupPanel.Visibility = Visibility.Collapsed;
                RefreshTotpStatus();
                MessageBox.Show(this, Loc.T("Sec_TotpEnabledMessage"), Loc.T("Common_Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TotpErrorText.Text = Loc.T("Sec_ErrCode");
        }

        private void OnCancelTotpSetupClick(object sender, RoutedEventArgs e)
        {
            TotpSetupPanel.Visibility = Visibility.Collapsed;
            RefreshTotpStatus();
        }

        private void OnDisableTotpClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                this,
                Loc.T("Sec_RemoveConfirm"),
                Loc.T("Sec_RemoveTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _authProvider.DisableTotp();
                GeneralErrorText.Text = string.Empty;
                RefreshTotpStatus();
            }
            catch (InvalidOperationException ex)
            {
                GeneralErrorText.Text = ex.Message;
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
