namespace CS2RuntimeAssetAuditor.Coordination
{
    public sealed class DiagnosticWorkCoordinator
    {
        private readonly object _gate = new object();
        private bool _runtimeActive;
        private bool _assetActive;
        private bool _assetQueued;
        private bool _assetInterruptionRequested;

        public bool HasQueuedAssetWork
        {
            get { lock (_gate) return _assetQueued; }
        }

        public bool IsActive(DiagnosticWorkKind kind)
        {
            lock (_gate)
                return kind == DiagnosticWorkKind.RuntimeDeepCapture ? _runtimeActive : _assetActive;
        }

        public DiagnosticWorkDecision Request(DiagnosticWorkKind kind)
        {
            lock (_gate)
            {
                if (kind == DiagnosticWorkKind.RuntimeDeepCapture)
                {
                    if (_runtimeActive) return DiagnosticWorkDecision.AlreadyActive;
                    _runtimeActive = true;
                    _assetInterruptionRequested = _assetActive;
                    return DiagnosticWorkDecision.Started;
                }

                if (_assetActive) return DiagnosticWorkDecision.AlreadyActive;
                if (_runtimeActive)
                {
                    _assetQueued = true;
                    return DiagnosticWorkDecision.Queued;
                }

                _assetActive = true;
                _assetQueued = false;
                return DiagnosticWorkDecision.Started;
            }
        }

        public void Complete(DiagnosticWorkKind kind)
        {
            lock (_gate)
            {
                if (kind == DiagnosticWorkKind.RuntimeDeepCapture)
                    _runtimeActive = false;
                else
                {
                    // Completing asset work also withdraws a queued request whose owner gave it up.
                    _assetActive = false;
                    _assetQueued = false;
                    _assetInterruptionRequested = false;
                }
            }
        }

        public bool ConsumeAssetInterruptionRequest()
        {
            lock (_gate)
            {
                var requested = _assetInterruptionRequested;
                _assetInterruptionRequested = false;
                return requested;
            }
        }
    }
}
