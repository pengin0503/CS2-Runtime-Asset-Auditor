namespace CS2RuntimeProfiler.Collectors
{
    public interface IMetricCollector
    {
        string Name { get; }
        void Sample(double timestampSeconds);
    }
}
