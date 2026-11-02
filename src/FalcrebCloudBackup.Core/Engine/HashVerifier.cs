using System.IO;
using System.Security.Cryptography;

namespace FalcrebCloudBackup.Core.Engine
{
    /// <summary>Confirms an upload landed intact by comparing local vs. remote SHA-256.</summary>
    public static class HashVerifier
    {
        public static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return ToHex(hash);
            }
        }

        public static string ComputeSha256(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                return ToHex(hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var hex = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                hex.Append(b.ToString("x2"));
            }
            return hex.ToString();
        }
    }
}
