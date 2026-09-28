using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public sealed class ReportCompatibilityResult
    {
        public ReportCompatibilityResult(bool isCompatible, IEnumerable<string> warnings)
        {
            IsCompatible = isCompatible;
            Warnings = new ReadOnlyCollection<string>(new List<string>(warnings ?? throw new ArgumentNullException(nameof(warnings))));
        }
        public bool IsCompatible { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    public static class ReportCompatibility
    {
        public static ReportCompatibilityResult Compare(AuditReport left, AuditReport right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            var warnings = new List<string>();
            if (!StringComparer.Ordinal.Equals(left.SchemaVersion, right.SchemaVersion)) warnings.Add("schema-version-mismatch");
            if (!StringComparer.Ordinal.Equals(left.QueryProfileVersion ?? string.Empty, right.QueryProfileVersion ?? string.Empty)) warnings.Add("query-profile-mismatch");
            if (MaterialScanOptionsDiffer(left.ScanOptions, right.ScanOptions)) warnings.Add("scan-options-mismatch");
            return new ReportCompatibilityResult(warnings.Count == 0, warnings);
        }

        private static bool MaterialScanOptionsDiffer(ReportScanOptions left, ReportScanOptions right)
        {
            if (left.WasCensusScanned != right.WasCensusScanned) return true;
            if (!left.WasCensusScanned && !right.WasCensusScanned) return false;
            return left.CollectSubordinateObjects != right.CollectSubordinateObjects || left.CollectNetworkEdges != right.CollectNetworkEdges;
        }
    }
}
