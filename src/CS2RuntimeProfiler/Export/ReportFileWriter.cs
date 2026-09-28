using System;
using System.IO;

namespace CS2RuntimeProfiler.Export
{
    public static class ReportFileWriter
    {
        private const int MaxCollisionRetries = 1000;

        public static string WriteUnique(string directory, string stem, Action<Stream> write)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A report directory is required.", nameof(directory));
            if (string.IsNullOrWhiteSpace(stem))
                throw new ArgumentException("A report filename stem is required.", nameof(stem));
            if (write == null)
                throw new ArgumentNullException(nameof(write));

            for (var attempt = 0; attempt < MaxCollisionRetries; attempt++)
            {
                var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
                var path = Path.Combine(directory, stem + suffix + ".json");
                FileStream stream;

                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Only an error while atomically claiming the path can be retried as a name collision.
                    continue;
                }

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
