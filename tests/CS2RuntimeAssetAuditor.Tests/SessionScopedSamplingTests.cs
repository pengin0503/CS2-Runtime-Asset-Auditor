using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #38: runtime samples of one city must not appear as the latest value or the capture prebuffer of the next.
public class SessionScopedSamplingTests
{
    private static GlobalMetricsSnapshot Sample(double t)
        => new(t, 4, 4, new Dictionary<string, RecorderReading>());

    [Test]
    public void Latest_and_prebuffer_are_empty_for_another_city_session()
    {
        var samples = new SessionScopedSnapshots(capacity: 20);
        for (var t = 0; t <= 4; t++)
            samples.Add(generation: 1, Sample(t));

        Assert.That(samples.Latest(1)!.TimestampSeconds, Is.EqualTo(4));
        Assert.That(samples.Recent(1, 5).Count, Is.EqualTo(5));

        // Menu (generation 2) and the next city (generation 3) see nothing from city A.
        Assert.That(samples.Latest(2), Is.Null);
        Assert.That(samples.Recent(2, 5), Is.Empty);
        Assert.That(samples.Latest(3), Is.Null);
        Assert.That(samples.Recent(3, 5), Is.Empty);
    }

    [Test]
    public void First_sample_of_a_new_city_discards_the_previous_city_history()
    {
        var samples = new SessionScopedSnapshots(capacity: 20);
        samples.Add(generation: 1, Sample(10));
        samples.Add(generation: 1, Sample(11));

        // City B starts within the prebuffer window of city A's last samples.
        samples.Add(generation: 3, Sample(12));

        Assert.That(samples.Recent(3, 5).Select(s => s.TimestampSeconds), Is.EqualTo(new[] { 12d }));
        Assert.That(samples.Latest(1), Is.Null);
    }

    [Test]
    public void Clear_forgets_samples_of_the_same_session()
    {
        var samples = new SessionScopedSnapshots(capacity: 4);
        samples.Add(generation: 1, Sample(1));
        samples.Clear();

        Assert.That(samples.Latest(1), Is.Null);
        Assert.That(samples.Recent(1, 5), Is.Empty);

        samples.Add(generation: 1, Sample(2));
        Assert.That(samples.Recent(1, 5).Select(s => s.TimestampSeconds), Is.EqualTo(new[] { 2d }));
    }

    private sealed class ActionList
    {
        public readonly List<int> m_Items = new();
        public int m_NextIndex;
    }

    private sealed class QueueSystem
    {
        private readonly ActionList m_PathfindActions = new();

        public void SetPending(int count)
        {
            m_PathfindActions.m_Items.Clear();
            for (var i = 0; i < count; i++) m_PathfindActions.m_Items.Add(i);
        }
    }

    [Test]
    public void Pathfinding_reset_drops_the_latest_sample_and_rate_baselines()
    {
        var queue = new QueueSystem();
        var stats = new List<PathfindQueryStat>
        {
            new("Game.Simulation.ResidentAISystem", "Pathfind", "None", "None", 100, 90, 1000)
        };
        var collector = new PathfindingCollector(queue, () => stats, () => 7);

        queue.SetPending(10);
        collector.Sample(1);
        Assert.That(collector.Latest.Metrics, Is.Not.Empty);

        collector.Reset();
        Assert.That(collector.Latest.Metrics, Is.Empty);

        // The new city's first sample must not difference against the old city's queue or query counters.
        queue.SetPending(50);
        stats[0] = new PathfindQueryStat("Game.Simulation.ResidentAISystem", "Pathfind", "None", "None", 5, 5, 10);
        collector.Sample(2);
        Assert.That(collector.Latest.Get("queueDeltaPerSecond").Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(collector.Latest.Get("resultsPerSecond").Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(collector.Latest.Get("resultsPerSecond").Reason, Is.EqualTo(PathfindQueryStatsTracker.NeedsPriorSample));
    }

    private sealed class CountSource : IEntityDomainCountSource
    {
        public int Count;
        public bool TryGetCount(string componentTypeName, out int count, out string reason)
        {
            count = Count;
            reason = null!;
            return true;
        }
    }

    [Test]
    public void Entity_reset_drops_counts_and_samples_the_new_city_at_once()
    {
        var source = new CountSource { Count = 1000 };
        var collector = new EntityMetricsCollector(source, samplePeriodSeconds: 2);
        collector.Sample(0);
        Assert.That(collector.Latest.Get("citizens").Value, Is.EqualTo(1000));

        collector.Reset();
        Assert.That(collector.Latest.Metrics, Is.Empty);

        source.Count = 5;
        collector.Sample(0.5);
        Assert.That(collector.Latest.Get("citizens").Value, Is.EqualTo(5));
    }
}
