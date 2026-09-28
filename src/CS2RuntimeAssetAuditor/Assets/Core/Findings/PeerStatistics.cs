using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Findings
{
    public sealed class PeerSample
    {
        public PeerSample(string assetId, string category, double value)
        {
            if (string.IsNullOrWhiteSpace(assetId)) throw new ArgumentException("Asset ID is required.", nameof(assetId));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
            AssetId = assetId; Category = category; Value = value;
        }
        public string AssetId { get; }
        public string Category { get; }
        public double Value { get; }
    }

    public sealed class PeerStatisticsResult
    {
        public PeerStatisticsResult(int sampleCount, Observation<double> median, Observation<double> p95, IEnumerable<string> memberAssetIds)
        {
            SampleCount = sampleCount;
            Median = median;
            P95 = p95;
            MemberAssetIds = Array.AsReadOnly(memberAssetIds.ToArray());
        }
        public int SampleCount { get; }
        public Observation<double> Median { get; }
        public Observation<double> P95 { get; }
        public IReadOnlyList<string> MemberAssetIds { get; }
    }

    public static class PeerStatistics
    {
        public static PeerStatisticsResult Calculate(IEnumerable<PeerSample> samples, string category, DateTimeOffset capturedAt, int minimumSampleSize = RuleSetInfo.MinimumPeerSampleSize)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
            if (minimumSampleSize <= 0) throw new ArgumentOutOfRangeException(nameof(minimumSampleSize));
            var selected = samples.Where(s => string.Equals(s.Category, category, StringComparison.Ordinal))
                .OrderBy(s => s.AssetId, StringComparer.Ordinal).ToArray();
            if (selected.Length < minimumSampleSize)
            {
                var unavailable = Observation<double>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, capturedAt);
                return new PeerStatisticsResult(selected.Length, unavailable, unavailable, selected.Select(s => s.AssetId));
            }
            var values = selected.Select(s => s.Value).OrderBy(v => v).ToArray();
            var median = values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2d;
            var p95Index = Math.Max(0, Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * 0.95d) - 1));
            return new PeerStatisticsResult(selected.Length,
                Observation<double>.FromValue(median, ObservationOrigin.Derived, capturedAt),
                Observation<double>.FromValue(values[p95Index], ObservationOrigin.Derived, capturedAt),
                selected.Select(s => s.AssetId));
        }
    }
}
