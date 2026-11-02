using System;
using System.IO;
using FalcrebCloudBackup.Core.Storage;

namespace FalcrebCloudBackup.Core.Updates
{
    /// <summary>
    /// Güncelleme denemesinin kaydı. Uygulama kurulum başlamadan kapandığı için kurulumun sonucunu
    /// göremez; bir sonraki açılışta bu kayıt ve kendi sürümü ile sonucu anlar. Kurulum bilgisayar
    /// kapanması gibi bir nedenle yarım kalsa bile durum böylece fark edilir.
    /// </summary>
    public class UpdateAttemptStore
    {
        /// <summary>Kurulum başarısız olunca uygulama bu bağımsız değişkenle yeniden açılır.</summary>
        public const string FailedArgument = "--update-failed";

        private readonly string _path;

        public UpdateAttemptStore(string directory = null)
        {
            _path = Path.Combine(directory ?? AppPaths.RootDirectory, "update-pending.txt");
        }

        public void Begin(string targetVersion) => File.WriteAllText(_path, targetVersion);

        public void Clear()
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        /// <summary>
        /// Son deneme başarısız mıydı? Kayıt okunduktan sonra silinir, böylece uyarı bir kez gösterilir.
        /// Kurulum başarısız olduğunu bildirdiyse ya da istenen sürüm hâlâ kurulu sürümden yeniyse başarısızdır.
        /// </summary>
        public bool ConsumeFailure(Version current, bool reportedByInstaller)
        {
            string target = null;
            if (File.Exists(_path))
            {
                target = File.ReadAllText(_path).Trim();
                File.Delete(_path);
            }

            return reportedByInstaller || (target != null && UpdateVerifier.IsNewer(target, current));
        }
    }
}
