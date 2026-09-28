namespace CS2RuntimeAssetAuditor.Core
{
    public sealed class RecorderDescriptor
    {
        public RecorderDescriptor(string id, string category, string name, string unitType, string dataType)
        {
            Id = id ?? string.Empty;
            Category = category ?? string.Empty;
            Name = name ?? string.Empty;
            UnitType = unitType ?? string.Empty;
            DataType = dataType ?? string.Empty;
        }

        public string Id { get; }
        public string Category { get; }
        public string Name { get; }
        public string UnitType { get; }
        public string DataType { get; }
    }

    public readonly struct RecorderReading
    {
        public RecorderReading(double value, long count)
        {
            Value = value;
            Count = count;
        }

        public double Value { get; }
        public long Count { get; }
    }
}
