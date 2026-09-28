namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class PatchOwnerInfo
    {
        public PatchOwnerInfo(string ownerId, string displayName)
        {
            OwnerId = ownerId ?? string.Empty;
            DisplayName = displayName ?? ownerId ?? string.Empty;
        }

        public string OwnerId { get; }
        public string DisplayName { get; }
    }
}
