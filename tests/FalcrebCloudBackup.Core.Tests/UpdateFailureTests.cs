using System;
using System.IO;
using FalcrebCloudBackup.Core.Updates;
using Xunit;

namespace FalcrebCloudBackup.Core.Tests
{
    /// <summary>Başarısız kurulumun fark edilmesi ve uygulamanın yeniden açılması için gereken mantık.</summary>
    public sealed class UpdateFailureTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "fcb-tests-" + Guid.NewGuid().ToString("N"));
        private readonly Version _current = new Version(1, 5, 1);

        public UpdateFailureTests()
        {
            Directory.CreateDirectory(_directory);
        }

        public void Dispose() => Directory.Delete(_directory, true);

        [Fact]
        public void Kayit_yoksa_basarisizlik_yoktur()
        {
            Assert.False(new UpdateAttemptStore(_directory).ConsumeFailure(_current, false));
        }

        [Fact]
        public void Istenen_surum_kuruluysa_basarisizlik_yoktur_ve_kayit_silinir()
        {
            var store = new UpdateAttemptStore(_directory);
            store.Begin("1.5.1");
            Assert.False(store.ConsumeFailure(_current, false));
            Assert.Empty(Directory.GetFiles(_directory));
        }

        [Fact]
        public void Istenen_surum_kurulu_surumden_yeniyse_basarisizliktir_ve_bir_kez_bildirilir()
        {
            var store = new UpdateAttemptStore(_directory);
            store.Begin("1.5.2");
            Assert.True(store.ConsumeFailure(_current, false));
            Assert.False(store.ConsumeFailure(_current, false));
        }

        [Fact]
        public void Kurulum_basarisiz_dediyse_kayit_olmasa_da_basarisizliktir()
        {
            Assert.True(new UpdateAttemptStore(_directory).ConsumeFailure(_current, true));
        }

        [Fact]
        public void Bozuk_kayit_basarisizlik_sayilmaz_ve_silinir()
        {
            var store = new UpdateAttemptStore(_directory);
            store.Begin("not-a-version");
            Assert.False(store.ConsumeFailure(_current, false));
            Assert.Empty(Directory.GetFiles(_directory));
        }

        [Fact]
        public void Temizle_kaydi_siler()
        {
            var store = new UpdateAttemptStore(_directory);
            store.Begin("1.5.2");
            store.Clear();
            Assert.False(store.ConsumeFailure(_current, false));
        }

        [Fact]
        public void Komut_kurulum_bitene_kadar_bekler_ve_yalnizca_hatada_uygulamayi_acar()
        {
            var command = VerifiedInstaller.BuildCommandArguments(@"C:\Data dir\Setup.exe", "/VERYSILENT", @"C:\Program Files\App\App.exe");

            Assert.Equal(
                "/d /s /c \"start \"\" /wait \"C:\\Data dir\\Setup.exe\" /VERYSILENT & if errorlevel 1 start \"\" \"C:\\Program Files\\App\\App.exe\" --update-failed\"",
                command);
        }
    }
}
