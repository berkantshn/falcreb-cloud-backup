using System.IO;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using FalcrebCloudBackup.Core.Models;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>Bildirim ayarlarını ProgramData altında tutar; şifreler dosyada DPAPI ile şifrelidir (bkz. NotificationSettings).</summary>
    public static class NotificationSettingsStore
    {
        private static string FilePath => Path.Combine(AppPaths.RootDirectory, "notifications.json");

        public static NotificationSettings Load()
        {
            AppPaths.EnsureCreated();
            var json = StoreFile.ReadAllTextOrNull(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new NotificationSettings();
            }

            try
            {
                return JsonConvert.DeserializeObject<NotificationSettings>(json) ?? new NotificationSettings();
            }
            catch (JsonException ex)
            {
                AppLog.Error(Loc.T("Log_NotifySettingsReadFailed"), ex);
                return new NotificationSettings();
            }
        }

        public static void Save(NotificationSettings settings)
        {
            AppPaths.EnsureCreated();
            StoreFile.WriteAllText(FilePath, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
    }
}
