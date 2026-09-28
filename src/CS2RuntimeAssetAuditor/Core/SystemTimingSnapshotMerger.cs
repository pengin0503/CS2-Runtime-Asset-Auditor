using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class SystemTimingSnapshotMerger
    {
        public static SystemTimingSnapshot Merge(SystemTimingSnapshot primary, SystemTimingSnapshot fallback)
        {
            var result = new SystemTimingSnapshot();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (primary != null)
            {
                foreach (var entry in primary.Systems)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.SystemId) || !seen.Add(entry.SystemId))
                        continue;
                    result.AddEntry(entry);
                }
            }

            if (fallback != null)
            {
                foreach (var entry in fallback.Systems)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.SystemId) || !seen.Add(entry.SystemId))
                        continue;
                    result.AddEntry(entry);
                }
            }

            var unattributedJobsMilliseconds = primary?.UnattributedJobsMilliseconds
                ?? fallback?.UnattributedJobsMilliseconds;
            if (unattributedJobsMilliseconds.HasValue)
                result.SetUnattributedJobsMilliseconds(unattributedJobsMilliseconds.Value);

            return result;
        }
    }
}
