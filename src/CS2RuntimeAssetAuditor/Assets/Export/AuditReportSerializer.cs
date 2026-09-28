using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public sealed class AuditReportSerializer
    {
        public string Serialize(AuditReport report) => SerializeJson(report);

        public string SerializeJson(AuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var serializer = new DataContractJsonSerializer(typeof(AuditReport));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, report);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
