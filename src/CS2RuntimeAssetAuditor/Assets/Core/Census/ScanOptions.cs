namespace CS2RuntimeAssetAuditor.Assets.Core.Census
{
    public sealed class ScanOptions
    {
        public ScanOptions(bool collectSubordinateObjects, bool collectNetworkEdges)
        {
            CollectSubordinateObjects = collectSubordinateObjects;
            CollectNetworkEdges = collectNetworkEdges;
        }

        public bool CollectSubordinateObjects { get; }

        public bool CollectNetworkEdges { get; }

        public static ScanOptions Default => new ScanOptions(collectSubordinateObjects: true, collectNetworkEdges: true);
    }
}
