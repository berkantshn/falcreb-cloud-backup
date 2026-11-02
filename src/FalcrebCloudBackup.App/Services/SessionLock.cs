using System;

namespace FalcrebCloudBackup.App.Services
{
    /// <summary>
    /// "Bir kez aç, işini yap, kilitle" oturum kilidi. Önceden her hassas işlem (görev ekleme,
    /// düzenleme, bulut ayarları, çıkış) ayrı ayrı PIN/doğrulayıcı soruyordu; art arda birkaç ayar
    /// değiştirirken bu çok yoruyordu. Artık tek bir doğrulama belirli bir süre boyunca geçerli olur.
    ///
    /// Süre kısa tutuldu ve pencere gizlenince/kapanınca kilit hemen kapanır — bilgisayar başından
    /// kalkıldığında açık kalmasın diye. Her yetkili işlemde sayaç baştan başlar, böylece aktif
    /// çalışırken iş ortasında kilitlenmez.
    /// </summary>
    public class SessionLock
    {
        public static readonly TimeSpan Duration = TimeSpan.FromMinutes(3);

        private DateTime? _expiresUtc;

        public bool IsUnlocked => _expiresUtc.HasValue && DateTime.UtcNow < _expiresUtc.Value;

        public TimeSpan Remaining
        {
            get
            {
                var left = _expiresUtc.HasValue ? _expiresUtc.Value - DateTime.UtcNow : TimeSpan.Zero;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        /// <summary>Başarılı bir kimlik doğrulamasından sonra çağrılır.</summary>
        public void Unlock() => _expiresUtc = DateTime.UtcNow + Duration;

        /// <summary>Kilit açıkken yapılan her yetkili işlemde süreyi yeniler; kapalıysa hiçbir şey yapmaz (kilidi kendiliğinden açmaz).</summary>
        public void Extend()
        {
            if (IsUnlocked)
            {
                _expiresUtc = DateTime.UtcNow + Duration;
            }
        }

        public void Lock() => _expiresUtc = null;
    }
}
