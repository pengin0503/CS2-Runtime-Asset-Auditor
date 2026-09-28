using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Profiling
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
