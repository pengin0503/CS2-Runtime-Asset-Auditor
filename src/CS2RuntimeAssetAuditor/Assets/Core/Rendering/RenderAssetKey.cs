using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public readonly struct RenderAssetKey : IEquatable<RenderAssetKey>
    {
        private readonly string? _renderAssetId;
        private readonly string? _renderAssetType;

        public RenderAssetKey(string renderAssetId, string renderAssetType)
        {
            if (string.IsNullOrWhiteSpace(renderAssetId))
                throw new ArgumentException("A stable render-asset ID is required.", nameof(renderAssetId));
            if (string.IsNullOrWhiteSpace(renderAssetType))
                throw new ArgumentException("A render-asset type is required.", nameof(renderAssetType));
            _renderAssetId = renderAssetId;
            _renderAssetType = renderAssetType;
        }
        public string RenderAssetId => _renderAssetId ?? string.Empty;
        public string RenderAssetType => _renderAssetType ?? string.Empty;
        public bool IsValid => _renderAssetId != null && _renderAssetType != null;
        public bool Equals(RenderAssetKey other) => StringComparer.Ordinal.Equals(RenderAssetId, other.RenderAssetId) && StringComparer.Ordinal.Equals(RenderAssetType, other.RenderAssetType);
        public override bool Equals(object? obj) => obj is RenderAssetKey other && Equals(other);
        public override int GetHashCode() { unchecked { return (StringComparer.Ordinal.GetHashCode(RenderAssetId) * 397) ^ StringComparer.Ordinal.GetHashCode(RenderAssetType); } }
        public override string ToString() => RenderAssetType + ":" + RenderAssetId;
        public static bool TryParse(string? value, out RenderAssetKey key)
        {
            key = default;
            if (value == null || string.IsNullOrWhiteSpace(value)) return false;
            var separator = value.IndexOf(':');
            if (separator <= 0 || separator >= value.Length - 1) return false;
            var type = value.Substring(0, separator);
            var id = value.Substring(separator + 1);
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id)) return false;
            key = new RenderAssetKey(id, type);
            return true;
        }
        public static bool operator ==(RenderAssetKey left, RenderAssetKey right) => left.Equals(right);
        public static bool operator !=(RenderAssetKey left, RenderAssetKey right) => !left.Equals(right);
    }
}
