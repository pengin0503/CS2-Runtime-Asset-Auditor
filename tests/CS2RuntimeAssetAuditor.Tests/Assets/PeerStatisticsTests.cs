using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets
{
    [TestFixture]
    public sealed class PeerStatisticsTests
    {
        private static readonly DateTimeOffset CapturedAt = new DateTimeOffset(2026, 9, 27, 7, 0, 0, TimeSpan.Zero);

        [Test]
        public void Insufficient_peer_sample_is_unavailable()
        {
            var samples = Enumerable.Range(1, 4).Select(i => new PeerSample("asset-" + i, "Building", i * 10d));
            var result = PeerStatistics.Calculate(samples, "Building", CapturedAt);

            Assert.That(result.SampleCount, Is.EqualTo(4));
            Assert.That(result.Median.Availability, Is.EqualTo(Availability.NotApplicable));
            Assert.That(result.Median.HasValue, Is.False);
            Assert.That(result.P95.HasValue, Is.False);
        }

        [Test]
        public void Peer_population_is_same_category_and_stable_regardless_input_order()
        {
            var samples = new[]
            {
                new PeerSample("z", "Vehicle", 9999),
                new PeerSample("b5", "Building", 50),
                new PeerSample("b1", "Building", 10),
                new PeerSample("b4", "Building", 40),
                new PeerSample("b2", "Building", 20),
                new PeerSample("b3", "Building", 30),
            };

            var forward = PeerStatistics.Calculate(samples, "Building", CapturedAt);
            var reverse = PeerStatistics.Calculate(samples.Reverse(), "Building", CapturedAt);

            Assert.That(forward.SampleCount, Is.EqualTo(5));
            Assert.That(forward.Median.Value, Is.EqualTo(30d));
            Assert.That(forward.P95.Value, Is.EqualTo(reverse.P95.Value));
            Assert.That(forward.MemberAssetIds, Is.EqualTo(new[] { "b1", "b2", "b3", "b4", "b5" }));
        }
    }
}
