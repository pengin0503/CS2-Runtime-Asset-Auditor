using System;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class TextureFootprintTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 7, 10, 0, TimeSpan.Zero);

        [TestCase("R8G8B8A8_UNorm", 4, 4, 1, 1, 64L)]
        [TestCase("RGB_DXT1_UNorm", 4, 4, 1, 1, 8L)]
        [TestCase("RGBA_BC7_UNorm", 4, 4, 1, 1, 16L)]
        [TestCase("RGBA_DXT5_SRGB", 4, 4, 1, 1, 16L)]
        [TestCase("R_BC4_UNorm", 4, 4, 1, 1, 8L)]
        [TestCase("RG_BC5_UNorm", 4, 4, 1, 1, 16L)]
        public void Representative_formats_have_documented_logical_payload(string format, int width, int height, int depth, int mips, long expected)
        {
            var result = TextureFootprintEstimator.Estimate(width, height, depth, mips, format, CapturedAt);
            Assert.That(result.Value, Is.EqualTo(expected));
            Assert.That(result.Origin, Is.EqualTo(ObservationOrigin.Estimated));
        }

        [Test]
        public void Block_compression_rounds_up_partial_blocks()
        {
            Assert.That(TextureFootprintEstimator.Estimate(5, 5, 1, 1, "RGB_DXT1_UNorm", CapturedAt).Value, Is.EqualTo(32L));
        }

        [Test]
        public void Mip_chain_sums_each_level_without_claiming_gpu_residency()
        {
            Assert.That(TextureFootprintEstimator.Estimate(4, 4, 1, 3, "R8G8B8A8_UNorm", CapturedAt).Value, Is.EqualTo(84L));
            Assert.That(TextureFootprintEstimator.MetricName, Does.Contain("logical"));
            Assert.That(TextureFootprintEstimator.MetricName, Does.Not.Contain("VRAM").IgnoreCase);
        }

        [Test]
        public void Texture_array_layers_each_carry_a_full_mip_chain()
        {
            var single = TextureFootprintEstimator.Estimate(1024, 1024, 1, 11, "RGBA_BC7_UNorm", CapturedAt).Value;
            var eightLayers = TextureFootprintEstimator.Estimate(1024, 1024, 8, 11, "RGBA_BC7_UNorm", CapturedAt).Value;
            Assert.That(eightLayers, Is.EqualTo(single * 8));
        }

        [TestCase("BC7_UNorm")]
        [TestCase("BC1_RGB_UNorm")]
        public void Names_that_are_not_GraphicsFormat_members_stay_unsupported(string format)
        {
            Assert.That(TextureFootprintEstimator.Estimate(4, 4, 1, 1, format, CapturedAt).Availability, Is.EqualTo(Availability.Unsupported));
        }

        [Test]
        public void Unsupported_format_is_not_reported_as_zero()
        {
            var result = TextureFootprintEstimator.Estimate(1024, 1024, 1, 1, "Unknown_Custom_Format", CapturedAt);
            Assert.That(result.Availability, Is.EqualTo(Availability.Unsupported));
            Assert.That(result.HasValue, Is.False);
        }

        [Test]
        public void Shared_texture_keys_are_deduplicated_for_unique_payload()
        {
            var observations = new[]
            {
                TextureObservation.Available("texture:shared", 4, 4, 1, "R8G8B8A8_UNorm", "Tex2D", 1, "Bilinear", "Repeat", 1, 64, CapturedAt),
                TextureObservation.Available("texture:shared", 4, 4, 1, "R8G8B8A8_UNorm", "Tex2D", 1, "Bilinear", "Repeat", 1, 64, CapturedAt),
            };
            Assert.That(TextureObservation.Deduplicate(observations).Count, Is.EqualTo(1));
        }

        [Test]
        public void Three_references_to_one_texture_have_one_unique_payload()
        {
            var aggregation = ResourceAggregation.Aggregate(new[]
            {
                new ResourceReference("texture:shared", 64),
                new ResourceReference("texture:shared", 64),
                new ResourceReference("texture:shared", 64),
            });
            Assert.That(aggregation.ReferenceCount, Is.EqualTo(3));
            Assert.That(aggregation.UniqueResourceCount, Is.EqualTo(1));
            Assert.That(aggregation.ReferencedPayloadBytes, Is.EqualTo(192));
            Assert.That(aggregation.UniquePayloadBytes, Is.EqualTo(64));
        }
    }
}
