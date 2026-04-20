using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyKeys
{
    /// <summary>
    /// Small helper for atomic file writes. Writes to a unique temporary file
    /// in the same directory, then uses <see cref="File.Move(string, string, bool)"/>
    /// to replace the destination — so a crash mid-write leaves the previous
    /// version intact instead of producing a half-written JSON file that the
    /// next load fails on.
    ///
    /// Using a unique tmp path (GUID suffix) allows concurrent writers without
    /// clobbering each other's temporary data; only the final move is racy,
    /// and callers that care about that serialize via their own lock.
    /// </summary>
    internal static class AtomicFile
    {
        private static string MakeTempPath(string path) =>
            path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        public static void WriteAllText(string path, string contents)
        {
            var tmp = MakeTempPath(path);
            try
            {
                File.WriteAllText(tmp, contents);
                File.Move(tmp, path, overwrite: true);
            }
            catch
            {
                TryDelete(tmp);
                throw;
            }
        }

        public static async Task WriteAllTextAsync(
            string path,
            string contents,
            CancellationToken ct = default)
        {
            var tmp = MakeTempPath(path);
            try
            {
                await File.WriteAllTextAsync(tmp, contents, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                File.Move(tmp, path, overwrite: true);
            }
            catch
            {
                TryDelete(tmp);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort — don't mask the original exception */ }
        }
    }
}
