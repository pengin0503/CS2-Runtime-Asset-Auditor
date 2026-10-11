using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// The profiler recorder handles of the last successful discovery, by recorder id. A rediscovery replaces the
    /// table only once it has enumerated every handle: if enumeration throws, the handles of the previous discovery
    /// stay usable, so recorders that are running or about to start are not orphaned by a failed refresh.
    /// </summary>
    public sealed class RecorderHandleTable<THandle>
    {
        private Dictionary<string, THandle> _handles = new Dictionary<string, THandle>(StringComparer.Ordinal);

        public int Count => _handles.Count;

        /// <param name="enumerate">Lists the available handles; an exception leaves the table unchanged.</param>
        /// <param name="describe">
        /// Describes one handle, or returns null for an invalid one. A handle whose description throws is skipped
        /// (an unsupported or stripped marker); the first handle with a given id wins.
        /// </param>
        public IReadOnlyList<RecorderDescriptor> Rediscover(Func<IReadOnlyList<THandle>> enumerate, Func<THandle, RecorderDescriptor?> describe)
        {
            if (enumerate == null) throw new ArgumentNullException(nameof(enumerate));
            if (describe == null) throw new ArgumentNullException(nameof(describe));

            var handles = enumerate() ?? Array.Empty<THandle>();
            var discovered = new Dictionary<string, THandle>(StringComparer.Ordinal);
            var result = new List<RecorderDescriptor>(handles.Count);
            foreach (var handle in handles)
            {
                RecorderDescriptor? descriptor;
                try { descriptor = describe(handle); }
                catch { continue; }
                if (descriptor == null || discovered.ContainsKey(descriptor.Id))
                    continue;
                discovered[descriptor.Id] = handle;
                result.Add(descriptor);
            }

            _handles = discovered;
            return result;
        }

        public bool TryGet(string id, out THandle handle) => _handles.TryGetValue(id, out handle!);
    }
}
