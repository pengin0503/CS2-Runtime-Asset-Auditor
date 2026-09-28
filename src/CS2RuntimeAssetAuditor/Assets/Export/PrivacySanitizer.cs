using System;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public sealed class PrivacySanitizer
    {
        private static readonly Regex UncPath = new Regex(@"\\\\[^\\/\s]+(?:\\[^\\/\r\n,;<>\""|]+)*", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        private static readonly Regex WindowsPath = new Regex(@"(?i)\b[A-Z]:\\(?:[^\\/:*?\""<>|\r\n]+\\)*[^\\/:*?\""<>|\r\n]*", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        // An absolute Unix path: a slash that does not continue a word, URL scheme, or relative path, followed
        // directly by a segment and at least one further separator ("/home/user/..."). A lone " / " or "a/b" is text.
        private static readonly Regex UnixPath = new Regex(@"(?<![\p{L}\p{N}_.:/\\-])/(?=[^\s/\\])(?:[^/\\\r\n,;<>\""|]+/)+[^\\\r\n,;<>\""|]*", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        private readonly string? _userName;
        private readonly string? _machineName;

        public PrivacySanitizer(string? userName = null, string? machineName = null)
        {
            _userName = string.IsNullOrWhiteSpace(userName) ? Environment.UserName : userName;
            _machineName = string.IsNullOrWhiteSpace(machineName) ? Environment.MachineName : machineName;
        }

        public string SanitizeText(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            var sanitized = UncPath.Replace(value, "[redacted-path]");
            sanitized = WindowsPath.Replace(sanitized, "[redacted-path]");
            sanitized = UnixPath.Replace(sanitized, "[redacted-path]");
            sanitized = ReplaceIdentifier(sanitized, _userName);
            sanitized = ReplaceIdentifier(sanitized, _machineName);
            return sanitized;
        }

        private static string ReplaceIdentifier(string value, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return value;

            var pattern = @"(?<![\p{L}\p{N}_-])" + Regex.Escape(identifier) + @"(?![\p{L}\p{N}_-])";
            return Regex.Replace(value, pattern, "[redacted]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
    }
}
