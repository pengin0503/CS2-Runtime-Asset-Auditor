using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeProfiler.Core
{
    public sealed class SystemCatalogCache
    {
        private readonly Func<SystemCatalogDiscoveryResult> _provider;
        private IReadOnlyList<SystemDescriptor> _snapshot = Array.Empty<SystemDescriptor>();

        public SystemCatalogCache(Func<SystemCatalogDiscoveryResult> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyList<SystemDescriptor> Snapshot => _snapshot;

        public bool TryRefresh(out string error)
        {
            try
            {
                var discovery = _provider()
                    ?? throw new InvalidOperationException("System catalog provider returned no discovery result.");
                var discovered = discovery.Systems
                    .Where(system => system != null && !string.IsNullOrWhiteSpace(system.FullTypeName))
                    .ToArray();

                if (!discovery.IsComplete)
                {
                    // Before the first complete discovery, keep any usable partial catalog as a bootstrap
                    // snapshot. Later incomplete refreshes must leave the published snapshot untouched.
                    if (_snapshot.Count == 0 && discovered.Length > 0)
                        _snapshot = discovered;
                    error = discovery.Error ?? "System catalog discovery was incomplete.";
                    return false;
                }

                _snapshot = discovered;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }
    }
}
