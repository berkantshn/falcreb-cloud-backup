using System.Security.Cryptography;
using System.Text;

namespace FalcrebCloudBackup.Auth
{
    /// <summary>
    /// Produces a one-time recovery code like "7F3K9-QRT4M-XW2LP" the user writes down at setup.
    /// The alphabet skips 0/O/1/I/L so a handwritten copy stays unambiguous.
    /// </summary>
    internal static class RecoveryCodeGenerator
    {
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        private const int GroupCount = 3;
        private const int GroupLength = 5;

        public static string Generate()
        {
            var sb = new StringBuilder();
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[1];
                for (var g = 0; g < GroupCount; g++)
                {
                    if (g > 0)
                    {
                        sb.Append('-');
                    }

                    for (var i = 0; i < GroupLength; i++)
                    {
                        rng.GetBytes(buffer);
                        sb.Append(Alphabet[buffer[0] % Alphabet.Length]);
                    }
                }
            }

            return sb.ToString();
        }
    }
}
