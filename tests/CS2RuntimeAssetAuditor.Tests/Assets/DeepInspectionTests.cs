using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class DeepInspectionTests
    {
        [Test]
        public void Render_record_retains_only_copied_deep_inspection_values()
        {
            var capturedAt = new DateTimeOffset(2026, 9, 27, 7, 20, 0, TimeSpan.Zero);
            var observation = DeepInspectionObservation.Available(
                new[] { new MaterialBindingObservation("Material A", "Shader A", new[] { "KW_A" }, 2000, 2, true) },
                new[] { "surface-a" },
                capturedAt);
            var record = new RenderAssetRecord(new RenderAssetKey("render-a", "RenderPrefab"), "Render A")
                .WithDeepInspection(observation);

            Assert.That(record.DeepInspection, Is.SameAs(observation));
            Assert.That(record.DeepInspection!.Materials.Single().ShaderName, Is.EqualTo("Shader A"));
            Assert.That(record.DeepInspection.Availability, Is.EqualTo(Availability.Available));
            Assert.That(typeof(DeepInspectionObservation).GetProperties().Any(p => (p.PropertyType.FullName ?? string.Empty).StartsWith("UnityEngine.", StringComparison.Ordinal)), Is.False);
            Assert.That(typeof(MaterialBindingObservation).GetProperties().Any(p => (p.PropertyType.FullName ?? string.Empty).StartsWith("UnityEngine.", StringComparison.Ordinal)), Is.False);
        }
    }
}
