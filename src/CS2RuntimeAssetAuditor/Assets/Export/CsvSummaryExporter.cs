using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public sealed class CsvSummaryExporter
    {
        public string Export(AuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var catalog = report.Catalog.ToDictionary(item => RowKey(item.PrefabType, item.PrefabId), StringComparer.Ordinal);
            var census = report.Census.ToDictionary(entry => RowKey(entry.PrefabType, entry.PrefabId), StringComparer.Ordinal);

            // Census-only and findings-only reports carry no catalog section, so rows come from every section
            // that identifies a Prefab rather than from the catalog alone.
            var rows = new SortedSet<(string Type, string Id)>(Comparer<(string Type, string Id)>.Create((left, right) =>
            {
                var byType = StringComparer.Ordinal.Compare(left.Type, right.Type);
                return byType != 0 ? byType : StringComparer.Ordinal.Compare(left.Id, right.Id);
            }));
            foreach (var prefab in report.Catalog) rows.Add((prefab.PrefabType, prefab.PrefabId));
            foreach (var entry in report.Census) rows.Add((entry.PrefabType, entry.PrefabId));
            foreach (var finding in report.Analysis.Findings)
                if (finding.PrefabId != null && finding.PrefabType != null)
                    rows.Add((finding.PrefabType, finding.PrefabId));

            var builder = new StringBuilder();
            builder.AppendLine("prefabId,prefabType,displayName,traits,presence,topLevelObjects,findingCount");
            foreach (var (type, id) in rows)
            {
                var key = RowKey(type, id);
                catalog.TryGetValue(key, out var prefab);
                census.TryGetValue(key, out var entry);
                var findingCount = report.Analysis.Findings.Count(finding => BelongsTo(finding, type, id));
                builder.Append(EscapeText(id)).Append(',')
                    .Append(EscapeText(type)).Append(',')
                    .Append(EscapeText(prefab?.DisplayName ?? string.Empty)).Append(',')
                    .Append(EscapeText(prefab?.Traits ?? string.Empty)).Append(',')
                    .Append(EscapeText(entry?.Presence ?? string.Empty)).Append(',')
                    .Append(entry?.Counters?.TopLevelObjects?.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                    .Append(findingCount.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }
            return builder.ToString();
        }

        private static bool BelongsTo(ReportFinding finding, string prefabType, string prefabId)
        {
            if (finding.PrefabId != null && finding.PrefabType != null)
                return StringComparer.Ordinal.Equals(finding.PrefabId, prefabId) && StringComparer.Ordinal.Equals(finding.PrefabType, prefabType);
            // Findings supplied without an owning analysis entry can only be attributed through their evidence.
            return finding.Evidence.Any(evidence => StringComparer.Ordinal.Equals(evidence, "asset=" + prefabId));
        }

        private static string RowKey(string prefabType, string prefabId) => prefabType + "\n" + prefabId;

        // Text cells come from Prefab and asset names, which mods control. A leading =, +, -, @, tab, or carriage
        // return would be evaluated as a formula by spreadsheet applications, so such cells are prefixed with '.
        private static string EscapeText(string value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0)
                value = "'" + value;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
