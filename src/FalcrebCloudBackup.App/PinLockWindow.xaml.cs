using System.Windows;
using FalcrebCloudBackup.Auth;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Localization;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    public partial class PinLockWindow : MetroWindow
    {
        private readonly PinAuthProvider _authProvider;

        public PinLockWindow(PinAuthProvider authProvider)
        {
            InitializeComponent();
            _authProvider = authProvider;

            if (_authProvider.State == AuthSetupState.NotConfigured)
            {
                ShowPanel(SetupPanel);
            }
            else
            {
                ShowPanel(UnlockPanel);
                ApplyLoginRequirements();
            }
        }

        /// <summary>Shows only the field(s) actually required right now — see Güvenlik ayarları (SecuritySettingsWindow).</summary>
        private void ApplyLoginRequirements()
        {
            var (pinRequired, totpRequired) = _authProvider.GetLoginRequirements();
            UnlockPinLabel.Visibility = UnlockPinBox.Visibility = pinRequired ? Visibility.Visible : Visibility.Collapsed;
            UnlockTotpLabel.Visibility = UnlockTotpBox.Visibility = totpRequired ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSetupPinClick(object sender, RoutedEventArgs e)
        {
            var pin = SetupPinBox.Password;
            var confirm = SetupPinConfirmBox.Password;

            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
            {
                SetupErrorText.Text = Loc.T("Pin_ErrTooShort");
                return;
            }

            if (pin != confirm)
            {
                SetupErrorText.Text = Loc.T("Pin_ErrMismatch");
                return;
            }

            var recoveryCode = _authProvider.SetupPin(pin);

            MessageBox.Show(
                this,
                Loc.T("Pin_RecoveryMessage", recoveryCode),
                Loc.T("Pin_RecoveryTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void OnUnlockClick(object sender, RoutedEventArgs e)
        {
            if (_authProvider.TryUnlockFull(UnlockPinBox.Password, UnlockTotpBox.Text))
            {
                DialogResult = true;
                Close();
                return;
            }

            UnlockErrorText.Text = Loc.T("Pin_ErrWrong");
            UnlockPinBox.Clear();
            UnlockTotpBox.Clear();
        }

        private void OnForgotPinClick(object sender, RoutedEventArgs e) => ShowPanel(RecoverPanel);

        private void OnBackToUnlockClick(object sender, RoutedEventArgs e)
        {
            ApplyLoginRequirements();
            ShowPanel(UnlockPanel);
        }

        private void OnResetPinClick(object sender, RoutedEventArgs e)
        {
            var newPin = RecoverNewPinBox.Password;
            if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4)
            {
                RecoverErrorText.Text = Loc.T("Pin_ErrNewTooShort");
                return;
            }

            if (!_authProvider.TryResetWithRecoveryCode(RecoveryCodeBox.Text.Trim(), newPin))
            {
                RecoverErrorText.Text = Loc.T("Pin_ErrRecovery");
                return;
            }

            MessageBox.Show(
                this,
                Loc.T("Pin_ResetDone"),
                Loc.T("Common_Success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ShowPanel(UnlockPanel);
        }

        private void ShowPanel(FrameworkElement panel)
        {
            SetupPanel.Visibility = Visibility.Collapsed;
            UnlockPanel.Visibility = Visibility.Collapsed;
            RecoverPanel.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
        }
    }
}
