using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class MonitoringLifecycleGateTests
{
    [Test]
    public void Observe_reports_only_enable_disable_edges()
    {
        var gate = new MonitoringLifecycleGate(initiallyEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(gate.Observe(true), Is.EqualTo(MonitoringTransition.None));
            Assert.That(gate.Observe(false), Is.EqualTo(MonitoringTransition.Disabled));
            Assert.That(gate.Observe(false), Is.EqualTo(MonitoringTransition.None));
            Assert.That(gate.Observe(true), Is.EqualTo(MonitoringTransition.Enabled));
            Assert.That(gate.Observe(true), Is.EqualTo(MonitoringTransition.None));
        });
    }
}
