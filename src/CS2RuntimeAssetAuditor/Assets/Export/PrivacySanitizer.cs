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
        private readonly string? _userNameText;
        private readonly string? _machineNameText;

        public PrivacySanitizer(string? userName = null, string? machineName = null)
        {
            _userNameText = RedactableIdentifier(string.IsNullOrWhiteSpace(userName) ? SafeEnvironment(() => Environment.UserName) : userName);
            _machineNameText = RedactableIdentifier(string.IsNullOrWhiteSpace(machineName) ? SafeEnvironment(() => Environment.MachineName) : machineName);
            _userName = BuildIdentifierPattern(_userNameText);
            _machineName = BuildIdentifierPattern(_machineNameText);
        }

        /// <summary>Sanitizer for the current account and machine; the identifier patterns are built once.</summary>
        public static PrivacySanitizer Shared => SharedInstance.Value;

        public string SanitizeText(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;
            return MayNeedRedaction(value!) ? ApplyPatterns(value!) : value!;
        }

        /// <summary>Runs every redaction pattern, without the check that lets most values skip them.</summary>
        internal string ApplyPatterns(string value)
        {
            var sanitized = UncPath.Replace(value, RedactedPath);
            sanitized = WindowsPath.Replace(sanitized, RedactedPath);
            sanitized = UnixPath.Replace(sanitized, RedactedPath);
            if (_userName != null)
                sanitized = _userName.Replace(sanitized, RedactedIdentifier);
            if (_machineName != null)
                sanitized = _machineName.Replace(sanitized, RedactedIdentifier);
            return sanitized;
        }

        /// <summary>
        /// A cheap test that is false only when no pattern can match, so most report strings (prefab names,
        /// numbers as text, identifiers) skip the five regular expressions. Every path pattern needs a slash or a
        /// backslash. An ASCII-only value can contain an ASCII identifier only as a case-insensitive substring;
        /// values or identifiers with other characters always take the full path, because case-insensitive
        /// matching can pair a non-ASCII character with an ASCII one (the Kelvin sign and "k").
        /// </summary>
        private bool MayNeedRedaction(string value)
        {
            foreach (var character in value)
            {
                if (character == '/' || character == '\\' || character > '\u007f')
                    return true;
            }
            return MayContain(value, _userNameText) || MayContain(value, _machineNameText);
        }

        private static bool MayContain(string asciiValue, string? identifier)
        {
            if (identifier == null)
                return false;
            foreach (var character in identifier)
            {
                if (character > '\u007f')
                    return true;
            }
            return asciiValue.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The identifier a pattern is built for, or null when the name is missing or too short to redact.
        private static string? RedactableIdentifier(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;
            var trimmed = identifier!.Trim();
            // Very short names ("a", "pc") would redact ordinary words and destroy useful context.
            return trimmed.Length < MinimumIdentifierLength ? null : trimmed;
        }

        private static Regex? BuildIdentifierPattern(string? trimmed)
        {
            if (trimmed == null)
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
