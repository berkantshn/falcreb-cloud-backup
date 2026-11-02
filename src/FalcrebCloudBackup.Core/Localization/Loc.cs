using System;
using System.Globalization;

namespace FalcrebCloudBackup.Core.Localization
{
    /// <summary>
    /// Uygulamanın küçük dil servisi: anahtar verilir, seçili dildeki metin döner. Metinler tek
    /// tabloda (Strings.cs) durur; arayüz, bildirimler, sessiz çalışma ve loglar aynı yolu kullanır.
    ///
    /// Kodda hiçbir karşılaştırma görünen metne bağlı değildir (bkz. ComboBox Tag değerleri, enum'lar).
    /// CurrentCulture bilerek değiştirilmez: tarih/dosya adı gibi yedekleme mantığına giren biçimler
    /// etkilenmesin diye dilin kültürü yalnızca buradaki metin biçimlendirmesinde kullanılır.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Turkish = "tr";

        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

        private static volatile string _current = DetectDefault(CultureInfo.CurrentUICulture);

        /// <summary>Dil değiştiğinde tetiklenir; açık arayüz metinlerini yenilemek içindir.</summary>
        public static event Action LanguageChanged;

        public static string Current => _current;

        public static CultureInfo Culture => _current == Turkish ? TurkishCulture : EnglishCulture;

        /// <summary>Sistem arayüz dili Türkçeyse Türkçe, değilse İngilizce.</summary>
        public static string DetectDefault(CultureInfo uiCulture) =>
            string.Equals(uiCulture?.TwoLetterISOLanguageName, Turkish, StringComparison.OrdinalIgnoreCase) ? Turkish : English;

        /// <summary>Kayıtlı değer geçerliyse onu, boş ya da tanınmıyorsa sistem varsayılanını döndürür.</summary>
        public static string Resolve(string savedLanguage)
        {
            if (string.Equals(savedLanguage, Turkish, StringComparison.OrdinalIgnoreCase))
            {
                return Turkish;
            }

            return string.Equals(savedLanguage, English, StringComparison.OrdinalIgnoreCase)
                ? English
                : DetectDefault(CultureInfo.CurrentUICulture);
        }

        public static void SetLanguage(string savedLanguage)
        {
            var resolved = Resolve(savedLanguage);

            // Yalnızca arayüz kültürü: .NET hata metinleri ve WPF'in hazır menüleri (Kes/Kopyala/Yapıştır)
            // seçili dili izlesin. CurrentCulture'a dokunulmaz, tarih ve sayı biçimleri değişmez.
            var uiCulture = resolved == Turkish ? TurkishCulture : EnglishCulture;
            CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = uiCulture;

            if (resolved == _current)
            {
                return;
            }

            _current = resolved;
            LanguageChanged?.Invoke();
        }

        public static string T(string key) => Strings.Get(key, _current);

        public static string T(string key, params object[] args) => string.Format(Culture, T(key), args);

        /// <summary>Tekil/çoğul için iki anahtar kullanır: key_One ve key_Other (Türkçede ikisi aynıdır).</summary>
        public static string Plural(string key, long count, params object[] args) =>
            T(key + (count == 1 ? "_One" : "_Other"), args);

        /// <summary>Ekrandaki ve bildirimlerdeki tarih-saat; yerel saat verilir.</summary>
        public static string FormatDateTime(DateTime localTime) =>
            localTime.ToString(_current == Turkish ? "dd.MM.yyyy HH:mm" : "dd MMM yyyy HH:mm", Culture);

        /// <summary>Haftanın günü adı; tablo enum adına göre aranır.</summary>
        public static string DayName(DayOfWeek day) => T("Day_" + day);
    }
}
