using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace FalcrebCloudBackup.CloudProviders
{
    /// <summary>
    /// Google girişinde kullanılan yerel geri dönüş sunucusu. Google SDK'sının hazır
    /// <c>LocalServerCodeReceiver</c> sınıfı yerine bunun yazılmasının nedeni: uygulama yönetici
    /// yetkisiyle çalıştığı için (Görev Zamanlayıcı kaydı gerektiriyor) başlattığı tarayıcı da
    /// yükseltilmiş yetkiyle açılıyor. Chrome/Edge o sırada normal kullanıcı olarak zaten açıksa,
    /// yeni pencere mevcut oturuma bağlanamayıp anında kapanıyor — kullanıcı "tarayıcı açıldı, hemen
    /// kapandı" diyor ve uygulama sonsuza kadar bekliyor.
    ///
    /// Bu sınıf aynı işi yapar ama giriş adresini <see cref="LocalBrowserCodeReceiver(Action{string})"/>
    /// ile dışarıya bildirir; arayüz adresi ekranda gösterip "Kopyala" düğmesi sunar. Böylece
    /// tarayıcı otomatik açılamasa bile kullanıcı adresi elle yapıştırarak girişi tamamlayabilir.
    /// </summary>
    public sealed class LocalBrowserCodeReceiver : ICodeReceiver
    {
        private readonly Action<string> _reportAuthorizationUrl;
        private readonly int _port;

        public LocalBrowserCodeReceiver(Action<string> reportAuthorizationUrl)
        {
            _reportAuthorizationUrl = reportAuthorizationUrl;
            _port = FindFreeLoopbackPort();
        }

        // Google SDK'sının kendi varsayılanıyla aynı biçim — "Desktop app" türü OAuth istemcisinde
        // loopback adresleri port farkı gözetmeksizin kabul edilir, ek bir kayıt gerekmez.
        public string RedirectUri => $"http://localhost:{_port}/authorize/";

        public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
        {
            var authorizationUrl = url.Build().AbsoluteUri;

            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(RedirectUri);
                listener.Start();

                try
                {
                    _reportAuthorizationUrl?.Invoke(authorizationUrl);
                    TryOpenBrowser(authorizationUrl);

                    HttpListenerContext context;
                    // İptal edildiğinde (kullanıcı vazgeçti ya da süre doldu) dinleyiciyi kapatmak,
                    // bekleyen GetContextAsync çağrısını sonlandırmanın tek yolu.
                    using (taskCancellationToken.Register(() => { try { listener.Stop(); } catch (Exception) { } }))
                    {
                        try
                        {
                            context = await listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (Exception) when (taskCancellationToken.IsCancellationRequested)
                        {
                            taskCancellationToken.ThrowIfCancellationRequested();
                            throw;
                        }
                    }

                    var result = ParseQuery(context.Request.Url);
                    await WriteClosePageAsync(context.Response, string.IsNullOrEmpty(result.Error)).ConfigureAwait(false);
                    return result;
                }
                finally
                {
                    try { listener.Close(); } catch (Exception) { }
                }
            }
        }

        private static AuthorizationCodeResponseUrl ParseQuery(Uri requestUrl)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in (requestUrl.Query ?? string.Empty).TrimStart('?').Split('&').Where(p => p.Length > 0))
            {
                var parts = pair.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace("+", " ")) : string.Empty;
                values[key] = value;
            }

            return new AuthorizationCodeResponseUrl(values);
        }

        private static async Task WriteClosePageAsync(HttpListenerResponse response, bool success)
        {
            var body = success
                ? "<h2>" + Loc.T("Browser_SuccessTitle") + "</h2><p>" + Loc.T("Browser_SuccessBody") + "</p>"
                : "<h2>" + Loc.T("Browser_FailTitle") + "</h2><p>" + Loc.T("Browser_FailBody") + "</p>";

            var html = "<!DOCTYPE html><html lang=\"" + Loc.Current + "\"><head><meta charset=\"utf-8\"><title>FalcrebCloudBackup</title></head>" +
                       "<body style=\"font-family:Segoe UI,Arial,sans-serif;text-align:center;padding:64px 24px;color:#1c1c1e\">" +
                       body + "</body></html>";

            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            try
            {
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                response.OutputStream.Close();
            }
            catch (Exception)
            {
                // Tarayıcı sekmeyi erken kapatmış olabilir; kod zaten alındı, önemli değil.
            }
        }

        private static void TryOpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // Tarayıcı açılamazsa akış durmaz: kullanıcı adresi arayüzden kopyalayıp elle açabilir.
                AppLog.Warn(Loc.T("Log_BrowserOpenFailed", ex.Message));
            }
        }

        /// <summary>İşletim sisteminden boş bir loopback portu ister (0 portuyla bağlanıp geri bırakarak).</summary>
        private static int FindFreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }
    }
}
