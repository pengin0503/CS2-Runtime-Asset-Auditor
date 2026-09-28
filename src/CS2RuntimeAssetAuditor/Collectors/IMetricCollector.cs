namespace CS2RuntimeAssetAuditor.Collectors
{
    public interface IMetricCollector
    {
        string Name { get; }
        void Sample(double timestampSeconds);
    }
}
