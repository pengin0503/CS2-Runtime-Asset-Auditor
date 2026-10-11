using System;
using System.Globalization;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>File name stems of the saved reports; ReportFileWriter adds "-1", "-2" on a collision.</summary>
    public static class ReportFileNames
    {
        public const string ReportPrefix = "CS2RuntimeAssetAuditor-report-";
        public const string AssetCsvPrefix = "CS2RuntimeAssetAuditor-assets-";
        public const string TimestampFormat = "yyyy-MM-dd_HHmmss_fff";

        public static string ReportStem(DateTime localTime) => ReportPrefix + Format(localTime);

        public static string AssetCsvStem(DateTime localTime) => AssetCsvPrefix + Format(localTime);

        private static string Format(DateTime localTime) => localTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }
}
