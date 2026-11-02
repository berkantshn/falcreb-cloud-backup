using System.Linq;

namespace FalcrebCloudBackup.Core.Storage
{
    /// <summary>
    /// Eski sürümden gelen görevlerde yalnızca LastRunUtc vardı; durum göstergesi ve "uzun
    /// süredir yedek yok" uyarısı için gereken LastRunSuccess/LastSuccessUtc alanları geçmiş
    /// kayıtlarından doldurulur. Sonraki her çalışma bu alanları zaten kendisi yazar; burası
    /// yalnızca "son başarılı" bilgisi hâlâ boş olan görevler için çalışır.
    /// </summary>
    public static class JobRunInfoBackfill
    {
        public static void Apply(JobStore jobStore, HistoryStore historyStore)
        {
            var jobs = jobStore.LoadAll();
            var candidates = jobs.Where(j => j.LastRunUtc.HasValue && !j.LastSuccessUtc.HasValue).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var history = historyStore.LoadAll();
            foreach (var job in candidates)
            {
                var runs = history.Where(r => r.JobId == job.Id).OrderBy(r => r.FinishedUtc).ToList();
                if (runs.Count == 0)
                {
                    continue;
                }

                var last = runs[runs.Count - 1];
                var lastSuccess = runs.LastOrDefault(r => r.OverallSuccess);
                if (lastSuccess == null && job.LastRunSuccess.HasValue)
                {
                    // Doldurulacak yeni bir bilgi yok.
                    continue;
                }

                jobStore.Update(job.Id, stored =>
                {
                    if (!stored.LastRunSuccess.HasValue)
                    {
                        stored.LastRunSuccess = last.OverallSuccess;
                        stored.LastRunError = last.FatalError ?? last.DestinationResults.FirstOrDefault(d => !d.Success)?.ErrorMessage;
                    }

                    if (!stored.LastSuccessUtc.HasValue && lastSuccess != null)
                    {
                        stored.LastSuccessUtc = lastSuccess.FinishedUtc;
                    }
                });
            }
        }
    }
}
