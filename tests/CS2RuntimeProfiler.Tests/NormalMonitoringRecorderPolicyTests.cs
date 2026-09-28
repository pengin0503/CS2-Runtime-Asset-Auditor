using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class NormalMonitoringRecorderPolicyTests
{
    [Test]
    public void Preferred_recorders_include_profiler_used_memory_for_capture_guard_baseline()
    {
        var groups = NormalMonitoringRecorderPolicy.GetPreferredRecorderNameGroups();

        Assert.That(
            groups.SelectMany(group => group),
            Does.Contain("Profiler Used Memory"));
    }
}
