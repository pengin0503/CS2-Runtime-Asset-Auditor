using CS2RuntimeAssetAuditor.Attribution;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class AttributionTests
{
    [TestCase("Game", SystemSourceKind.Vanilla)]
    [TestCase("Unity.Entities", SystemSourceKind.Runtime)]
    [TestCase("Colossal.Core", SystemSourceKind.Runtime)]
    [TestCase("System.Private.CoreLib", SystemSourceKind.Runtime)]
    [TestCase("TourismOverhaul", SystemSourceKind.Mod)]
    public void Assembly_classification_is_deterministic(string assemblyName, SystemSourceKind expected)
    {
        Assert.That(AssemblyAttributor.ClassifyName(assemblyName), Is.EqualTo(expected));
    }

    [Test]
    public void Patch_owners_do_not_reassign_vanilla_system_ownership()
    {
        var descriptor = new SystemDescriptor(
            fullTypeName: "Game.Simulation.TrafficLightSystem",
            assemblyName: "Game",
            sourceKind: SystemSourceKind.Vanilla,
            modName: null,
            confidence: MetricConfidence.Full,
            patchOwners: new[] { new PatchOwnerInfo("TrafficLightsEnhancement", "Traffic Lights Enhancement") });

        Assert.That(descriptor.SourceKind, Is.EqualTo(SystemSourceKind.Vanilla));
        Assert.That(descriptor.PatchOwners.Single().OwnerId, Is.EqualTo("TrafficLightsEnhancement"));
    }
}
