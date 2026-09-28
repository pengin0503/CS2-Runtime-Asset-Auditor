using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class SimulationEfficiencyTests
{
    [TestCase(0, 0, 0)]
    [TestCase(-1, 1, 0)]
    [TestCase(4, 2.5, 0.625)]
    [TestCase(1, 1.2, 1.2)]
    public void Efficiency_is_safe_for_paused_and_running_simulation(double selected, double actual, double expected)
    {
        Assert.That(SimulationEfficiency.Calculate(selected, actual), Is.EqualTo(expected).Within(0.0001));
    }

    [Test]
    public void Negative_actual_speed_is_clamped_to_zero()
    {
        Assert.That(SimulationEfficiency.Calculate(4, -1), Is.EqualTo(0));
    }
}
