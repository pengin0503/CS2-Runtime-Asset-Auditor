using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Prefabs
{
    public sealed class PrefabRecord
    {
        public PrefabRecord(PrefabKey key, string displayName, PrefabTraits traits, AssetOriginEvidence originEvidence)
        {
            if (!key.IsValid)
                throw new ArgumentException("A Prefab record needs a valid stable key.", nameof(key));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", nameof(displayName));
            Key = key;
            DisplayName = displayName;
            Traits = traits;
            OriginEvidence = originEvidence ?? throw new ArgumentNullException(nameof(originEvidence));
        }

        public PrefabKey Key { get; }

        public string DisplayName { get; }

        public PrefabTraits Traits { get; }

        public AssetOriginEvidence OriginEvidence { get; }
    }
}
