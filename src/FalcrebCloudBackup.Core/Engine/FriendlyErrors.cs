using System;
using System.Net.Http;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Localization;

namespace FalcrebCloudBackup.Core.Engine
{
    /// <summary>
    /// Kütüphanelerin teknik hata metinlerini ("Bir görev iptal edildi", "An error occurred
    /// while sending the request") geçmiş ekranında ve bildirimlerde seçili dilde anlaşılır bir cümleye
    /// çevirir. Teknik ayrıntı log dosyasında zaten tam olarak duruyor.
    /// </summary>
    public static class FriendlyErrors
    {
        public static string Describe(Exception ex)
        {
            if (ex is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
            {
                ex = aggregate.InnerExceptions[0];
            }

            switch (ex)
            {
                case OperationCanceledException _:
                    // TaskCanceledException da buraya düşer: HttpClient zaman aşımı böyle raporlanır.
                    return Loc.T("Friendly_Timeout");

                case HttpRequestException http:
                    return Loc.T("Friendly_Network", Innermost(http).Message);

                case UnauthorizedAccessException _:
                    return Loc.T("Friendly_AccessDenied", ex.Message);

                default:
                    return ex.Message;
            }
        }

        private static Exception Innermost(Exception ex)
        {
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            return ex;
        }
    }
}
