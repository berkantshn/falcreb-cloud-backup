using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Notifications;
using FalcrebCloudBackup.Core.Storage;
using MahApps.Metro.Controls;

namespace FalcrebCloudBackup.App
{
    /// <summary>E-posta / Telegram bildirim ayarları; "Test Gönder" kaydetmeden önce alanlardaki değerlerle dener.</summary>
    public partial class NotificationSettingsWindow : MetroWindow
    {
        private bool _isLoading = true;

        public NotificationSettingsWindow()
        {
            InitializeComponent();
            Populate(NotificationSettingsStore.Load());
            _isLoading = false;
        }

        private void Populate(NotificationSettings settings)
        {
            NotifyOnFailureCheck.IsChecked = settings.NotifyOnFailure;
            NotifyWhenStaleCheck.IsChecked = settings.NotifyWhenStale;
            NotifyOnSuccessCheck.IsChecked = settings.NotifyOnSuccess;

            EmailEnabledCheck.IsChecked = settings.EmailEnabled;
            SmtpHostBox.Text = settings.SmtpHost ?? string.Empty;
            SmtpPortBox.Text = settings.SmtpPort.ToString();
            SmtpSslCheck.IsChecked = settings.SmtpUseSsl;
            SmtpUserBox.Text = settings.SmtpUser ?? string.Empty;
            SmtpPasswordBox.Password = settings.SmtpPassword ?? string.Empty;
            FromBox.Text = settings.FromAddress ?? string.Empty;
            ToBox.Text = settings.ToAddresses ?? string.Empty;

            TelegramEnabledCheck.IsChecked = settings.TelegramEnabled;
            TelegramTokenBox.Password = settings.TelegramBotToken ?? string.Empty;
            TelegramChatIdBox.Text = settings.TelegramChatId ?? string.Empty;

            // Kayıtlı sunucu bir ön ayarla eşleşiyorsa onu seç, yoksa "Özel".
            var preset = PresetCombo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => ((string)i.Tag).Split('|')[0].Equals(settings.SmtpHost, StringComparison.OrdinalIgnoreCase));
            PresetCombo.SelectedItem = preset ?? PresetCombo.Items.OfType<ComboBoxItem>().Last();

            OnChannelToggled(this, new RoutedEventArgs());
        }

        private void OnChannelToggled(object sender, RoutedEventArgs e)
        {
            EmailPanel.IsEnabled = EmailEnabledCheck.IsChecked == true;
            TelegramPanel.IsEnabled = TelegramEnabledCheck.IsChecked == true;
        }

        private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            var tag = (PresetCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            var parts = tag.Split('|');
            SmtpHostBox.Text = parts[0];
            SmtpPortBox.Text = parts[1];
            SmtpSslCheck.IsChecked = true;
        }

        private NotificationSettings BuildSettings(out string validationError)
        {
            validationError = null;
            var settings = new NotificationSettings
            {
                NotifyOnFailure = NotifyOnFailureCheck.IsChecked == true,
                NotifyWhenStale = NotifyWhenStaleCheck.IsChecked == true,
                NotifyOnSuccess = NotifyOnSuccessCheck.IsChecked == true,

                EmailEnabled = EmailEnabledCheck.IsChecked == true,
                SmtpHost = SmtpHostBox.Text.Trim(),
                SmtpUseSsl = SmtpSslCheck.IsChecked == true,
                SmtpUser = SmtpUserBox.Text.Trim(),
                SmtpPassword = SmtpPasswordBox.Password,
                FromAddress = FromBox.Text.Trim(),
                ToAddresses = ToBox.Text.Trim(),

                TelegramEnabled = TelegramEnabledCheck.IsChecked == true,
                TelegramBotToken = TelegramTokenBox.Password.Trim(),
                TelegramChatId = TelegramChatIdBox.Text.Trim()
            };

            if (!int.TryParse(SmtpPortBox.Text.Trim(), out var port) || port <= 0 || port > 65535)
            {
                port = 587;
            }
            settings.SmtpPort = port;

            if (settings.EmailEnabled)
            {
                if (string.IsNullOrWhiteSpace(settings.SmtpHost)) validationError = Loc.T("NotifyWin_ErrHost");
                else if (string.IsNullOrWhiteSpace(settings.ToAddresses)) validationError = Loc.T("NotifyWin_ErrTo");
                else if (string.IsNullOrWhiteSpace(settings.SmtpUser) && string.IsNullOrWhiteSpace(settings.FromAddress)) validationError = Loc.T("NotifyWin_ErrFrom");
                else if (port == 465) validationError = Loc.T("NotifyWin_ErrPort465");
            }

            if (validationError == null && settings.TelegramEnabled)
            {
                if (string.IsNullOrWhiteSpace(settings.TelegramBotToken)) validationError = Loc.T("NotifyWin_ErrToken");
                else if (string.IsNullOrWhiteSpace(settings.TelegramChatId)) validationError = Loc.T("NotifyWin_ErrChat");
            }

            return settings;
        }

        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            var settings = BuildSettings(out var error);
            if (error != null)
            {
                ShowStatus(error, isError: true);
                return;
            }

            if (!settings.AnyChannelEnabled)
            {
                ShowStatus(Loc.T("NotifyWin_ErrNoChannel"), isError: true);
                return;
            }

            TestButton.IsEnabled = false;
            ShowStatus(Loc.T("NotifyWin_TestSending"), isError: false);
            try
            {
                await AlertNotifier.SendTestAsync(settings, CancellationToken.None);
                ShowStatus(Loc.T("NotifyWin_TestSent"), isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus(Loc.T("NotifyWin_TestFailed", ex.Message), isError: true);
            }
            finally
            {
                TestButton.IsEnabled = true;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var settings = BuildSettings(out var error);
            if (error != null)
            {
                ShowStatus(error, isError: true);
                return;
            }

            NotificationSettingsStore.Save(settings);
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError
                ? Brushes.OrangeRed
                : new SolidColorBrush(Color.FromRgb(0x2F, 0xB3, 0x44));
        }
    }
}
