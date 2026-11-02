using FalcrebCloudBackup.Core.Security;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>
    /// Yedekleme sonucu bildirimleri. Sunucu başında kimse yokken bir yedeklemenin başarısız
    /// olduğu (ya da günlerdir hiç çalışmadığı) ancak böyle fark edilir — bu ayarlar boşsa
    /// uygulama sessiz kalır.
    /// </summary>
    public class NotificationSettings
    {
        public bool EmailEnabled { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; } = true;
        public string SmtpUser { get; set; }
        public string SmtpPasswordProtected { get; set; }
        public string FromAddress { get; set; }

        /// <summary>Birden fazla alıcı noktalı virgül veya virgülle ayrılır.</summary>
        public string ToAddresses { get; set; }

        public bool TelegramEnabled { get; set; }
        public string TelegramBotTokenProtected { get; set; }
        public string TelegramChatId { get; set; }

        public bool NotifyOnFailure { get; set; } = true;
        public bool NotifyOnSuccess { get; set; }
        public bool NotifyWhenStale { get; set; } = true;

        [JsonIgnore]
        public string SmtpPassword
        {
            get => SecretProtector.Unprotect(SmtpPasswordProtected);
            set => SmtpPasswordProtected = SecretProtector.Protect(value);
        }

        [JsonIgnore]
        public string TelegramBotToken
        {
            get => SecretProtector.Unprotect(TelegramBotTokenProtected);
            set => TelegramBotTokenProtected = SecretProtector.Protect(value);
        }

        [JsonIgnore]
        public bool AnyChannelEnabled => EmailEnabled || TelegramEnabled;
    }
}
