namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>
    /// The single result protocol of every export binding: <c>ok:&lt;file name&gt;</c> on success and
    /// <c>error:&lt;code&gt;: &lt;message&gt;</c> (or <c>error:&lt;message&gt;</c> without a code) on failure.
    /// The UI formats both with the same helper.
    /// </summary>
    public static class ExportResultFormat
    {
        public static string Succeeded(string fileName) => "ok:" + (fileName ?? string.Empty);

        public static string Failed(string code, string message)
        {
            var text = string.IsNullOrWhiteSpace(message) ? "unknown error" : message.Trim();
            return string.IsNullOrWhiteSpace(code) ? "error:" + text : "error:" + code + ": " + text;
        }
    }
}
