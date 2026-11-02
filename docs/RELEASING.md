# Yeni sürüm yayımlama

1. `src/FalcrebCloudBackup.App/FalcrebCloudBackup.App.csproj` içindeki `Version` değerini artır ve `CHANGELOG.md`'ye yeni sürümü ekle.
2. Değişikliği PR ile main'e al.
3. main'de aynı sürümle tag at: `git tag -a v1.5.1 -m "v1.5.1"` ve `git push origin v1.5.1`.

GitHub Actions gerisini yapar: testler, kurulum dosyası, imza, sessiz kurulum sınaması ve yayın. Bir adım geçmezse hiçbir şey yayımlanmaz. Kurulu uygulamalar yeni sürümü en geç 24 saat içinde görür.

## İmzalama anahtarı

- Özel anahtar yalnızca iki yerde durur: `UPDATE_SIGNING_KEY` adlı GitHub secret ve proje klasöründeki `.env` (git'e girmez, Drive yedeğine girer).
- Açık anahtar `UpdateClient.PublicKey` sabitindedir.
- **Özel anahtar kaybolursa** kurulu uygulamalar yeni imzaları kabul etmez. Yeni anahtar üretilir ve kullanıcılar bir kez elle kurulum yapar.
- **Özel anahtar sızarsa** GitHub secret'ını hemen sil, yeni anahtar üret, açık anahtarı değiştirip yeni sürüm yayımla ve kullanıcılara elle güncellemelerini söyle.

Anahtar üretmek için: `FalcrebCloudBackup.ReleaseSigner.exe keygen <dosya>`. Aracı var olan bir dosyanın üzerine yazmaz.

## Yayının doğrulanması

Yayından sonra canlı sürümü, uygulamanın kullandığı kodla denetlemek için **yönetici olarak açılmış** bir PowerShell'de:

```powershell
tools\FalcrebCloudBackup.ReleaseSigner\bin\Release\net48\FalcrebCloudBackup.ReleaseSigner.exe check-live <bir önceki sürüm>
```

Bu komut sürümü indirip imzasını doğrular, kurmaz. Yönetici olmayan bir kabukta hiçbir şey indirmeden hata verir.
