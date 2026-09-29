using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Colossal.PSI.Environment;

namespace CS2RuntimeAssetAuditor.Export
{
    public sealed class ReportExportResult
    {
        private ReportExportResult(bool success, string path, string error)
        {
            Success = success;
            Path = path;
            Error = error;
        }

        public bool Success { get; }
        public string Path { get; }
        public string Error { get; }

        public static ReportExportResult Succeeded(string path) => new ReportExportResult(true, path, null);
        public static ReportExportResult Failed(string error) => new ReportExportResult(false, null, error);
    }

    public sealed class ReportExporter
    {
        /// <param name="buildMilliseconds">Time the caller spent building the report, written to the timing line.</param>
        public ReportExportResult Export(RuntimeAssetAuditReport report, double? buildMilliseconds = null)
        {
            try
            {
                var directory = Path.Combine(EnvPath.kUserDataPath, "ModsData", Mod.Id);
                Directory.CreateDirectory(directory);

                var timestamp = DateTime.Now;
                var stem = $"CS2RuntimeAssetAuditor-report-{timestamp:yyyy-MM-dd_HHmmss_fff}";
                var start = Stopwatch.GetTimestamp();
                long bytes = 0;
                // The report is serialized straight into the file; it never exists as one JSON string in memory.
                var path = ReportFileWriter.WriteUnique(directory, stem, stream =>
                {
                    using (var buffered = new BufferedStream(stream, 1 << 16))
                    {
                        RuntimeAssetAuditReportSerializer.Serialize(report, buffered);
                        buffered.Flush();
                        bytes = stream.Length;
                    }
                });
                var written = Stopwatch.GetTimestamp();
                Mod.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Report export timing: file={0} buildMs={1} serializeAndWriteMs={2:0.0} bytes={3}",
                    Path.GetFileName(path),
                    buildMilliseconds.HasValue ? buildMilliseconds.Value.ToString("0.0", CultureInfo.InvariantCulture) : "unknown",
                    (written - start) * 1000d / Stopwatch.Frequency,
                    bytes));
                return ReportExportResult.Succeeded(path);
            }
            catch (Exception ex)
            {
                Mod.Error(ex, "Failed to export CS2 Runtime Asset Auditor report");
                return ReportExportResult.Failed(ReportPrivacy.Sanitize(ex.Message));
            }
        }
    }
}
