using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class RuntimeAssetAuditReportSerializer
    {
        private static readonly Regex JsonString = new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

        public static string Serialize(RuntimeAssetAuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var serializer = new DataContractJsonSerializer(typeof(RuntimeAssetAuditReport));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, report);
                return SanitizeJsonStrings(Encoding.UTF8.GetString(stream.ToArray()), ReportPrivacy.Sanitize);
            }
        }

        internal static string SanitizeJsonStrings(string json, Func<string, string> sanitize)
        {
            // Sanitize decoded values, including escaped Windows paths, then encode them again.
            return JsonString.Replace(json, match =>
            {
                // A JSON string followed by a colon is a property name, not report data.
                var next = match.Index + match.Length;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next < json.Length && json[next] == ':') return match.Value;

                var stringSerializer = new DataContractJsonSerializer(typeof(string));
                using (var input = new MemoryStream(Encoding.UTF8.GetBytes(match.Value)))
                {
                    var value = (string)stringSerializer.ReadObject(input);
                    var clean = sanitize(value);
                    using (var output = new MemoryStream())
                    {
                        stringSerializer.WriteObject(output, clean);
                        return Encoding.UTF8.GetString(output.ToArray());
                    }
                }
            });
        }
    }
}
