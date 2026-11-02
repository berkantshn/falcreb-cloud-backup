using System;
using System.Security.Cryptography;

namespace FalcrebCloudBackup.Auth
{
    /// <summary>PBKDF2-based hash/verify for the PIN and recovery code — one more layer under the DPAPI file encryption.</summary>
    internal static class SecretHasher
    {
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;
        private const int Iterations = 100_000;

        public static (string hash, string salt) Hash(string secret)
        {
            var saltBytes = new byte[SaltSizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            var hashBytes = Derive(secret, saltBytes);
            return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
        }

        public static bool Verify(string secret, string expectedHashBase64, string saltBase64)
        {
            var saltBytes = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(expectedHashBase64);
            var actualHash = Derive(secret, saltBytes);
            return CryptographicOperations_FixedTimeEquals(actualHash, expectedHash);
        }

        private static byte[] Derive(string secret, byte[] saltBytes)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(secret, saltBytes, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSizeBytes);
            }
        }

        // .NET Framework 4.8 doesn't have System.Security.Cryptography.CryptographicOperations,
        // so a small constant-time comparison is inlined here instead.
        private static bool CryptographicOperations_FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
