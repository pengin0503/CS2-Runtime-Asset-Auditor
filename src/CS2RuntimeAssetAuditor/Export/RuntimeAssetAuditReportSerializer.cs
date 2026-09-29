using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class RuntimeAssetAuditReportSerializer
    {
        private static readonly Regex JsonString = new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled, TimeSpan.FromSeconds(2));
        private static readonly DataContractJsonSerializer ReportSerializer = new DataContractJsonSerializer(typeof(RuntimeAssetAuditReport));
        private static readonly DataContractJsonSerializer StringSerializer = new DataContractJsonSerializer(typeof(string));

        public static string Serialize(RuntimeAssetAuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            using (var stream = new MemoryStream())
            {
                ReportSerializer.WriteObject(stream, report);
                return SanitizeJsonStrings(Encoding.UTF8.GetString(stream.ToArray()), ReportPrivacy.Sanitize);
            }
        }

        internal static string SanitizeJsonStrings(string json, Func<string, string> sanitize)
        {
            // Sanitize decoded values, including escaped Windows paths, then encode them again. A report holds
            // thousands of strings, so only values the sanitizer actually changes are re-encoded.
            return JsonString.Replace(json, match =>
            {
                // A JSON string followed by a colon is a property name, not report data.
                var next = match.Index + match.Length;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next < json.Length && json[next] == ':') return match.Value;

                var value = Unescape(match.Value);
                var clean = sanitize(value);
                if (string.Equals(clean, value, StringComparison.Ordinal))
                    return match.Value;
                using (var output = new MemoryStream())
                {
                    StringSerializer.WriteObject(output, clean);
                    return Encoding.UTF8.GetString(output.ToArray());
                }
            });
        }

        // Decodes a quoted JSON string literal produced by DataContractJsonSerializer.
        internal static string Unescape(string quoted)
        {
            var body = quoted.Substring(1, quoted.Length - 2);
            if (body.IndexOf('\\') < 0)
                return body;
            var builder = new StringBuilder(body.Length);
            for (var index = 0; index < body.Length; index++)
            {
                var character = body[index];
                if (character != '\\' || index + 1 >= body.Length)
                {
                    builder.Append(character);
                    continue;
                }
                var escape = body[++index];
                switch (escape)
                {
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u' when index + 4 < body.Length
                        && int.TryParse(body.Substring(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                        builder.Append((char)code);
                        index += 4;
                        break;
                    default: builder.Append(escape); break;
                }
            }
            return builder.ToString();
        }
    }
}
