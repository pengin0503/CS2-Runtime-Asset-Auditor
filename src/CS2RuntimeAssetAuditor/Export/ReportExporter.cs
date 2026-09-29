using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
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
                var json = RuntimeAssetAuditReportSerializer.Serialize(report);
                var serialized = Stopwatch.GetTimestamp();
                var encoding = new UTF8Encoding(false);
                var path = ReportFileWriter.WriteUnique(directory, stem, stream =>
                {
                    using (var writer = new StreamWriter(stream, encoding))
                        writer.Write(json);
                });
                var written = Stopwatch.GetTimestamp();
                // The export runs on the main thread, so these times are the length of the freeze it causes.
                Mod.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Report export timing: file={0} buildMs={1} serializeMs={2:0.0} writeMs={3:0.0} characters={4}",
                    Path.GetFileName(path),
                    buildMilliseconds.HasValue ? buildMilliseconds.Value.ToString("0.0", CultureInfo.InvariantCulture) : "unknown",
                    (serialized - start) * 1000d / Stopwatch.Frequency,
                    (written - serialized) * 1000d / Stopwatch.Frequency,
                    json.Length));
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
