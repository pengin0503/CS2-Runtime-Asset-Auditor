using System;

namespace CS2RuntimeAssetAuditor.Coordination
{
    public sealed class DiagnosticSessionContext
    {
        public string SessionId { get; }
        public DateTimeOffset StartedAtUtc { get; }
        public string GameVersion { get; }
        public string BuildIdentity { get; }

        private DiagnosticSessionContext(string id, string gameVersion, string buildIdentity, DateTimeOffset startedAtUtc)
        {
            SessionId = id;
            GameVersion = gameVersion;
            BuildIdentity = buildIdentity;
            StartedAtUtc = startedAtUtc.ToUniversalTime();
        }

        public static DiagnosticSessionContext Create(string gameVersion, string buildIdentity, DateTimeOffset startedAtUtc)
        {
            return new DiagnosticSessionContext(Guid.NewGuid().ToString("D"), gameVersion ?? "unknown", buildIdentity ?? "unknown", startedAtUtc);
        }
    }
}
