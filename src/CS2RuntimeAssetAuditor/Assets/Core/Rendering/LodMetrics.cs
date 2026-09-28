using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public static class LodMetrics
    {
        public static Observation<double> RetentionPercent(long baseValue, long lowerLodValue, DateTimeOffset capturedAt)
        {
            Validate(baseValue, lowerLodValue);
            if (baseValue == 0)
                return Observation<double>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, capturedAt);
            return Observation<double>.FromValue(lowerLodValue * 100d / baseValue, ObservationOrigin.Derived, capturedAt);
        }

        public static Observation<double> ReductionPercent(long baseValue, long lowerLodValue, DateTimeOffset capturedAt)
        {
            var retention = RetentionPercent(baseValue, lowerLodValue, capturedAt);
            return retention.HasValue
                ? Observation<double>.FromValue(100d - retention.Value, ObservationOrigin.Derived, capturedAt)
                : Observation<double>.Unavailable(retention.Availability, ObservationOrigin.Derived, capturedAt);
        }

        private static void Validate(long baseValue, long lowerLodValue)
        {
            if (baseValue < 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (lowerLodValue < 0) throw new ArgumentOutOfRangeException(nameof(lowerLodValue));
        }
    }
}
