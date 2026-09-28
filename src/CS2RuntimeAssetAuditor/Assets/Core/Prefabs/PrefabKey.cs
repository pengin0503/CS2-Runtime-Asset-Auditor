using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Prefabs
{
    public readonly struct PrefabKey : IEquatable<PrefabKey>
    {
        private readonly string? _prefabId;
        private readonly string? _prefabType;

        public PrefabKey(string prefabId, string prefabType)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                throw new ArgumentException("A stable Prefab ID is required.", nameof(prefabId));
            if (string.IsNullOrWhiteSpace(prefabType))
                throw new ArgumentException("A Prefab type is required.", nameof(prefabType));

            _prefabId = prefabId;
            _prefabType = prefabType;
        }

        public string PrefabId => _prefabId ?? string.Empty;

        public string PrefabType => _prefabType ?? string.Empty;

        public bool IsValid => _prefabId != null && _prefabType != null;

        public bool Equals(PrefabKey other)
        {
            return StringComparer.Ordinal.Equals(PrefabId, other.PrefabId)
                && StringComparer.Ordinal.Equals(PrefabType, other.PrefabType);
        }

        public override bool Equals(object? obj)
        {
            return obj is PrefabKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(PrefabId) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(PrefabType);
            }
        }

        public override string ToString()
        {
            return PrefabType + ":" + PrefabId;
        }

        public static bool operator ==(PrefabKey left, PrefabKey right) => left.Equals(right);

        public static bool operator !=(PrefabKey left, PrefabKey right) => !left.Equals(right);
    }
}
