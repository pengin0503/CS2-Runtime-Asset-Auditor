using CS2RuntimeAssetAuditor.Assets.Export;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>Runtime-side entry point to the shared <see cref="PrivacySanitizer"/> redaction policy.</summary>
    public static class ReportPrivacy
    {
        public static string Sanitize(string text) => string.IsNullOrEmpty(text) ? text : PrivacySanitizer.Shared.SanitizeText(text);
    }
}
