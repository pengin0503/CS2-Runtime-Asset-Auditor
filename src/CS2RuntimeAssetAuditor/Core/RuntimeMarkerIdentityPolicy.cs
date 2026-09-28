namespace CS2RuntimeAssetAuditor.Core
{
    public static class RuntimeMarkerIdentityPolicy
    {
        public static bool AllowStrictFullTypeFallback(bool systemIsLive, string profilerMarkerName)
        {
            return systemIsLive && string.IsNullOrWhiteSpace(profilerMarkerName);
        }
    }
}
