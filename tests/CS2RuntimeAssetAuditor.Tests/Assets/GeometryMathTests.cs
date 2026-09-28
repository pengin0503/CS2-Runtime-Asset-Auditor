using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class GeometryMathTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 6, 50, 0, TimeSpan.Zero);

        [Test]
        public void Triangle_topology_with_300_indices_reports_100_triangles()
        {
            var observation = SubMeshObservation.Create(0, 0, "Triangles", 300, 180, BoundsObservation.Empty, CapturedAt);
            Assert.That(observation.TriangleCount.Availability, Is.EqualTo(Availability.Available));
            Assert.That(observation.TriangleCount.Value, Is.EqualTo(100));
            Assert.That(observation.TriangleCount.Origin, Is.EqualTo(ObservationOrigin.Derived));
        }

        [Test]
        public void Line_topology_does_not_invent_triangle_count()
        {
            var observation = SubMeshObservation.Create(0, 0, "Lines", 300, 180, BoundsObservation.Empty, CapturedAt);
            Assert.That(observation.TriangleCount.Availability, Is.EqualTo(Availability.NotApplicable));
            Assert.That(observation.TriangleCount.HasValue, Is.False);
        }

        [Test]
        public void Missing_geometry_metadata_is_failed_not_zero()
        {
            var observation = GeometryObservation.Unavailable("geometry:missing", Availability.Failed, "APA-GEO-001", CapturedAt);
            Assert.That(observation.MeshCount.Availability, Is.EqualTo(Availability.Failed));
            Assert.That(observation.MeshCount.HasValue, Is.False);
            Assert.That(observation.TotalVertexCount.HasValue, Is.False);
            Assert.That(observation.TotalIndexCount.HasValue, Is.False);
        }

        [Test]
        public void Generation_cache_reads_shared_geometry_once_per_generation()
        {
            var calls = 0;
            var cache = new GenerationCache<string, int>();
            var first = cache.GetOrAdd(7, "geometry:shared", () => { calls++; return 10; });
            var second = cache.GetOrAdd(7, "geometry:shared", () => { calls++; return 20; });
            var nextGeneration = cache.GetOrAdd(8, "geometry:shared", () => { calls++; return 30; });

            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(10));
            Assert.That(nextGeneration, Is.EqualTo(30));
            Assert.That(calls, Is.EqualTo(2));
        }
    }
}
