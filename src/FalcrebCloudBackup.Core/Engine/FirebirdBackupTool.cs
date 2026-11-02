using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FalcrebCloudBackup.Core.Localization;
using FalcrebCloudBackup.Core.Models;
using Microsoft.Win32;

namespace FalcrebCloudBackup.Core.Engine
{
    /// <summary>
    /// Firebird'ün kendi yedekleme aracı gbak.exe ile canlı bir veritabanından tutarlı bir .fbk
    /// üretir. Ham dosya kopyasından farkı: Wolvox/Firebird veritabanını açık tutarken bile
    /// çalışır ve çıkan yedek her zaman geri yüklenebilir (gbak -c) durumdadır.
    /// </summary>
    public static class FirebirdBackupTool
    {
        private static readonly string[] DatabaseExtensions = { ".fdb", ".gdb" };

        public static bool IsDatabaseFile(string path) =>
            DatabaseExtensions.Contains(Path.GetExtension(path) ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        /// <summary>Kaynak klasördeki (alt klasörler dahil) veya tek dosya kaynağındaki Firebird veritabanı dosyaları.</summary>
        public static IReadOnlyList<string> FindDatabaseFiles(string sourcePath, bool isFolder)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return Array.Empty<string>();
            }

            if (!isFolder)
            {
                return IsDatabaseFile(sourcePath) && File.Exists(sourcePath) ? new[] { sourcePath } : Array.Empty<string>();
            }

            if (!Directory.Exists(sourcePath))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories).Where(IsDatabaseFile).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Görevde elle seçilmiş bir gbak yolu varsa onu, yoksa otomatik tespit edileni döndürür (bulunamazsa null).</summary>
        public static string ResolveGbakPath(FirebirdBackupSettings settings, string nearPath)
        {
            if (!string.IsNullOrWhiteSpace(settings?.GbakPath) && File.Exists(settings.GbakPath))
            {
                return settings.GbakPath;
            }

            return FindGbak(nearPath);
        }

        /// <summary>
        /// gbak.exe'yi arar: önce Firebird'ün kayıt defteri kaydı, sonra Program Files altındaki
        /// Firebird klasörleri, sonra kaynak yolun üst klasörlerindeki "Firebird*" klasörleri
        /// (AKINSOFT gibi kendi kurulum dizininde Firebird taşıyan programlar için), son olarak PATH.
        /// </summary>
        public static string FindGbak(string nearPath = null)
        {
            foreach (var root in CandidateRoots(nearPath))
            {
                foreach (var candidate in new[] { Path.Combine(root, "gbak.exe"), Path.Combine(root, "bin", "gbak.exe") })
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> CandidateRoots(string nearPath)
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                string root = null;
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = hklm.OpenSubKey(@"SOFTWARE\Firebird Project\Firebird Server\Instances"))
                    {
                        root = key?.GetValue("DefaultInstance") as string;
                    }
                }
                catch (Exception)
                {
                    // Kayıt defteri okunamazsa diğer yöntemlerle devam edilir.
                }

                if (!string.IsNullOrWhiteSpace(root))
                {
                    yield return root;
                }
            }

            foreach (var programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                     })
            {
                var firebirdDir = Path.Combine(programFiles, "Firebird");
                if (Directory.Exists(firebirdDir))
                {
                    // En yeni sürüm önce (Firebird_5_0 > Firebird_2_5).
                    foreach (var dir in Directory.GetDirectories(firebirdDir).OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return dir;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(nearPath))
            {
                var ancestor = Directory.Exists(nearPath) ? nearPath : Path.GetDirectoryName(nearPath);
                while (!string.IsNullOrEmpty(ancestor))
                {
                    string[] siblings;
                    try
                    {
                        siblings = Directory.GetDirectories(ancestor, "*Firebird*");
                    }
                    catch (Exception)
                    {
                        siblings = Array.Empty<string>();
                    }

                    foreach (var dir in siblings)
                    {
                        yield return dir;
                    }

                    ancestor = Path.GetDirectoryName(ancestor);
                }
            }

            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                yield return dir.Trim();
            }
        }

        /// <summary>
        /// Tek bir veritabanının gbak yedeğini alır. Önce sunucu servisi üzerinden (localhost:),
        /// olmazsa doğrudan dosya yoluyla denenir — Firebird sürümüne/kurulumuna göre biri
        /// çalışmayabiliyor. Kullanıcı adı/şifre komut satırında görünmesin diye ortam
        /// değişkeniyle (ISC_USER/ISC_PASSWORD) verilir.
        /// </summary>
        public static void Backup(string gbakPath, string databasePath, string outputPath, string user, string password, CancellationToken cancellationToken, Action<string> log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var errors = new List<string>();
            foreach (var connection in new[] { "localhost:" + databasePath, databasePath })
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                var exitCode = RunGbak(gbakPath, $"-b -g \"{connection}\" \"{outputPath}\"", user, password, cancellationToken, out var output);
                if (exitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    return;
                }

                var detail = Loc.T("Firebird_AttemptFailed", connection, exitCode, LastLines(output));
                errors.Add(detail);
                log?.Invoke("gbak: " + detail);
            }

            throw new InvalidOperationException(
                Loc.T("Firebird_BackupFailed", Path.GetFileName(databasePath), string.Join(" | ", errors)));
        }

        private static int RunGbak(string gbakPath, string arguments, string user, string password, CancellationToken cancellationToken, out string output)
        {
            var startInfo = new ProcessStartInfo(gbakPath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(gbakPath) ?? string.Empty
            };
            startInfo.EnvironmentVariables["ISC_USER"] = user ?? "SYSDBA";
            startInfo.EnvironmentVariables["ISC_PASSWORD"] = password ?? FirebirdBackupSettings.DefaultPassword;

            var buffer = new StringBuilder();
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (buffer) buffer.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (buffer) buffer.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.WaitForExit(500))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { process.Kill(); } catch (Exception) { /* zaten bitmiş olabilir */ }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                // Asenkron okuyucuların son satırları teslim etmesi için süresiz WaitForExit gerekir.
                process.WaitForExit();
                lock (buffer)
                {
                    output = buffer.ToString();
                }
                return process.ExitCode;
            }
        }

        private static string LastLines(string output, int count = 4)
        {
            var lines = (output ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            return lines.Count == 0 ? Loc.T("Firebird_NoOutput") : string.Join(" / ", lines.Skip(Math.Max(0, lines.Count - count)));
        }
    }
}
