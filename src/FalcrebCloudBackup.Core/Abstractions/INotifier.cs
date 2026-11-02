using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Core.Abstractions
{
    /// <summary>
    /// Yedekleme sonucu / sorun bildirimi kancası. BackupEngine her çalışmadan sonra çağırır;
    /// uygulama tarafı ise "uzun süredir yedek yok" gibi zamana bağlı uyarılar için
    /// <see cref="NotifyProblemAsync"/> kullanır. Uygulamalar asla fırlatmamalı — bildirim
    /// gönderilememesi yedeklemeyi başarısız saydırmamalı.
    /// </summary>
    public interface INotifier
    {
        Task NotifyRunCompletedAsync(BackupRunResult result, CancellationToken cancellationToken);

        Task NotifyProblemAsync(string title, string body, CancellationToken cancellationToken);
    }
}
