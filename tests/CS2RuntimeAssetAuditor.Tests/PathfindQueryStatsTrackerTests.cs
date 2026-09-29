using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Collectors;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class PathfindQueryStatsTrackerTests
{
    private const string Residents = "Game.Simulation.ResidentAISystem";
    private const string Coverage = "Game.Simulation.ServiceCoverageSystem";

    private static PathfindQueryStat Stat(string system, string type, int queries, int successes, double traversal, string origin = "CurrentLocation") =>
        new PathfindQueryStat(system, type, origin, "Home", queries, successes, traversal);

    private static Dictionary<string, NamedMetricValue> ById(IEnumerable<NamedMetricValue> metrics) =>
        metrics.ToDictionary(metric => metric.Id);

    [Test]
    public void First_sample_has_no_rates()
    {
        var metrics = ById(new PathfindQueryStatsTracker().Sample(1, new[] { Stat(Residents, "Pathfind", 10, 9, 0.5) }));

        Assert.That(metrics["resultsPerSecond"].Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(metrics["resultsPerSecond"].Reason, Is.EqualTo(PathfindQueryStatsTracker.NeedsPriorSample));
    }

    [Test]
    public void Rates_come_from_the_totals_added_since_the_previous_sample()
    {
        var tracker = new PathfindQueryStatsTracker();
        tracker.Sample(10, new[]
        {
            Stat(Residents, "Pathfind", 100, 90, 5),
            Stat(Coverage, "Coverage", 4, 4, 2)
        });
        var metrics = ById(tracker.Sample(12, new[]
        {
            Stat(Residents, "Pathfind", 140, 120, 6),
            Stat(Residents, "Pathfind", 10, 10, 0.2, origin: "Transport"), // a new key counts from zero
            Stat(Coverage, "Coverage", 6, 6, 5)
        }));

        Assert.That(metrics["resultsPerSecond"].Value, Is.EqualTo((40 + 10 + 2) / 2d));
        Assert.That(metrics["pathfindResultsPerSecond"].Value, Is.EqualTo(25));
        Assert.That(metrics["coverageResultsPerSecond"].Value, Is.EqualTo(1));
        Assert.That(metrics["availabilityResultsPerSecond"].Value, Is.EqualTo(0));
        Assert.That(metrics["pathfindSuccessRatio"].Value, Is.EqualTo(40d / 50));
        Assert.That(metrics["pathfindSuccessRatio"].UnitType, Is.EqualTo(MetricUnits.Ratio));
        // Coverage explored 3 graphs, residents 1.2: the heavier requester is listed first.
        Assert.That(metrics["systemGraphTraversalPerSecond:" + Coverage].Value, Is.EqualTo(1.5).Within(1e-9));
        Assert.That(metrics["systemGraphTraversalPerSecond:" + Residents].Value, Is.EqualTo(0.6).Within(1e-9));
        Assert.That(metrics["systemResultsPerSecond:" + Residents].Value, Is.EqualTo(25));
    }

    [Test]
    public void A_reset_after_loading_a_save_discards_the_interval()
    {
        var tracker = new PathfindQueryStatsTracker();
        tracker.Sample(10, new[] { Stat(Residents, "Pathfind", 100, 90, 5) });

        var afterLoad = ById(tracker.Sample(12, new[] { Stat(Residents, "Pathfind", 3, 3, 0.1) }));
        Assert.That(afterLoad["resultsPerSecond"].Reason, Is.EqualTo(PathfindQueryStatsTracker.CountersWereReset));

        var emptied = ById(tracker.Sample(14, new PathfindQueryStat[0]));
        Assert.That(emptied["resultsPerSecond"].Reason, Is.EqualTo(PathfindQueryStatsTracker.CountersWereReset));

        var next = ById(tracker.Sample(16, new[] { Stat(Residents, "Pathfind", 4, 4, 0.1) }));
        Assert.That(next["resultsPerSecond"].Value, Is.EqualTo(2));
    }

    [Test]
    public void Only_the_heaviest_systems_are_listed()
    {
        var tracker = new PathfindQueryStatsTracker();
        var systems = Enumerable.Range(0, 8).Select(index => "System" + index).ToArray();
        tracker.Sample(0, systems.Select(system => Stat(system, "Pathfind", 0, 0, 0)).ToArray());
        var metrics = tracker.Sample(1, systems.Select((system, index) => Stat(system, "Pathfind", 1, 1, index)).ToArray());

        var listed = metrics.Where(metric => metric.Id.StartsWith("systemGraphTraversalPerSecond:")).Select(metric => metric.Id).ToArray();
        Assert.That(listed, Is.EqualTo(new[] { 7, 6, 5, 4, 3 }.Select(index => "systemGraphTraversalPerSecond:System" + index)));
    }

    private sealed class ThreeQueueSystem
    {
        private readonly Queue<int> m_HighPriorityTypes = new(new[] { 1 });
        private readonly Queue<int> m_ModificationTypes = new(new[] { 1, 2 });
        private readonly Queue<int> m_ActionTypes = new(new[] { 1, 2, 3, 4 });
    }

    [Test]
    public void Collector_reports_each_action_type_queue_and_their_total()
    {
        var collector = new PathfindingCollector(new ThreeQueueSystem(), () => new PathfindQueryStat[0], () => 17);
        collector.Sample(1);
        var metrics = collector.Latest.Metrics;

        Assert.That(metrics["highPriorityActionTypeQueue"].Value, Is.EqualTo(1));
        Assert.That(metrics["modificationActionTypeQueue"].Value, Is.EqualTo(2));
        Assert.That(metrics["actionTypeQueue"].Value, Is.EqualTo(4));
        Assert.That(metrics["totalActionTypeQueue"].Value, Is.EqualTo(7));
        Assert.That(metrics["pendingRequestCount"].Value, Is.EqualTo(17));
        Assert.That(metrics["requestsPerSecond"].Availability, Is.EqualTo(MetricAvailability.Unavailable));
    }

    [Test]
    public void Collector_without_the_result_system_leaves_rates_unavailable()
    {
        var collector = new PathfindingCollector(new ThreeQueueSystem());
        collector.Sample(1);
        collector.Sample(2);

        Assert.That(collector.Latest.Metrics["resultsPerSecond"].Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(collector.Latest.Metrics["pendingRequestCount"].Availability, Is.EqualTo(MetricAvailability.Unavailable));
    }
}
