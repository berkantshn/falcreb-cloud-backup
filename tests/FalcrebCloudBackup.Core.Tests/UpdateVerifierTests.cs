using System;
using System.IO;
using System.Text;
using FalcrebCloudBackup.Core.Updates;
using Xunit;

namespace FalcrebCloudBackup.Core.Tests
{
    /// <summary>Güncellemenin reddedilmesi gereken her durum burada sınanır.</summary>
    public class UpdateVerifierTests
    {
        private const string Url = UpdateVerifier.AllowedUrlPrefix + "v1.5.0/FalcrebCloudBackup-Setup.exe";

        private readonly string _privateKey;
        private readonly string _publicKey;

        public UpdateVerifierTests()
        {
            (_privateKey, _publicKey) = UpdateVerifier.GenerateKeyPair();
        }

        private UpdateManifest SignedManifest()
        {
            var manifest = new UpdateManifest { Version = "1.5.0", Url = Url, Size = 1234, Sha256 = new string('a', 64) };
            manifest.Signature = Convert.ToBase64String(UpdateVerifier.Sign(manifest, _privateKey));
            return manifest;
        }

        [Fact]
        public void Gecerli_imza_kabul_edilir()
        {
            Assert.Null(UpdateVerifier.Validate(SignedManifest(), _publicKey));
        }

        [Fact]
        public void Baska_anahtarla_imzalanan_bildirim_reddedilir()
        {
            var (_, otherPublicKey) = UpdateVerifier.GenerateKeyPair();
            Assert.Equal("invalid signature", UpdateVerifier.Validate(SignedManifest(), otherPublicKey));
        }

        [Theory]
        [InlineData("version")]
        [InlineData("url")]
        [InlineData("size")]
        [InlineData("sha256")]
        public void Herhangi_bir_alani_degistirilen_bildirim_reddedilir(string field)
        {
            var manifest = SignedManifest();
            switch (field)
            {
                case "version": manifest.Version = "9.9.9"; break;
                case "url": manifest.Url = UpdateVerifier.AllowedUrlPrefix + "v9/evil.exe"; break;
                case "size": manifest.Size = 1; break;
                case "sha256": manifest.Sha256 = new string('b', 64); break;
            }

            Assert.Equal("invalid signature", UpdateVerifier.Validate(manifest, _publicKey));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-base64!")]
        [InlineData("AAAA")]
        public void Bozuk_imza_hata_firlatmadan_reddedilir(string signature)
        {
            var manifest = SignedManifest();
            manifest.Signature = signature;
            Assert.False(UpdateVerifier.IsSignatureValid(manifest, _publicKey));
        }

        [Theory]
        [InlineData("http://github.com/berkantshn/falcreb-cloud-backup/releases/download/v1/a.exe")]
        [InlineData("https://evil.example/berkantshn/falcreb-cloud-backup/releases/download/v1/a.exe")]
        [InlineData("https://github.com/someone/falcreb-cloud-backup/releases/download/v1/a.exe")]
        [InlineData("https://github.com/berkantshn/falcreb-cloud-backup/releases/download/../../../x/a.exe")]
        [InlineData(null)]
        public void Beklenmeyen_indirme_adresi_imzali_olsa_bile_reddedilir(string url)
        {
            var manifest = new UpdateManifest { Version = "1.5.0", Url = url, Size = 1, Sha256 = "a" };
            manifest.Signature = Convert.ToBase64String(UpdateVerifier.Sign(manifest, _privateKey));
            Assert.Equal("unexpected download address", UpdateVerifier.Validate(manifest, _publicKey));
        }

        [Theory]
        [InlineData("1.5.0", "1.4.0.0", true)]
        [InlineData("1.5.1", "1.5.0.0", true)]
        [InlineData("1.5.0", "1.5.0.0", false)]
        [InlineData("1.4.9", "1.5.0.0", false)]
        [InlineData("not-a-version", "1.5.0.0", false)]
        [InlineData(null, "1.5.0.0", false)]
        public void Yalnizca_daha_yeni_surum_kabul_edilir(string candidate, string current, bool expected)
        {
            Assert.Equal(expected, UpdateVerifier.IsNewer(candidate, Version.Parse(current)));
        }

        [Fact]
        public void Sha256_bilinen_degerle_eslesir()
        {
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc")))
            {
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", UpdateVerifier.ComputeSha256(stream));
            }
        }

        [Fact]
        public void Gercek_acik_anahtar_tanimli()
        {
            Assert.Equal(64, Convert.FromBase64String(UpdateClient.PublicKey).Length);
        }
    }
}
