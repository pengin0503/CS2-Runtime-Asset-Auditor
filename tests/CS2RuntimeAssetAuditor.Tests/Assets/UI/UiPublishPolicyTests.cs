using System;
using CS2RuntimeAssetAuditor.Assets.UI;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets.UI
{
    [TestFixture]
    public sealed class UiPublishPolicyTests
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

        [Test]
        public void Idle_unchanged_state_is_not_rebuilt_however_long_it_has_been()
        {
            Assert.That(UiPublishPolicy.Decide(false, false, false, TimeSpan.FromMinutes(10), Interval), Is.EqualTo(UiPublishDecision.Skip));
        }

        [Test]
        public void Data_changes_publish_immediately_even_during_a_scan()
        {
            Assert.That(UiPublishPolicy.Decide(true, false, true, TimeSpan.Zero, Interval), Is.EqualTo(UiPublishDecision.Publish));
        }

        [Test]
        public void Status_changes_while_idle_publish_immediately()
        {
            Assert.That(UiPublishPolicy.Decide(false, true, false, TimeSpan.Zero, Interval), Is.EqualTo(UiPublishDecision.Publish));
        }

        [Test]
        public void Scan_progress_is_throttled_to_the_progress_interval()
        {
            Assert.That(UiPublishPolicy.Decide(false, true, true, TimeSpan.FromMilliseconds(50), Interval), Is.EqualTo(UiPublishDecision.Skip));
            Assert.That(UiPublishPolicy.Decide(false, true, true, Interval, Interval), Is.EqualTo(UiPublishDecision.Publish));
        }
    }
}
