using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Profiling
{
    public interface IRecorderBackend
    {
        IReadOnlyList<RecorderDescriptor> Discover();
        IActiveRecorder Start(RecorderDescriptor descriptor, int capacity);
    }

    public interface IActiveRecorder : IDisposable
    {
        string Id { get; }
        RecorderReading Read();
    }
}
