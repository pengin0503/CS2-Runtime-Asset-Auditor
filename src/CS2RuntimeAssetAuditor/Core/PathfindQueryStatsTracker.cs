using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// One entry of <c>PathfindResultSystem.queryStats</c> (Game 1.6.2f1): cumulative totals for one requesting
    /// system, query type and origin/destination pair. The game adds to them for every processed result, whatever
    /// the debug settings, and clears them only when a save is loaded (<c>PreDeserialize</c>).
    /// </summary>
    public readonly struct PathfindQueryStat
    {
        public PathfindQueryStat(string system, string queryType, string originType, string destinationType,
            int queryCount, int successCount, double graphTraversal)
        {
            System = system ?? string.Empty;
            QueryType = queryType ?? string.Empty;
            OriginType = originType ?? string.Empty;
            DestinationType = destinationType ?? string.Empty;
            QueryCount = queryCount;
            SuccessCount = successCount;
            GraphTraversal = graphTraversal;
        }

        /// <summary>Full type name of the requesting system.</summary>
        public string System { get; }
        /// <summary>Pathfind, Coverage or Availability.</summary>
        public string QueryType { get; }
        public string OriginType { get; }
        public string DestinationType { get; }
        public int QueryCount { get; }
        /// <summary>Queries whose result was not empty.</summary>
        public int SuccessCount { get; }
        /// <summary>Sum over the queries of the share of the path graph each one explored.</summary>
        public double GraphTraversal { get; }

        internal string Key => System + "\n" + QueryType + "\n" + OriginType + "\n" + DestinationType;
    }

    /// <summary>
    /// Turns the game's cumulative pathfinding query totals into rates for the interval since the previous sample:
    /// completed queries per second overall and per query type, the pathfind success share, and the requesting
    /// systems whose queries explored the most of the path graph.
    /// </summary>
    public sealed class PathfindQueryStatsTracker
    {
        public const int TopSystemCount = 5;
        public const string NeedsPriorSample = "A prior query-statistics sample is required.";
        public const string CountersWereReset =
            "The game reset its query statistics (a save was loaded); rates resume at the next sample.";
        public const string NoPathfindQueries = "No pathfind query completed in this interval.";

        private Dictionary<string, PathfindQueryStat>? _previous;
        private double _previousTimestamp;

        public IReadOnlyList<NamedMetricValue> Sample(double timestampSeconds, IReadOnlyList<PathfindQueryStat> stats)
        {
            var current = new Dictionary<string, PathfindQueryStat>(StringComparer.Ordinal);
            foreach (var stat in stats ?? Array.Empty<PathfindQueryStat>())
                current[stat.Key] = stat;

            var previous = _previous;
            var previousTimestamp = _previousTimestamp;
            _previous = current;
            _previousTimestamp = timestampSeconds;

            if (previous == null || timestampSeconds <= previousTimestamp)
                return Unavailable(NeedsPriorSample);

            var deltas = new List<(PathfindQueryStat Stat, int Queries, int Successes, double Traversal)>();
            foreach (var stat in current.Values)
            {
                previous.TryGetValue(stat.Key, out var before);
                var queries = stat.QueryCount - before.QueryCount;
                var successes = stat.SuccessCount - before.SuccessCount;
                var traversal = stat.GraphTraversal - before.GraphTraversal;
                if (queries < 0 || successes < 0 || traversal < -1e-6)
                    return Unavailable(CountersWereReset);
                deltas.Add((stat, queries, successes, Math.Max(0d, traversal)));
            }
            // A key that disappeared means the dictionary was cleared and refilled within the interval.
            if (previous.Keys.Any(key => !current.ContainsKey(key)))
                return Unavailable(CountersWereReset);

            var seconds = timestampSeconds - previousTimestamp;
            var metrics = new List<NamedMetricValue>
            {
                Rate("resultsPerSecond", deltas.Sum(delta => delta.Queries), seconds),
                Rate("pathfindResultsPerSecond", QueriesOf(deltas, "Pathfind"), seconds),
                Rate("coverageResultsPerSecond", QueriesOf(deltas, "Coverage"), seconds),
                Rate("availabilityResultsPerSecond", QueriesOf(deltas, "Availability"), seconds)
            };

            var pathfindQueries = QueriesOf(deltas, "Pathfind");
            metrics.Add(pathfindQueries > 0
                ? NamedMetricValue.Available(
                    "pathfindSuccessRatio",
                    (double)deltas.Where(delta => delta.Stat.QueryType == "Pathfind").Sum(delta => delta.Successes) / pathfindQueries,
                    MetricConfidence.Full,
                    MetricUnits.Ratio)
                : NamedMetricValue.Unavailable("pathfindSuccessRatio", NoPathfindQueries));

            var bySystem = deltas
                .GroupBy(delta => delta.Stat.System, StringComparer.Ordinal)
                .Select(group => (System: group.Key, Queries: group.Sum(delta => delta.Queries), Traversal: group.Sum(delta => delta.Traversal)))
                .Where(system => system.Queries > 0)
                .OrderByDescending(system => system.Traversal)
                .ThenByDescending(system => system.Queries)
                .ThenBy(system => system.System, StringComparer.Ordinal)
                .Take(TopSystemCount);
            foreach (var system in bySystem)
            {
                metrics.Add(Rate("systemResultsPerSecond:" + system.System, system.Queries, seconds));
                // Path graphs explored per second: the requesting system's share of the pathfinding work.
                metrics.Add(NamedMetricValue.Available("systemGraphTraversalPerSecond:" + system.System,
                    system.Traversal / seconds, MetricConfidence.Full));
            }
            return metrics;
        }

        private static int QueriesOf(IEnumerable<(PathfindQueryStat Stat, int Queries, int Successes, double Traversal)> deltas, string queryType) =>
            deltas.Where(delta => delta.Stat.QueryType == queryType).Sum(delta => delta.Queries);

        private static NamedMetricValue Rate(string id, int count, double seconds) =>
            NamedMetricValue.Available(id, count / seconds, MetricConfidence.Full);

        private static IReadOnlyList<NamedMetricValue> Unavailable(string reason) => new[]
        {
            NamedMetricValue.Unavailable("resultsPerSecond", reason),
            NamedMetricValue.Unavailable("pathfindResultsPerSecond", reason),
            NamedMetricValue.Unavailable("coverageResultsPerSecond", reason),
            NamedMetricValue.Unavailable("availabilityResultsPerSecond", reason),
            NamedMetricValue.Unavailable("pathfindSuccessRatio", reason)
        };
    }
}
