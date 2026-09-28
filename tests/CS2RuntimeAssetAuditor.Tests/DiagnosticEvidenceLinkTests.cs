using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DiagnosticEvidenceLinkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Different_city_sessions_cannot_be_linked()
    {
        var runtime = new DiagnosticEvidenceInterval("city-1", "capture-1", T0, T0.AddMinutes(1));
        var asset = new DiagnosticEvidenceInterval("city-2", "asset-1", T0, T0.AddMinutes(1));
        Assert.That(DiagnosticEvidenceLink.TryCreate(runtime, asset), Is.Null);
    }

    [TestCase(-2, -1, EvidenceRelativeTiming.Before)]
    [TestCase(0, 1, EvidenceRelativeTiming.Overlapping)]
    [TestCase(2, 3, EvidenceRelativeTiming.After)]
    public void Relative_timing_is_derived_only_from_the_intervals(int startMinutes, int endMinutes, EvidenceRelativeTiming expected)
    {
        var runtime = new DiagnosticEvidenceInterval("city-1", "capture-1", T0, T0.AddMinutes(1));
        var asset = new DiagnosticEvidenceInterval("city-1", "asset-1", T0.AddMinutes(startMinutes), T0.AddMinutes(endMinutes));
        var link = DiagnosticEvidenceLink.TryCreate(runtime, asset);
        Assert.That(link, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(link!.RelativeTiming, Is.EqualTo(expected));
            Assert.That(link.CaptureId, Is.EqualTo("capture-1"));
            Assert.That(link.AssetSnapshotId, Is.EqualTo("asset-1"));
        });
    }
}
