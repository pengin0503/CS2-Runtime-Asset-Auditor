using CS2RuntimeAssetAuditor.Coordination;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class DiagnosticSessionContextTests
{
    [Test]
    public void New_world_creates_a_distinct_serializable_session_identity()
    {
        var started = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var first = DiagnosticSessionContext.Create("1.6.2f1", "build-1", started);
        var second = DiagnosticSessionContext.Create("1.6.2f1", "build-1", started);

        Assert.Multiple(() =>
        {
            Assert.That(Guid.TryParse(first.SessionId, out _), Is.True);
            Assert.That(second.SessionId, Is.Not.EqualTo(first.SessionId));
            Assert.That(first.StartedAtUtc, Is.EqualTo(started));
            Assert.That(first.GameVersion, Is.EqualTo("1.6.2f1"));
            Assert.That(first.BuildIdentity, Is.EqualTo("build-1"));
            Assert.That(typeof(DiagnosticSessionContext).Assembly.GetReferencedAssemblies()
                .Select(x => x.Name), Does.Not.Contain("Unity.Entities"));
        });
    }
}
