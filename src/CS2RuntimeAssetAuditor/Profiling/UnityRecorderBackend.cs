using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace CS2RuntimeAssetAuditor.Profiling
{
    internal sealed class UnityRecorderBackend : IRecorderBackend
    {
        private readonly RecorderHandleTable<ProfilerRecorderHandle> _handles = new RecorderHandleTable<ProfilerRecorderHandle>();

        public IReadOnlyList<RecorderDescriptor> Discover() => _handles.Rediscover(EnumerateHandles, Describe);

        private static IReadOnlyList<ProfilerRecorderHandle> EnumerateHandles()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return handles;
        }

        private static RecorderDescriptor? Describe(ProfilerRecorderHandle handle)
        {
            if (!handle.Valid)
                return null;
            var description = ProfilerRecorderHandle.GetDescription(handle);
            var category = description.Category.Name ?? string.Empty;
            var name = description.Name ?? string.Empty;
            return new RecorderDescriptor(
                category + "\u001f" + name,
                category,
                name,
                description.UnitType.ToString(),
                description.DataType.ToString());
        }

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity)
        {
            if (!_handles.TryGet(descriptor.Id, out var handle))
                throw new InvalidOperationException($"Profiler marker '{descriptor.Id}' is no longer available.");

            var recorder = new ProfilerRecorder(handle, Math.Max(1, capacity));
            recorder.Start();
            return new UnityActiveRecorder(descriptor.Id, recorder);
        }

        private sealed class UnityActiveRecorder : IActiveRecorder
        {
            private ProfilerRecorder _recorder;

            public UnityActiveRecorder(string id, ProfilerRecorder recorder)
            {
                Id = id;
                _recorder = recorder;
            }

            public string Id { get; }

            public RecorderReading Read()
            {
                if (!_recorder.Valid || _recorder.Count == 0)
                    return new RecorderReading(0, 0);
                return new RecorderReading(_recorder.LastValueAsDouble, _recorder.GetSample(_recorder.Count - 1).Count);
            }

            public void Dispose()
            {
                if (!_recorder.Valid)
                    return;
                try { _recorder.Stop(); } catch { }
                _recorder.Dispose();
            }
        }
    }
}
