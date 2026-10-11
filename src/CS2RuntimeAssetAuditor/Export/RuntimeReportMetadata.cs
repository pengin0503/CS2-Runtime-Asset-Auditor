using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Export
{
    public sealed class RuntimeReportMetadata
    {
        public string BuildId { get; set; }
        public string HardwareSummary { get; set; }
        /// <summary>SystemInfo.graphicsMemorySize in megabytes; null when it was not read.</summary>
        public int? GpuMemoryCapacityMegabytes { get; set; }
        public IReadOnlyList<string> EnabledMods { get; set; } = Array.Empty<string>();
        /// <summary>Code mods the game did not load, as "name (ModInfo.State)".</summary>
        public IReadOnlyList<string> ModLoadFailures { get; set; } = Array.Empty<string>();
        /// <summary>The game's latest stored benchmark result; null when none was stored or it could not be read.</summary>
        public ReportGameBenchmark GameBenchmark { get; set; }
    }
}
