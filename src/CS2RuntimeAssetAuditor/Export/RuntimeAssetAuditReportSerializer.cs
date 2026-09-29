using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class RuntimeAssetAuditReportSerializer
    {
        private static readonly DataContractJsonSerializer ReportSerializer = new DataContractJsonSerializer(typeof(RuntimeAssetAuditReport));
        // Makes the serializer's calls without its per-value reflection; null only if a report type gained a shape
        // it does not support, and then the serializer writes the report.
        private static readonly DataContractJsonWriter? FastWriter = DataContractJsonWriter.TryCreate(typeof(RuntimeAssetAuditReport));

        /// <summary>True when reports are written by <see cref="DataContractJsonWriter"/> rather than the serializer.</summary>
        internal static bool UsesFastWriter => FastWriter != null;

        /// <summary>
        /// Writes the report as UTF-8 JSON to <paramref name="output"/>, redacting every string value with
        /// <see cref="ReportPrivacy.Sanitize"/> while it is written. Nothing is buffered beyond the JSON writer, so
        /// a large report never exists as one string in memory.
        /// </summary>
        public static void Serialize(RuntimeAssetAuditReport report, Stream output)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (output == null) throw new ArgumentNullException(nameof(output));
            // The same writer DataContractJsonSerializer.WriteObject(Stream) creates, so the output is unchanged.
            using (var json = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, ownsStream: false))
            {
                var sanitizing = new SanitizingJsonWriter(json, ReportPrivacy.Sanitize);
                if (FastWriter != null)
                    FastWriter.WriteObject(sanitizing, report);
                else
                    ReportSerializer.WriteObject(sanitizing, report);
                sanitizing.Flush();
            }
        }

        public static string Serialize(RuntimeAssetAuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            using (var stream = new MemoryStream())
            {
                Serialize(report, stream);
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }
    }
}
