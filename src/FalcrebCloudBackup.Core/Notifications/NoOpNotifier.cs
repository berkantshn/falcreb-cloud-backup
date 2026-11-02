using System.Threading;
using System.Threading.Tasks;
using FalcrebCloudBackup.Core.Abstractions;
using FalcrebCloudBackup.Core.Models;

namespace FalcrebCloudBackup.Core.Notifications
{
    /// <summary>Hiçbir şey yapmayan INotifier (testler ve bildirimin istenmediği durumlar için).</summary>
    public class NoOpNotifier : INotifier
    {
        public Task NotifyRunCompletedAsync(BackupRunResult result, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyProblemAsync(string title, string body, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
