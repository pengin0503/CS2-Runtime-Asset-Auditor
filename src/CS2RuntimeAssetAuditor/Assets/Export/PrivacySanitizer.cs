using System;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    /// <summary>
    /// The single redaction policy for every exported report, CSV cell and user-visible error message.
    /// Absolute paths are replaced by <c>[redacted-path]</c>; the account and machine names are replaced by
    /// <c>[redacted]</c> only where they appear as a whole word, so identifiers that merely contain them
    /// (for example "Custom" for the account "tom", or "Game.Simulation" for the account "Game") stay intact.
    /// </summary>
    public sealed class PrivacySanitizer
    {
        public const string RedactedPath = "[redacted-path]";
        public const string RedactedIdentifier = "[redacted]";
        private const int MinimumIdentifierLength = 3;
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly Regex UncPath = new Regex(@"\\\\[^\\/\s]+(?:\\[^\\/\r\n,;<>\""|]+)*", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex WindowsPath = new Regex(@"(?i)\b[A-Z]:\\(?:[^\\/:*?\""<>|\r\n]+\\)*[^\\/:*?\""<>|\r\n]*", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
        // An absolute Unix path: a slash that does not continue a word, URL scheme, or relative path, followed
        // directly by a segment and at least one further separator ("/home/user/..."). A lone " / " or "a/b" is text.
        private static readonly Regex UnixPath = new Regex(@"(?<![\p{L}\p{N}_.:/\\-])/(?=[^\s/\\])(?:[^/\\\r\n,;<>\""|]+/)+[^\\\r\n,;<>\""|]*", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);

        private static readonly Lazy<PrivacySanitizer> SharedInstance = new Lazy<PrivacySanitizer>(() => new PrivacySanitizer());

        private readonly Regex? _userName;
        private readonly Regex? _machineName;

        public PrivacySanitizer(string? userName = null, string? machineName = null)
        {
            _userName = BuildIdentifierPattern(string.IsNullOrWhiteSpace(userName) ? SafeEnvironment(() => Environment.UserName) : userName);
            _machineName = BuildIdentifierPattern(string.IsNullOrWhiteSpace(machineName) ? SafeEnvironment(() => Environment.MachineName) : machineName);
        }

        /// <summary>Sanitizer for the current account and machine; the identifier patterns are built once.</summary>
        public static PrivacySanitizer Shared => SharedInstance.Value;

        public string SanitizeText(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            var sanitized = UncPath.Replace(value, RedactedPath);
            sanitized = WindowsPath.Replace(sanitized, RedactedPath);
            sanitized = UnixPath.Replace(sanitized, RedactedPath);
            if (_userName != null)
                sanitized = _userName.Replace(sanitized, RedactedIdentifier);
            if (_machineName != null)
                sanitized = _machineName.Replace(sanitized, RedactedIdentifier);
            return sanitized;
        }

        private static Regex? BuildIdentifierPattern(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;
            var trimmed = identifier!.Trim();
            // Very short names ("a", "pc") would redact ordinary words and destroy useful context.
            if (trimmed.Length < MinimumIdentifierLength)
                return null;

            // Whole-word match only. A dot joined to a letter or digit continues a dotted identifier
            // ("Game.Simulation"), so the name is not redacted there either.
            var pattern = @"(?<![\p{L}\p{N}_-])(?<![\p{L}\p{N}]\.)" + Regex.Escape(trimmed) + @"(?![\p{L}\p{N}_-])(?!\.[\p{L}\p{N}])";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, MatchTimeout);
        }

        private static string? SafeEnvironment(Func<string> read)
        {
            try { return read(); }
            catch { return null; }
        }
    }
}
