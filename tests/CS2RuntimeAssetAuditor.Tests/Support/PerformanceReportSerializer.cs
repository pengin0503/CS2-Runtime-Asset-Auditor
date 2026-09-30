using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CS2RuntimeAssetAuditor.Export
{
    // Test support only: the mod writes reports through RuntimeAssetAuditReportSerializer.
    public static class PerformanceReportSerializer
    {
        public static string Serialize(PerformanceReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var serializer = new DataContractJsonSerializer(typeof(PerformanceReport));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, report.SanitizedCopy());
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
