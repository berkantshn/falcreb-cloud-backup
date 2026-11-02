using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using FalcrebCloudBackup.Core.Storage;

namespace FalcrebCloudBackup.Core.Notifications
{
    /// <summary>
    /// E-posta (SMTP) ve/veya Telegram üzerinden bildirim gönderir. Ayarlar her gönderimde
    /// dosyadan yeniden okunur — sessiz yedekleme ayrı bir işlem olduğundan arayüzde yapılan
    /// bir değişikliği bir sonraki gece hemen görmesi gerekir. Gönderim hataları yalnızca
    /// loglanır; bildirim gidememesi yedeklemeyi asla başarısız saymaz.
    /// </summary>
    public class AlertNotifier : INotifier
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public async Task NotifyRunCompletedAsync(BackupRunResult result, CancellationToken cancellationToken)
        {
            var settings = NotificationSettingsStore.Load();
            var wanted = result.OverallSuccess ? settings.NotifyOnSuccess : settings.NotifyOnFailure;
            if (!settings.AnyChannelEnabled || !wanted)
            {
                return;
            }

            await SendQuietlyAsync(settings, NotificationMessages.SubjectForRun(result), NotificationMessages.BodyForRun(result), cancellationToken).ConfigureAwait(false);
        }

        public async Task NotifyProblemAsync(string title, string body, CancellationToken cancellationToken)
        {
            var settings = NotificationSettingsStore.Load();
            if (!settings.AnyChannelEnabled || !settings.NotifyWhenStale)
            {
                return;
            }

            await SendQuietlyAsync(settings, "[FalcrebCloudBackup] " + title, body, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Ayarlar ekranındaki "Test gönder" için: hatayı yutmaz, kullanıcıya gösterilsin diye fırlatır.</summary>
        public static async Task SendTestAsync(NotificationSettings settings, CancellationToken cancellationToken)
        {
            var body = Loc.T("Notify_TestBody", Environment.MachineName, Loc.FormatDateTime(DateTime.Now));
            var errors = new List<string>();

            if (settings.EmailEnabled)
            {
                try { await SendEmailAsync(settings, Loc.T("Notify_TestSubject"), body).ConfigureAwait(false); }
                catch (Exception ex) { errors.Add(Loc.T("Notify_TestEmailError", ex.Message)); }
            }

            if (settings.TelegramEnabled)
            {
                try { await SendTelegramAsync(settings, Loc.T("Notify_TestSubject") + "\n\n" + body, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex) { errors.Add("Telegram: " + ex.Message); }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }

        private static async Task SendQuietlyAsync(NotificationSettings settings, string subject, string body, CancellationToken cancellationToken)
        {
            if (settings.EmailEnabled)
            {
                try
                {
                    await SendEmailAsync(settings, subject, body).ConfigureAwait(false);
                    AppLog.Info(Loc.T("Log_EmailSent", subject));
                }
                catch (Exception ex)
                {
                    AppLog.Error(Loc.T("Log_EmailFailed"), ex);
                }
            }

            if (settings.TelegramEnabled)
            {
                try
                {
                    await SendTelegramAsync(settings, subject + "\n\n" + body, cancellationToken).ConfigureAwait(false);
                    AppLog.Info(Loc.T("Log_TelegramSent", subject));
                }
                catch (Exception ex)
                {
                    AppLog.Error(Loc.T("Log_TelegramFailed"), ex);
                }
            }
        }

        private static async Task SendEmailAsync(NotificationSettings settings, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(settings.SmtpHost)) throw new InvalidOperationException(Loc.T("Err_SmtpHostEmpty"));
            if (string.IsNullOrWhiteSpace(settings.ToAddresses)) throw new InvalidOperationException(Loc.T("Err_ToEmpty"));

            var from = string.IsNullOrWhiteSpace(settings.FromAddress) ? settings.SmtpUser : settings.FromAddress;
            if (string.IsNullOrWhiteSpace(from)) throw new InvalidOperationException(Loc.T("Err_FromEmpty"));

            using (var message = new MailMessage
            {
                From = new MailAddress(from, "FalcrebCloudBackup", Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false
            })
            {
                foreach (var to in SplitAddresses(settings.ToAddresses))
                {
                    message.To.Add(to);
                }

                using (var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
                {
                    EnableSsl = settings.SmtpUseSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30_000
                })
                {
                    if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
                    {
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword ?? string.Empty);
                    }

                    await client.SendMailAsync(message).ConfigureAwait(false);
                }
            }
        }

        private static async Task SendTelegramAsync(NotificationSettings settings, string text, CancellationToken cancellationToken)
        {
            var token = settings.TelegramBotToken;
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException(Loc.T("Err_TelegramTokenEmpty"));
            if (string.IsNullOrWhiteSpace(settings.TelegramChatId)) throw new InvalidOperationException(Loc.T("Err_TelegramChatEmpty"));

            // Telegram tek mesajda 4096 karakter kabul eder.
            if (text.Length > 4000)
            {
                text = text.Substring(0, 4000) + "…";
            }

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = settings.TelegramChatId.Trim(),
                ["text"] = text
            });

            using (var response = await Http.PostAsync($"https://api.telegram.org/bot{token.Trim()}/sendMessage", content, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(Loc.T("Err_TelegramApi", (int)response.StatusCode, detail));
                }
            }
        }

        private static IEnumerable<string> SplitAddresses(string value) =>
            value.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).Where(a => a.Length > 0);
    }
}
