using System;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    /// <summary>
    /// The inputs of the asset panel's snapshot that are not covered by the asset page, taken every frame to decide
    /// whether the snapshot must be rebuilt. Objects compare by reference and values by value; it allocates nothing.
    /// </summary>
    internal readonly struct AssetStatusFingerprint
    {
        private readonly bool _taken;
        private readonly object? _auditSystem;
        private readonly ScanSession? _scan;
        private readonly ScanState _scanState;
        private readonly ScanStage _scanStage;
        private readonly ScanProgress? _scanProgress;
        private readonly CapabilityReport? _capabilities;
        private readonly string? _lastDiagnosticCode;
        private readonly DateTimeOffset _catalogCapturedAt;
        private readonly int _catalogUnresolvedEntityCount;
        private readonly int _unmatchedPrefabReferenceCount;
        private readonly ScanTelemetrySnapshot? _finalTelemetry;
        private readonly bool _liveTelemetry;
        private readonly string? _diagnosticWorkStatus;
        private readonly string? _sessionId;
        private readonly long _diagnosticOccurrences;

        public AssetStatusFingerprint(
            object? auditSystem,
            ScanSession? scan,
            ScanState scanState,
            ScanStage scanStage,
            ScanProgress? scanProgress,
            CapabilityReport? capabilities,
            string? lastDiagnosticCode,
            DateTimeOffset catalogCapturedAt,
            int catalogUnresolvedEntityCount,
            int unmatchedPrefabReferenceCount,
            ScanTelemetrySnapshot? finalTelemetry,
            bool liveTelemetry,
            string? diagnosticWorkStatus,
            string? sessionId,
            long diagnosticOccurrences)
        {
            _taken = true;
            _auditSystem = auditSystem;
            _scan = scan;
            _scanState = scanState;
            _scanStage = scanStage;
            _scanProgress = scanProgress;
            _capabilities = capabilities;
            _lastDiagnosticCode = lastDiagnosticCode;
            _catalogCapturedAt = catalogCapturedAt;
            _catalogUnresolvedEntityCount = catalogUnresolvedEntityCount;
            _unmatchedPrefabReferenceCount = unmatchedPrefabReferenceCount;
            _finalTelemetry = finalTelemetry;
            _liveTelemetry = liveTelemetry;
            _diagnosticWorkStatus = diagnosticWorkStatus;
            _sessionId = sessionId;
            _diagnosticOccurrences = diagnosticOccurrences;
        }

        /// <summary>
        /// False against a fingerprint that was never taken, and while either side has a running scan's telemetry,
        /// which changes with every slice.
        /// </summary>
        public bool Matches(in AssetStatusFingerprint other)
        {
            return _taken && other._taken
                && !_liveTelemetry && !other._liveTelemetry
                && ReferenceEquals(_auditSystem, other._auditSystem)
                && ReferenceEquals(_scan, other._scan)
                && _scanState == other._scanState
                && _scanStage == other._scanStage
                && ReferenceEquals(_scanProgress, other._scanProgress)
                && ReferenceEquals(_capabilities, other._capabilities)
                && string.Equals(_lastDiagnosticCode, other._lastDiagnosticCode, StringComparison.Ordinal)
                && _catalogCapturedAt == other._catalogCapturedAt
                && _catalogUnresolvedEntityCount == other._catalogUnresolvedEntityCount
                && _unmatchedPrefabReferenceCount == other._unmatchedPrefabReferenceCount
                && ReferenceEquals(_finalTelemetry, other._finalTelemetry)
                && string.Equals(_diagnosticWorkStatus, other._diagnosticWorkStatus, StringComparison.Ordinal)
                && string.Equals(_sessionId, other._sessionId, StringComparison.Ordinal)
                && _diagnosticOccurrences == other._diagnosticOccurrences;
        }
    }
}
