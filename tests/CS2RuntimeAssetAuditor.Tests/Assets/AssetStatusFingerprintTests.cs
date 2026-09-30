using System;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Assets.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    public sealed class AssetStatusFingerprintTests
    {
        private static readonly object AuditSystem = new object();
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        private static readonly ScanTelemetrySnapshot Final = new ScanTelemetrySnapshot(CapturedAt, CapturedAt, 10, 2, 2, 5d, 3d, 3d);

        private static AssetStatusFingerprint Take(
            object? auditSystem = null,
            string? diagnosticCode = "code",
            DateTimeOffset? catalogCapturedAt = null,
            int unresolved = 1,
            ScanTelemetrySnapshot? finalTelemetry = null,
            bool liveTelemetry = false,
            string workStatus = "Idle",
            string? sessionId = "session",
            long occurrences = 3,
            ScanState state = ScanState.Completed)
        {
            return new AssetStatusFingerprint(
                auditSystem ?? AuditSystem,
                null,
                state,
                default,
                null,
                null,
                diagnosticCode,
                catalogCapturedAt ?? CapturedAt,
                unresolved,
                0,
                finalTelemetry ?? Final,
                liveTelemetry,
                workStatus,
                sessionId,
                occurrences);
        }

        [Test]
        public void Identical_inputs_match_so_the_snapshot_is_not_rebuilt()
        {
            Assert.That(Take().Matches(Take()), Is.True);
            Assert.That(Take(diagnosticCode: new string('c', 1) + "ode").Matches(Take()), Is.True, "strings compare by value");
        }

        [Test]
        public void A_fingerprint_never_taken_matches_nothing()
        {
            var never = default(AssetStatusFingerprint);
            Assert.That(Take().Matches(never), Is.False);
            Assert.That(never.Matches(Take()), Is.False);
            Assert.That(never.Matches(never), Is.False);
        }

        [Test]
        public void A_running_scan_telemetry_never_matches()
        {
            Assert.That(Take(liveTelemetry: true).Matches(Take(liveTelemetry: true)), Is.False);
            Assert.That(Take().Matches(Take(liveTelemetry: true)), Is.False);
        }

        [Test]
        public void Every_kind_of_input_change_is_detected()
        {
            var baseline = Take();
            Assert.That(Take(auditSystem: new object()).Matches(baseline), Is.False, "audit system reference");
            Assert.That(Take(diagnosticCode: "other").Matches(baseline), Is.False, "diagnostic code");
            Assert.That(Take(catalogCapturedAt: CapturedAt.AddSeconds(1)).Matches(baseline), Is.False, "catalog time");
            Assert.That(Take(unresolved: 2).Matches(baseline), Is.False, "unresolved count");
            Assert.That(Take(finalTelemetry: new ScanTelemetrySnapshot(CapturedAt, CapturedAt, 10, 2, 2, 5d, 3d, 3d)).Matches(baseline), Is.False, "telemetry compares by reference");
            Assert.That(Take(workStatus: "Running").Matches(baseline), Is.False, "work status");
            Assert.That(Take(sessionId: "next").Matches(baseline), Is.False, "session");
            Assert.That(Take(occurrences: 4).Matches(baseline), Is.False, "diagnostic occurrences");
            Assert.That(Take(state: ScanState.Running).Matches(baseline), Is.False, "scan state");
        }
    }
}
