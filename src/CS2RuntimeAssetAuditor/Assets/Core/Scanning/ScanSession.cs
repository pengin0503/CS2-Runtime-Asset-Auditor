using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    public sealed class ScanSession
    {
        private static readonly ScanStage[] CensusStages =
        {
            ScanStage.Preparing,
            ScanStage.CapturingCatalog,
            ScanStage.ProcessingCatalog,
            ScanStage.CapturingObjectCensus,
            ScanStage.ReducingObjectCensus,
            ScanStage.CapturingNetworkCensus,
            ScanStage.ReducingNetworkCensus,
            ScanStage.Finalizing
        };

        private static readonly ScanStage[] AssetAuditStages =
        {
            ScanStage.Preparing,
            ScanStage.ResolvingRenderGraph,
            ScanStage.CollectingGeometry,
            ScanStage.CollectingSurfaceTexture,
            ScanStage.EvaluatingFindings,
            ScanStage.Finalizing
        };

        private static readonly ScanStage[] DeepInspectionStages =
        {
            ScanStage.Preparing,
            ScanStage.DeepInspecting,
            ScanStage.Finalizing
        };

        private readonly ScanStage[] _stages;

        private ScanSession(ScanKind kind, long worldGeneration, DateTimeOffset startedAt)
        {
            Kind = kind;
            WorldGeneration = worldGeneration;
            StartedAt = startedAt;
            _stages = GetStages(kind);
            State = ScanState.Running;
            Stage = _stages[0];
            Progress = CreateIndeterminateProgress(Stage);
        }

        public ScanKind Kind { get; }
        public long WorldGeneration { get; }
        public DateTimeOffset StartedAt { get; }
        public ScanState State { get; private set; }
        public ScanStage Stage { get; private set; }
        public ScanProgress Progress { get; private set; }
        public string? DiagnosticCode { get; private set; }
        public bool CanPublish => State == ScanState.Running && Stage == ScanStage.Finalizing;

        public static ScanSession Start(ScanKind kind, long worldGeneration, DateTimeOffset startedAt)
        {
            if (!Enum.IsDefined(typeof(ScanKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            return new ScanSession(kind, worldGeneration, startedAt);
        }

        public void TransitionTo(ScanStage nextStage)
        {
            EnsureRunning();
            var index = IndexOfStage(Stage);
            if (index < 0 || index + 1 >= _stages.Length || _stages[index + 1] != nextStage)
                throw new InvalidOperationException("Scan stages must advance in the sequence defined for the active scan kind; use Complete after Finalizing.");

            Stage = nextStage;
            Progress = CreateIndeterminateProgress(Stage);
        }

        public void ReportProgress(long? completedItems, long? totalItems)
        {
            EnsureRunning();
            Progress = new ScanProgress(Stage, GetStageNumber(Stage), _stages.Length, completedItems, totalItems);
        }

        public void RequestCancellation()
        {
            if (State == ScanState.Running)
                State = ScanState.CancellationRequested;
        }

        public void MarkCancelled()
        {
            if (State != ScanState.CancellationRequested)
                throw new InvalidOperationException("Cancellation can complete only after a cancellation request.");
            State = ScanState.Cancelled;
        }

        public void Fail(string diagnosticCode)
        {
            if (State != ScanState.Running && State != ScanState.CancellationRequested)
                throw new InvalidOperationException("Only an active scan can fail.");
            if (string.IsNullOrWhiteSpace(diagnosticCode))
                throw new ArgumentException("A stable diagnostic code is required.", nameof(diagnosticCode));
            DiagnosticCode = diagnosticCode;
            State = ScanState.Failed;
        }

        public void Complete()
        {
            if (!CanPublish)
                throw new InvalidOperationException("A scan can complete only after successful Finalizing.");
            State = ScanState.Completed;
            Stage = ScanStage.Completed;
            Progress = new ScanProgress(ScanStage.Completed, _stages.Length, _stages.Length, 1, 1);
        }

        private void EnsureRunning()
        {
            if (State != ScanState.Running)
                throw new InvalidOperationException("The scan is not accepting work or progress updates.");
        }

        private int GetStageNumber(ScanStage stage)
        {
            if (stage == ScanStage.Completed)
                return _stages.Length;
            var index = IndexOfStage(stage);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(stage));
            return index + 1;
        }

        private int IndexOfStage(ScanStage stage)
        {
            for (var index = 0; index < _stages.Length; index++)
                if (_stages[index] == stage)
                    return index;
            return -1;
        }

        private ScanProgress CreateIndeterminateProgress(ScanStage stage)
        {
            return new ScanProgress(stage, GetStageNumber(stage), _stages.Length, null, null);
        }

        private static ScanStage[] GetStages(ScanKind kind)
        {
            switch (kind)
            {
                case ScanKind.Census: return CensusStages;
                case ScanKind.AssetAudit: return AssetAuditStages;
                case ScanKind.DeepInspection: return DeepInspectionStages;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
