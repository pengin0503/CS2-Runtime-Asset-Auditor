using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class NormalMonitoringRecorderPolicy
    {
        private static readonly IReadOnlyList<string[]> Groups = BuildGroups();

        /// <summary>
        /// Each group is one recorder: the first name the game provides. The rendering counters
        /// (<see cref="RenderCounters"/>) follow the timing and memory recorders.
        /// </summary>
        public static IReadOnlyList<string[]> GetPreferredRecorderNameGroups() => Groups;

        private static IReadOnlyList<string[]> BuildGroups()
        {
            var groups = new List<string[]>
            {
                new[] { "Main Thread" },
                new[] { "Render Thread" },
                new[] { "GPU Frame Time", "GPU Time" },
                new[] { "Total Used Memory", "System Used Memory" },
                new[] { "Profiler Used Memory" }
            };
            foreach (var name in RenderCounters.MarkerNames())
                groups.Add(new[] { name });
            return groups;
        }
    }
}
