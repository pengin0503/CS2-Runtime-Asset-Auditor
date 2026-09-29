#nullable enable
using System;

namespace CS2RuntimeAssetAuditor.Coordination
{
    /// <summary>
    /// Owns the identity of the currently loaded gameplay city. The game reuses one ECS World across
    /// loads, so the session is driven by game-load events rather than by World identity.
    /// Every begin or close advances <see cref="Generation"/>; systems compare it with the value they last
    /// observed and discard evidence that belongs to an earlier city session.
    /// </summary>
    public sealed class DiagnosticSessionRegistry
    {
        private readonly object _gate = new object();
        private DiagnosticSessionContext? _current;
        private long _generation;

        public DiagnosticSessionContext? Current
        {
            get { lock (_gate) return _current; }
        }

        public long Generation
        {
            get { lock (_gate) return _generation; }
        }

        public bool IsActive => Current != null;

        public DiagnosticSessionContext Begin(string gameVersion, string buildIdentity, DateTimeOffset startedAtUtc)
        {
            var context = DiagnosticSessionContext.Create(gameVersion, buildIdentity, startedAtUtc);
            lock (_gate)
            {
                _current = context;
                _generation++;
            }
            return context;
        }

        public void Close()
        {
            lock (_gate)
            {
                if (_current == null)
                    return;
                _current = null;
                _generation++;
            }
        }

        public bool IsCurrent(string? sessionId)
        {
            var current = Current;
            return current != null && !string.IsNullOrWhiteSpace(sessionId)
                && string.Equals(current.SessionId, sessionId, StringComparison.Ordinal);
        }
    }
}
