using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class NormalMonitoringRecorderPolicy
    {
        public static IReadOnlyList<string[]> GetPreferredRecorderNameGroups()
        {
            return new[]
            {
                new[] { "Main Thread" },
                new[] { "Render Thread" },
                new[] { "GPU Frame Time", "GPU Time" },
                new[] { "Total Used Memory", "System Used Memory" },
                new[] { "Profiler Used Memory" }
            };
        }
    }
}
