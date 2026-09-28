using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class ResourceReference
    {
        public ResourceReference(string resourceId, long payloadBytes)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource ID is required.", nameof(resourceId));
            if (payloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(payloadBytes));
            ResourceId = resourceId; PayloadBytes = payloadBytes;
        }
        public string ResourceId { get; }
        public long PayloadBytes { get; }
    }

    public sealed class ResourceAggregationResult
    {
        public ResourceAggregationResult(int referenceCount, int uniqueResourceCount, long referencedPayloadBytes, long uniquePayloadBytes)
        {
            ReferenceCount = referenceCount; UniqueResourceCount = uniqueResourceCount; ReferencedPayloadBytes = referencedPayloadBytes; UniquePayloadBytes = uniquePayloadBytes;
        }
        public int ReferenceCount { get; }
        public int UniqueResourceCount { get; }
        public long ReferencedPayloadBytes { get; }
        public long UniquePayloadBytes { get; }
    }

    public static class ResourceAggregation
    {
        public static ResourceAggregationResult Aggregate(IEnumerable<ResourceReference> references)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var items = references.ToArray();
            long referenced = 0;
            foreach (var item in items) referenced = checked(referenced + item.PayloadBytes);
            long unique = 0;
            foreach (var group in items.GroupBy(i => i.ResourceId, StringComparer.Ordinal))
            {
                var payloads = group.Select(i => i.PayloadBytes).Distinct().ToArray();
                if (payloads.Length != 1) throw new InvalidOperationException("One stable resource ID was observed with conflicting payload estimates.");
                unique = checked(unique + payloads[0]);
            }
            return new ResourceAggregationResult(items.Length, items.Select(i => i.ResourceId).Distinct(StringComparer.Ordinal).Count(), referenced, unique);
        }
    }
}
