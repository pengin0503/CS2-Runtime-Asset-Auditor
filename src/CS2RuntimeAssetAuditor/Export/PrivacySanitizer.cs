using System;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Export
{
    public static class PrivacySanitizer
    {
        private static readonly Regex WindowsUserPath = new Regex(
            @"\b[A-Z]:\\Users\\[^\\\r\n\""']+(?:\\[^\r\n\""']+)*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MacUserPath = new Regex(
            @"/Users/[^/\r\n\""']+(?:/[^\r\n\""']+)*",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex LinuxUserPath = new Regex(
            @"/home/[^/\r\n\""']+(?:/[^\r\n\""']+)*",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sanitized = WindowsUserPath.Replace(text, "<user-path>");
            sanitized = MacUserPath.Replace(sanitized, "<user-path>");
            sanitized = LinuxUserPath.Replace(sanitized, "<user-path>");

            // A diagnostic may contain the current account name without an absolute path.
            // Avoid replacing generic/very short account names because that would destroy useful context.
            var userName = Environment.UserName;
            if (!string.IsNullOrWhiteSpace(userName) && userName.Length >= 3)
            {
                sanitized = Regex.Replace(
                    sanitized,
                    Regex.Escape(userName),
                    "<user>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return sanitized;
        }
    }
}
