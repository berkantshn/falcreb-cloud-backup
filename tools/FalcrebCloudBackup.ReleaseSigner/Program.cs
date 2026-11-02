using System;
using System.IO;
using System.Net;
using System.Threading;
using FalcrebCloudBackup.Core.Updates;
using Newtonsoft.Json;

namespace FalcrebCloudBackup.ReleaseSigner
{
    /// <summary>
    /// Yalnızca sürüm hazırlarken kullanılan araç; kurulum paketine girmez.
    ///   keygen &lt;özel-anahtar-dosyası&gt;                       anahtar çifti üretir, açık anahtarı yazar
    ///   sign &lt;sürüm&gt; &lt;adres&gt; &lt;kurulum.exe&gt; &lt;update.json&gt;     UPDATE_SIGNING_KEY ile imzalar
    ///   verify &lt;update.json&gt; &lt;kurulum.exe&gt;                  uygulamanın yaptığı doğrulamayı yapar
    ///   check-live &lt;mevcut-sürüm&gt;                           yayındaki sürümü denetler ve indirir
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                switch (args.Length > 0 ? args[0] : string.Empty)
                {
                    case "keygen" when args.Length == 2:
                        return KeyGen(args[1]);
                    case "sign" when args.Length == 5:
                        return Sign(args[1], args[2], args[3], args[4]);
                    case "verify" when args.Length == 3:
                        return Verify(args[1], args[2]);
                    case "check-live" when args.Length == 2:
                        return CheckLive(args[1]);
                    default:
                        Console.Error.WriteLine("Usage: keygen | sign | verify | check-live");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }

        private static int KeyGen(string privateKeyPath)
        {
            if (File.Exists(privateKeyPath))
            {
                // Var olan anahtarın üzerine yazılırsa kurulu uygulamalar bir daha güncellenemez.
                throw new IOException("Key file already exists.");
            }

            var (privateKey, publicKey) = UpdateVerifier.GenerateKeyPair();
            File.WriteAllText(privateKeyPath, privateKey);
            Console.WriteLine(publicKey);
            return 0;
        }

        private static int Sign(string version, string url, string installerPath, string outputPath)
        {
            var key = Environment.GetEnvironmentVariable("UPDATE_SIGNING_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("UPDATE_SIGNING_KEY is not set.");
            }

            var info = new FileInfo(installerPath);
            var manifest = new UpdateManifest { Version = version, Url = url, Size = info.Length };
            using (var stream = info.OpenRead())
            {
                manifest.Sha256 = UpdateVerifier.ComputeSha256(stream);
            }

            manifest.Signature = Convert.ToBase64String(UpdateVerifier.Sign(manifest, key.Trim()));
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));
            return Verify(outputPath, installerPath);
        }

        private static int Verify(string manifestPath, string installerPath)
        {
            var manifest = JsonConvert.DeserializeObject<UpdateManifest>(File.ReadAllText(manifestPath));
            var problem = UpdateVerifier.Validate(manifest, UpdateClient.PublicKey);
            if (problem != null)
            {
                Console.Error.WriteLine("REJECTED: " + problem);
                return 1;
            }

            var info = new FileInfo(installerPath);
            using (var stream = info.OpenRead())
            {
                if (info.Length != manifest.Size || !string.Equals(UpdateVerifier.ComputeSha256(stream), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("REJECTED: installer does not match the manifest");
                    return 1;
                }
            }

            Console.WriteLine($"OK: v{manifest.Version} signature, size and SHA-256 verified.");
            return 0;
        }

        private static int CheckLive(string currentVersion)
        {
            var client = new UpdateClient();
            var manifest = client.CheckAsync(Version.Parse(currentVersion), CancellationToken.None).GetAwaiter().GetResult();
            if (manifest == null)
            {
                Console.WriteLine("No newer version.");
                return 0;
            }

            using (var installer = client.DownloadAsync(manifest, CancellationToken.None).GetAwaiter().GetResult())
            {
                Console.WriteLine($"OK: v{manifest.Version} downloaded and verified.");
            }

            return 0;
        }
    }
}
