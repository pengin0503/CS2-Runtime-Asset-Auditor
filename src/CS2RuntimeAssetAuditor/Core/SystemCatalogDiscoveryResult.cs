using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class SystemCatalogDiscoveryResult
    {
        private SystemCatalogDiscoveryResult(
            IEnumerable<SystemDescriptor> systems,
            bool isComplete,
            string error)
        {
            Systems = Array.AsReadOnly((systems ?? Array.Empty<SystemDescriptor>())
                .Where(system => system != null)
                .ToArray());
            IsComplete = isComplete;
            Error = error;
        }

        public IReadOnlyList<SystemDescriptor> Systems { get; }
        public bool IsComplete { get; }
        public string Error { get; }

        public static SystemCatalogDiscoveryResult Complete(IEnumerable<SystemDescriptor> systems) =>
            new SystemCatalogDiscoveryResult(systems, isComplete: true, error: null);

        public static SystemCatalogDiscoveryResult Incomplete(
            IEnumerable<SystemDescriptor> availableSystems,
            string error) =>
            new SystemCatalogDiscoveryResult(
                availableSystems,
                isComplete: false,
                error: string.IsNullOrWhiteSpace(error) ? "System catalog discovery was incomplete." : error);
    }
}
