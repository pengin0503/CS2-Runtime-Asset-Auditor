using System;
using System.IO;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class ReportFileWriter
    {
        private const int MaxCollisionRetries = 1000;

        public static string WriteUnique(string directory, string stem, Action<Stream> write, string extension = ".json")
        {
            if (write == null)
                throw new ArgumentNullException(nameof(write));

            var stream = CreateUnique(directory, stem, extension, out var path);
            try
            {
                using (stream)
                    write(stream);
            }
            catch
            {
                TryDeletePartialFile(path);
                throw;
            }

            return path;
        }

        /// <summary>
        /// Atomically claims a new file named <paramref name="stem"/> (plus a numeric suffix on a collision) and
        /// returns it open for writing, for files that are written over time instead of in one call.
        /// </summary>
        public static FileStream CreateUnique(string directory, string stem, string extension, out string path)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A report directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(stem))
                throw new ArgumentException("A report filename stem is required.", nameof(stem));

            for (var attempt = 0; attempt < MaxCollisionRetries; attempt++)
            {
                var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
                path = Path.Combine(directory, stem + suffix + extension);

                try
                {
                    return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Only an error while atomically claiming the path can be retried as a name collision.
                }
            }

            throw new IOException("Could not allocate a unique profiler report filename.");
        }

        private static void TryDeletePartialFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Preserve the original write failure if cleanup cannot remove the partial file.
            }
        }
    }
}
