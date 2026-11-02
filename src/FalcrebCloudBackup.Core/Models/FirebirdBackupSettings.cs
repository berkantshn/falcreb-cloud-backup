using FalcrebCloudBackup.Core.Security;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Models
{
    /// <summary>
    /// Kaynak klasörde canlı Firebird veritabanı (.fdb/.gdb — AKINSOFT Wolvox bunu kullanır)
    /// varsa ham dosya kopyası yerine Firebird'ün kendi yedekleme aracı gbak kullanılır.
    /// Nedeni: canlı bir veritabanı dosyası program açıkken ya hiç okunamaz (kilit hatası) ya da
    /// tutarsız/bozuk kopyalanır; gbak ise kullanıcılar bağlıyken bile her zaman tutarlı,
    /// geri yüklenebilir bir .fbk üretir.
    /// </summary>
    public class FirebirdBackupSettings
    {
        /// <summary>null = otomatik: kaynakta veritabanı dosyası bulunur ve gbak tespit edilirse kullan.</summary>
        public bool? UseGbak { get; set; }

        /// <summary>Boşsa gbak.exe otomatik aranır (kayıt defteri + bilinen kurulum klasörleri).</summary>
        public string GbakPath { get; set; }

        public string User { get; set; } = "SYSDBA";

        /// <summary>DPAPI ile şifreli saklanır; düz metin için <see cref="Password"/> kullanın.</summary>
        public string PasswordProtected { get; set; }

        /// <summary>Firebird'ün varsayılan SYSDBA şifresi; Wolvox kurulumlarının büyük çoğunluğu bunu değiştirmez.</summary>
        public const string DefaultPassword = "masterkey";

        [JsonIgnore]
        public string Password
        {
            get => SecretProtector.Unprotect(PasswordProtected) ?? DefaultPassword;
            set => PasswordProtected = string.IsNullOrEmpty(value) || value == DefaultPassword ? null : SecretProtector.Protect(value);
        }
    }
}
