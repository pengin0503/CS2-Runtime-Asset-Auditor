using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    public sealed class ScanProgress
    {
        public ScanProgress(
            ScanStage stage,
            int stageNumber,
            int totalStages,
            long? completedItems,
            long? totalItems)
        {
            if (totalStages <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalStages));
            if (stageNumber <= 0 || stageNumber > totalStages)
                throw new ArgumentOutOfRangeException(nameof(stageNumber));
            if (completedItems.HasValue != totalItems.HasValue)
                throw new ArgumentException("Exact progress requires both a completed and total item count.");
            if (totalItems.HasValue && (totalItems.Value <= 0 || completedItems!.Value < 0 || completedItems.Value > totalItems.Value))
                throw new ArgumentOutOfRangeException(nameof(totalItems), "Exact progress counts must satisfy 0 <= completed <= total and total > 0.");

            Stage = stage;
            StageNumber = stageNumber;
            TotalStages = totalStages;
            CompletedItems = completedItems;
            TotalItems = totalItems;
        }

        public ScanStage Stage { get; }

        public int StageNumber { get; }

        public int TotalStages { get; }

        public long? CompletedItems { get; }

        public long? TotalItems { get; }

        public bool IsIndeterminate => !TotalItems.HasValue;

        public double? PercentComplete => TotalItems.HasValue
            ? CompletedItems!.Value * 100.0 / TotalItems.Value
            : (double?)null;
    }
}
