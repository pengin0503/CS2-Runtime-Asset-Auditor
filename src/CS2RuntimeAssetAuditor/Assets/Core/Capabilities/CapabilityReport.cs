using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Assets.Core.Capabilities
{
    public sealed class CapabilityStatus
    {
        public CapabilityStatus(CapabilityId id, CapabilityState state, string? detail = null)
        {
            Id = id;
            State = state;
            Detail = detail;
        }

        public CapabilityId Id { get; }

        public CapabilityState State { get; }

        public string? Detail { get; }
    }

    public sealed class CapabilityReport
    {
        private readonly IReadOnlyDictionary<CapabilityId, CapabilityStatus> _byId;
        private readonly IReadOnlyList<CapabilityStatus> _capabilities;

        public CapabilityReport(
            string gameVersion,
            CompatibilityState compatibility,
            IEnumerable<CapabilityStatus> capabilities)
        {
            if (string.IsNullOrWhiteSpace(gameVersion))
                throw new ArgumentException("A game version is required.", nameof(gameVersion));
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            GameVersion = gameVersion;
            Compatibility = compatibility;

            var byId = new Dictionary<CapabilityId, CapabilityStatus>();
            foreach (var capability in capabilities)
            {
                if (capability == null)
                    throw new ArgumentException("Capability entries cannot be null.", nameof(capabilities));
                if (byId.ContainsKey(capability.Id))
                    throw new ArgumentException("Each capability can appear only once.", nameof(capabilities));
                byId.Add(capability.Id, capability);
            }

            var ordered = byId.Values.OrderBy(item => item.Id).ToArray();
            _byId = new ReadOnlyDictionary<CapabilityId, CapabilityStatus>(byId);
            _capabilities = Array.AsReadOnly(ordered);
        }

        public string GameVersion { get; }

        public CompatibilityState Compatibility { get; }

        public IReadOnlyList<CapabilityStatus> Capabilities => _capabilities;

        public bool TryGet(CapabilityId id, out CapabilityStatus status)
        {
            if (_byId.TryGetValue(id, out var found))
            {
                status = found;
                return true;
            }

            status = null!;
            return false;
        }
    }
}
