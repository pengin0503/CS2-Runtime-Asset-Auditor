using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.Core.Findings
{
    /// <summary>Which assets form the reference population a Prefab is compared against.</summary>
    public enum ComparisonPopulation
    {
        /// <summary>All Prefabs of the same Prefab type.</summary>
        SameCategory,
        /// <summary>Built-in (vanilla or DLC) Prefabs of the same type.</summary>
        BuiltinDlc,
        /// <summary>Non-built-in (custom) Prefabs of the same type.</summary>
        Custom,
        /// <summary>Prefabs of the same type from the same asset pack, or the same asset-database source.</summary>
        SameSourcePack
    }

    /// <summary>
    /// Flags Prefabs whose LOD0 vertex count or estimated texture payload is far above comparable Prefabs.
    /// Comparison is always within one Prefab type; the configured population narrows the reference set.
    /// A result requires at least <see cref="RuleSetInfo.MinimumPeerSampleSize"/> other reference values, a value
    /// above the reference P95, and a value of at least <see cref="RuleSetInfo.PeerOutlierMedianMultiplier"/>
    /// times the reference median. It describes relative asset complexity, never measured runtime cost.
    /// </summary>
    public static class PeerOutlierEvaluator
    {
        public const string VertexRuleId = "APA-PEER-001";
        public const string TextureRuleId = "APA-PEER-002";

        public static IReadOnlyDictionary<PrefabKey, IReadOnlyList<Finding>> Evaluate(
            IEnumerable<PrefabAnalysisEntry> entries,
            IReadOnlyDictionary<PrefabKey, PrefabRecord> catalog,
            ComparisonPopulation population,
            DateTimeOffset capturedAt)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var candidates = entries
                .Where(entry => entry != null && catalog.ContainsKey(entry.Key))
                .Select(entry => new Candidate(entry, catalog[entry.Key]))
                .ToArray();

            var result = new Dictionary<PrefabKey, List<Finding>>();
            EvaluateMetric(candidates, population, capturedAt, VertexRuleId, "lod0Vertices", FindingCategory.Geometry,
                candidate => candidate.Entry.Lod0Vertices.HasValue ? candidate.Entry.Lod0Vertices.Value : (long?)null,
                "LOD0 vertex count far above comparable assets",
                "The LOD0 vertex count is far above comparable Prefabs in the selected population. This compares asset complexity only and does not measure frame or GPU cost.",
                result);
            EvaluateMetric(candidates, population, capturedAt, TextureRuleId, "estimatedTexturePayloadBytes", FindingCategory.Texture,
                candidate => candidate.Entry.EstimatedTexturePayload.HasValue ? candidate.Entry.EstimatedTexturePayload.Value : (long?)null,
                "Texture payload far above comparable assets",
                "The estimated logical texture payload is far above comparable Prefabs in the selected population. This is not measured VRAM residency.",
                result);
            return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Finding>)pair.Value.AsReadOnly());
        }

        private static void EvaluateMetric(
            IReadOnlyList<Candidate> candidates,
            ComparisonPopulation population,
            DateTimeOffset capturedAt,
            string ruleId,
            string metricName,
            FindingCategory category,
            Func<Candidate, long?> select,
            string title,
            string explanation,
            Dictionary<PrefabKey, List<Finding>> result)
        {
            // Each group's reference values are sorted once (O(n log n)). A candidate that belongs to its own
            // reference group is compared against the others only (leave-one-out); otherwise it would bound its own P95.
            var measured = candidates.Select(candidate => (Candidate: candidate, Value: select(candidate)))
                .Where(item => item.Value.HasValue && item.Value.Value >= 0)
                .ToArray();
            var groups = measured
                .Where(item => IsReferenceMember(item.Candidate, population))
                .SelectMany(item => GroupKeys(item.Candidate, population).Select(key => (Key: key, Value: (double)item.Value!.Value)))
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Value).OrderBy(v => v).ToArray(), StringComparer.Ordinal);

            foreach (var (candidate, value) in measured)
            {
                var groupKey = GroupKeys(candidate, population).FirstOrDefault();
                if (groupKey == null || !groups.TryGetValue(groupKey, out var sorted))
                    continue;
                var candidateValue = (double)value!.Value;
                var member = IsReferenceMember(candidate, population);
                var removed = member ? Array.BinarySearch(sorted, candidateValue) : -1;
                if (member && removed < 0)
                    continue;
                var count = member ? sorted.Length - 1 : sorted.Length;
                if (count < RuleSetInfo.MinimumPeerSampleSize)
                    continue;

                double At(int index) => member && index >= removed ? sorted[index + 1] : sorted[index];
                var median = count % 2 == 1 ? At(count / 2) : (At(count / 2 - 1) + At(count / 2)) / 2d;
                var p95 = At(Math.Max(0, Math.Min(count - 1, (int)Math.Ceiling(count * 0.95d) - 1)));
                if (median <= 0 || candidateValue <= p95 || candidateValue < median * RuleSetInfo.PeerOutlierMedianMultiplier)
                    continue;

                if (!result.TryGetValue(candidate.Entry.Key, out var list))
                    result[candidate.Entry.Key] = list = new List<Finding>();
                list.Add(new Finding(ruleId, FindingStatus.Notice, category, title, explanation, new[]
                {
                    "asset=" + candidate.Entry.Key.PrefabId,
                    metricName + "=" + value.Value.ToString(CultureInfo.InvariantCulture),
                    "peerMedian=" + median.ToString("0.##", CultureInfo.InvariantCulture),
                    "peerP95=" + p95.ToString("0.##", CultureInfo.InvariantCulture),
                    "peerSampleCount=" + count.ToString(CultureInfo.InvariantCulture),
                    "population=" + population
                }, FindingBasis.PeerComparison, RuleSetInfo.Version));
            }
        }

        private static bool IsReferenceMember(Candidate candidate, ComparisonPopulation population)
        {
            var origin = candidate.Record.OriginEvidence;
            switch (population)
            {
                case ComparisonPopulation.BuiltinDlc: return origin.IsBuiltin == true;
                case ComparisonPopulation.Custom: return origin.IsBuiltin == false;
                default: return true;
            }
        }

        private static IEnumerable<string> GroupKeys(Candidate candidate, ComparisonPopulation population)
        {
            var type = candidate.Entry.Key.PrefabType;
            if (population != ComparisonPopulation.SameSourcePack)
                return new[] { type };
            var origin = candidate.Record.OriginEvidence;
            var packs = (origin.AssetPackMembership ?? Array.Empty<string>())
                .OrderBy(pack => pack, StringComparer.Ordinal)
                .Select(pack => type + "\u001fpack\u001f" + pack)
                .ToList();
            if (packs.Count == 0 && !string.IsNullOrWhiteSpace(origin.AssetDatabaseSource))
                packs.Add(type + "\u001fsource\u001f" + origin.AssetDatabaseSource);
            // An asset without pack or source evidence has no same-pack population.
            return packs;
        }

        private sealed class Candidate
        {
            public Candidate(PrefabAnalysisEntry entry, PrefabRecord record)
            {
                Entry = entry;
                Record = record;
                Id = entry.Key.PrefabType + ":" + entry.Key.PrefabId;
            }

            public PrefabAnalysisEntry Entry { get; }
            public PrefabRecord Record { get; }
            public string Id { get; }
        }
    }
}
