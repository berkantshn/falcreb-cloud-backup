using System.IO;
using FalcrebCloudBackup.Core.Storage;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.App.Services
{
    public class UiSettings
    {
        public string Theme { get; set; } = "Dark";

        /// <summary>"en" veya "tr". Boşsa sistem arayüz diline göre belirlenir (bkz. Loc.Resolve).</summary>
        public string Language { get; set; }
    }

    /// <summary>The light/dark and language preferences — separate from BackupJob config since it's purely cosmetic and per-machine.</summary>
    public static class UiSettingsStore
    {
        private static string FilePath => Path.Combine(AppPaths.RootDirectory, "ui_settings.json");

        public static UiSettings Load()
        {
            if (!File.Exists(FilePath))
            {
                return new UiSettings();
            }

            var json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<UiSettings>(json) ?? new UiSettings();
        }

        public static void Save(UiSettings settings)
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(settings));
        }
    }
}
