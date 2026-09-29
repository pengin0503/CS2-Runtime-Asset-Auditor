using System;
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
        public ReportExportResult Export(RuntimeAssetAuditReport report)
        {
            try
            {
                var directory = Path.Combine(EnvPath.kUserDataPath, "ModsData", Mod.Id);
                Directory.CreateDirectory(directory);

                var timestamp = DateTime.Now;
                var stem = $"CS2RuntimeAssetAuditor-report-{timestamp:yyyy-MM-dd_HHmmss_fff}";
                var json = RuntimeAssetAuditReportSerializer.Serialize(report);
                var encoding = new UTF8Encoding(false);
                var path = ReportFileWriter.WriteUnique(directory, stem, stream =>
                {
                    using (var writer = new StreamWriter(stream, encoding))
                        writer.Write(json);
                });
                return ReportExportResult.Succeeded(path);
            }
            catch (Exception ex)
            {
                Mod.Log.Error(ex, "Failed to export CS2 Runtime Asset Auditor report");
                return ReportExportResult.Failed(ReportPrivacy.Sanitize(ex.Message));
            }
        }
    }
}
