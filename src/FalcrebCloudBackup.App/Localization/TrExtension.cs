using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using FalcrebCloudBackup.Core.Localization;

namespace FalcrebCloudBackup.App.Localization
{
    /// <summary>XAML'deki {loc:Tr Anahtar} bağlamalarının kaynağı; dil değişince hepsini tek seferde yeniler.</summary>
    public sealed class TranslationSource : INotifyPropertyChanged
    {
        public static TranslationSource Instance { get; } = new TranslationSource();

        private TranslationSource()
        {
            Loc.LanguageChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string this[string key] => Loc.T(key);
    }

    /// <summary>XAML kullanımı: Text="{loc:Tr Main_RunNow}". Metin bağlama ile geldiği için dil hemen değişir.</summary>
    public sealed class TrExtension : MarkupExtension
    {
        public TrExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("[" + Key + "]")
            {
                Source = TranslationSource.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
