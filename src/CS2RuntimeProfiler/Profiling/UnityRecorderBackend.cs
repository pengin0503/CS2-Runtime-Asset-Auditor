using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace CS2RuntimeProfiler.Profiling
{
    internal sealed class UnityRecorderBackend : IRecorderBackend
    {
        private readonly Dictionary<string, ProfilerRecorderHandle> _handles = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.Ordinal);

        public IReadOnlyList<RecorderDescriptor> Discover()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);

            var discoveredHandles = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.Ordinal);
            var result = new List<RecorderDescriptor>(handles.Count);

            foreach (var handle in handles)
            {
                if (!handle.Valid)
                    continue;

                try
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    var category = description.Category.Name ?? string.Empty;
                    var name = description.Name ?? string.Empty;
                    var id = category + "\u001f" + name;
                    if (discoveredHandles.ContainsKey(id))
                        continue;

                    discoveredHandles[id] = handle;
                    result.Add(new RecorderDescriptor(
                        id,
                        category,
                        name,
                        description.UnitType.ToString(),
                        description.DataType.ToString()));
                }
                catch
                {
                    // Unsupported/stripped marker: omit from the usable runtime catalog.
                }
            }

            _handles.Clear();
            foreach (var pair in discoveredHandles)
                _handles[pair.Key] = pair.Value;

            return result;
        }

        public IActiveRecorder Start(RecorderDescriptor descriptor, int capacity)
        {
            if (!_handles.TryGetValue(descriptor.Id, out var handle))
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
