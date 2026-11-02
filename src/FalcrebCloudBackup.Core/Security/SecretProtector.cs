using System;
using System.Security.Cryptography;
using System.Text;

namespace FalcrebCloudBackup.Core.Security
{
    /// <summary>
    /// Ayar dosyalarındaki şifreleri (Firebird şifresi, SMTP şifresi, Telegram bot token'ı) düz
    /// metin yerine Windows DPAPI ile şifreli saklar. LocalMachine kapsamı seçildi çünkü sessiz
    /// yedekleme Task Scheduler'ın açtığı ayrı bir oturumda (S4U) çalışır ve CurrentUser kapsamı
    /// orada her zaman çözülemeyebilir. Şifreli değer yalnızca aynı bilgisayarda açılabilir.
    /// </summary>
    public static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FalcrebCloudBackup.secrets.v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return null;
            }

            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return null;
            }

            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                // Başka bir bilgisayardan kopyalanmış ayar dosyası vb. — şifre yok sayılır, kullanıcı yeniden girer.
                return null;
            }
        }
    }
}
