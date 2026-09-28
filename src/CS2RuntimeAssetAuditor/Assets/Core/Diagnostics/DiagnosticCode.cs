using System;
using System.Text.RegularExpressions;

namespace CS2RuntimeAssetAuditor.Assets.Core.Diagnostics
{
    public readonly struct DiagnosticCode : IEquatable<DiagnosticCode>
    {
        private static readonly Regex ValidPattern = new Regex(
            @"^APA-(CAT|CEN|GEO|SRF|TEX|EXP|AUD|DEEP)-[0-9]{3}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        public DiagnosticCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !ValidPattern.IsMatch(value))
                throw new ArgumentException("Diagnostic codes must use a supported APA subsystem and three-digit identifier.", nameof(value));
            Value = value;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value) && ValidPattern.IsMatch(Value);

        public bool Equals(DiagnosticCode other) => StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is DiagnosticCode other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(DiagnosticCode left, DiagnosticCode right) => left.Equals(right);

        public static bool operator !=(DiagnosticCode left, DiagnosticCode right) => !left.Equals(right);
    }
}
