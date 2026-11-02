using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.Core.Updates
{
    /// <summary>Sürüm bildirim dosyası (update.json): GitHub Releases'te kurulum dosyasının yanında durur.</summary>
    public class UpdateManifest
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }
    }

    /// <summary>
    /// Güncellemenin gerçekten bu projeden geldiğini doğrular. Windows'un kendi ECDSA P-256
    /// altyapısını kullanır, ek kütüphane gerektirmez. Özel anahtar yalnızca sürüm imzalanırken
    /// kullanılır ve kaynak kodda bulunmaz; uygulamada yalnızca açık anahtar vardır.
    /// </summary>
    public static class UpdateVerifier
    {
        /// <summary>Yalnızca bu adresten gelen kurulum dosyası kabul edilir.</summary>
        public const string AllowedUrlPrefix = "https://github.com/berkantshn/falcreb-cloud-backup/releases/download/";

        private const int CoordinateLength = 32;

        /// <summary>
        /// İmzalanan metin. Bildirimdeki her alan bu metne girdiği için, biri dosyada tek bir
        /// değeri değiştirirse imza tutmaz. Başındaki etiket, imzanın başka bir amaçla
        /// imzalanmış veriyle karıştırılmasını önler.
        /// </summary>
        public static byte[] BuildSignedPayload(UpdateManifest manifest) =>
            Encoding.UTF8.GetBytes(string.Join("\n",
                "falcreb-cloud-backup-update-v1",
                manifest.Version,
                manifest.Url,
                manifest.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (manifest.Sha256 ?? string.Empty).ToLowerInvariant()));

        public static byte[] Sign(UpdateManifest manifest, string privateKeyBase64)
        {
            var key = Convert.FromBase64String(privateKeyBase64);
            if (key.Length != CoordinateLength * 3)
            {
                throw new InvalidDataException("Invalid private key.");
            }

            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = key.Take(CoordinateLength).ToArray(),
                Q = new ECPoint
                {
                    X = key.Skip(CoordinateLength).Take(CoordinateLength).ToArray(),
                    Y = key.Skip(CoordinateLength * 2).ToArray(),
                },
            };
            using (var ecdsa = ECDsa.Create(parameters))
            {
                return ecdsa.SignData(BuildSignedPayload(manifest), HashAlgorithmName.SHA256);
            }
        }

        /// <summary>Yeni bir anahtar çifti üretir. Dönen değerler: özel anahtar, açık anahtar (base64).</summary>
        public static (string PrivateKey, string PublicKey) GenerateKeyPair()
        {
            using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                var p = ecdsa.ExportParameters(true);
                var publicKey = p.Q.X.Concat(p.Q.Y).ToArray();
                return (Convert.ToBase64String(p.D.Concat(publicKey).ToArray()), Convert.ToBase64String(publicKey));
            }
        }

        public static bool IsSignatureValid(UpdateManifest manifest, string publicKeyBase64)
        {
            try
            {
                var key = Convert.FromBase64String(publicKeyBase64);
                var signature = Convert.FromBase64String(manifest.Signature ?? string.Empty);
                if (key.Length != CoordinateLength * 2 || signature.Length != CoordinateLength * 2)
                {
                    return false;
                }

                var parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = key.Take(CoordinateLength).ToArray(),
                        Y = key.Skip(CoordinateLength).ToArray(),
                    },
                };
                using (var ecdsa = ECDsa.Create(parameters))
                {
                    return ecdsa.VerifyData(BuildSignedPayload(manifest), signature, HashAlgorithmName.SHA256);
                }
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public static bool IsUrlAllowed(string url) =>
            url != null
            && url.StartsWith(AllowedUrlPrefix, StringComparison.Ordinal)
            && !url.Contains("..")
            && Uri.TryCreate(url, UriKind.Absolute, out _);

        /// <summary>Yalnızca daha yeni sürüm kabul edilir: eski ve açık bilinen bir sürüme geri döndürülemez.</summary>
        public static bool IsNewer(string candidate, Version current) =>
            Version.TryParse(candidate, out var parsed) && parsed > current;

        public static string ComputeSha256(Stream stream)
        {
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            }
        }

        /// <summary>Bildirimin tamamını sınar; geçerliyse null, değilse sebebi döner.</summary>
        public static string Validate(UpdateManifest manifest, string publicKeyBase64)
        {
            if (manifest == null || manifest.Size <= 0)
            {
                return "incomplete manifest";
            }

            if (!IsUrlAllowed(manifest.Url))
            {
                return "unexpected download address";
            }

            return IsSignatureValid(manifest, publicKeyBase64) ? null : "invalid signature";
        }
    }
}
