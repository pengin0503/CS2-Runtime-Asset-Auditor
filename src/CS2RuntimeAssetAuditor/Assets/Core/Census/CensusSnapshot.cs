using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Census
{
    public sealed class CensusSnapshot
    {
        private readonly IReadOnlyDictionary<PrefabKey, CensusEntry> _byKey;
        private readonly IReadOnlyList<CensusEntry> _entries;

        public CensusSnapshot(
            long worldGeneration,
            string queryProfileVersion,
            ScanOptions scanOptions,
            DateTimeOffset capturedAt,
            long catalogGeneration,
            IEnumerable<CensusEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(queryProfileVersion))
                throw new ArgumentException("A query-profile version is required.", nameof(queryProfileVersion));
            if (scanOptions == null)
                throw new ArgumentNullException(nameof(scanOptions));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            WorldGeneration = worldGeneration;
            QueryProfileVersion = queryProfileVersion;
            ScanOptions = scanOptions;
            CapturedAt = capturedAt;
            CatalogGeneration = catalogGeneration;

            var byKey = new Dictionary<PrefabKey, CensusEntry>();
            foreach (var entry in entries)
            {
                if (entry == null)
                    throw new ArgumentException("Census entries cannot be null.", nameof(entries));
                if (byKey.ContainsKey(entry.Key))
                    throw new ArgumentException("A Prefab can appear only once in a snapshot.", nameof(entries));
                byKey.Add(entry.Key, entry);
            }

            var ordered = byKey.Values
                .OrderBy(entry => entry.Key.PrefabType, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.PrefabId, StringComparer.Ordinal)
                .ToArray();
            _byKey = new ReadOnlyDictionary<PrefabKey, CensusEntry>(byKey);
            _entries = Array.AsReadOnly(ordered);
        }

        public long WorldGeneration { get; }

        public string QueryProfileVersion { get; }

        public ScanOptions ScanOptions { get; }

        public DateTimeOffset CapturedAt { get; }

        public long CatalogGeneration { get; }

        public IReadOnlyList<CensusEntry> Entries => _entries;

        public bool TryGetEntry(PrefabKey key, out CensusEntry entry)
        {
            if (_byKey.TryGetValue(key, out var found))
            {
                entry = found;
                return true;
            }

            entry = null!;
            return false;
        }
    }
}
