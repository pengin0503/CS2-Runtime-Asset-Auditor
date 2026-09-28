using System;
using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DiagnosticEvidenceBridgeTests
{
    private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [TestCase(-10, -1, EvidenceRelativeTiming.Before)]
    [TestCase(-1, 5, EvidenceRelativeTiming.Overlapping)]
    [TestCase(11, 15, EvidenceRelativeTiming.After)]
    public void Links_only_temporal_same_session_evidence(int assetStart, int assetEnd, EvidenceRelativeTiming expected)
    {
        var link = DiagnosticEvidenceBridge.TryLink("same", "capture", Epoch, Epoch.AddSeconds(10),
            "same", "asset", Epoch.AddSeconds(assetStart), Epoch.AddSeconds(assetEnd));
        Assert.That(link?.RelativeTiming, Is.EqualTo(expected));
    }

    [Test]
    public void Different_session_or_unknown_interval_has_no_link()
    {
        Assert.That(DiagnosticEvidenceBridge.TryLink("one", "capture", Epoch, Epoch.AddSeconds(1), "two", "asset", Epoch, Epoch.AddSeconds(1)), Is.Null);
        Assert.That(DiagnosticEvidenceBridge.TryLink("one", "capture", null, Epoch, "one", "asset", Epoch, Epoch.AddSeconds(1)), Is.Null);
        Assert.That(DiagnosticEvidenceBridge.TryLink("one", "capture", Epoch, Epoch.AddSeconds(1), "one", "asset", null, Epoch), Is.Null);
    }
}
