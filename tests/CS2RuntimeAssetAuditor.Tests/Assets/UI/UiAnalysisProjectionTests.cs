using System;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets.UI
{
    [TestFixture]
    public sealed class UiAnalysisProjectionTests
    {
        [Test]
        public void Deep_inspection_projection_copies_only_serializable_observations()
        {
            var capturedAt = new DateTimeOffset(2026, 9, 27, 9, 30, 0, TimeSpan.Zero);
            var observation = DeepInspectionObservation.Available(
                new[]
                {
                    new MaterialBindingObservation(
                        "House Material",
                        "Shader/Building",
                        new[] { "FOG_ON", "WIND_OFF" },
                        2000,
                        3,
                        true)
                },
                new[] { "Surface.House.A" },
                capturedAt);

            var projected = UiAnalysisProjection.MapDeepInspection(observation);

            Assert.That(projected.Availability, Is.EqualTo("Available"));
            Assert.That(projected.CapturedAt, Is.EqualTo(capturedAt.ToString("O")));
            Assert.That(projected.SurfaceAssetIds, Is.EqualTo(new[] { "Surface.House.A" }));
            Assert.That(projected.Materials, Has.Length.EqualTo(1));
            Assert.That(projected.Materials[0].MaterialName, Is.EqualTo("House Material"));
            Assert.That(projected.Materials[0].ShaderName, Is.EqualTo("Shader/Building"));
            Assert.That(projected.Materials[0].ShaderKeywords, Is.EqualTo(new[] { "FOG_ON", "WIND_OFF" }));
            Assert.That(projected.Materials[0].PassCount, Is.EqualTo(3));
            Assert.That(projected.Materials[0].EnableInstancing, Is.True);
        }
    }
}
