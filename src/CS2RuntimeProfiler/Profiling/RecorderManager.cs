using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Profiling
{
    public sealed class RecorderManager : IDisposable
    {
        private readonly IRecorderBackend _backend;
        private readonly Dictionary<string, RecorderDescriptor> _discovered = new Dictionary<string, RecorderDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, IActiveRecorder> _active = new Dictionary<string, IActiveRecorder>(StringComparer.Ordinal);

        public RecorderManager(IRecorderBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public IReadOnlyCollection<string> ActiveIds => _active.Keys.ToArray();
        public IReadOnlyCollection<RecorderDescriptor> Descriptors => _discovered.Values.ToArray();

        public IReadOnlyList<RecorderDescriptor> DiscoverAvailableMarkers()
        {
            var discovered = _backend.Discover() ?? Array.Empty<RecorderDescriptor>();
            var replacement = new Dictionary<string, RecorderDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in discovered)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;
                replacement[descriptor.Id] = descriptor;
            }

            _discovered.Clear();
            foreach (var pair in replacement)
                _discovered[pair.Key] = pair.Value;

            return _discovered.Values.ToArray();
        }

        public bool TryActivate(string id, int capacity, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "Recorder id is empty.";
                return false;
            }

            if (_active.ContainsKey(id))
                return true;

            if (!_discovered.TryGetValue(id, out var descriptor))
            {
                error = $"Recorder '{id}' was not discovered.";
                return false;
            }

            try
            {
                _active[id] = _backend.Start(descriptor, Math.Max(1, capacity));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public RecorderDescriptor FindFirstByName(params string[] preferredNames)
        {
            if (preferredNames == null)
                return null;

            foreach (var preferredName in preferredNames)
            {
                if (string.IsNullOrWhiteSpace(preferredName))
                    continue;

                var exact = _discovered.Values.FirstOrDefault(x => string.Equals(x.Name, preferredName, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;
            }

            foreach (var preferredName in preferredNames)
            {
                if (string.IsNullOrWhiteSpace(preferredName))
                    continue;

                var contains = _discovered.Values.FirstOrDefault(x => x.Name.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (contains != null)
                    return contains;
            }

            return null;
        }

        public IReadOnlyDictionary<string, RecorderReading> SampleActive()
        {
            var readings = new Dictionary<string, RecorderReading>(StringComparer.Ordinal);
            foreach (var pair in _active.ToArray())
            {
                try
                {
                    readings[pair.Key] = pair.Value.Read();
                }
                catch
                {
                    // A single recorder failing must not break other metrics.
                }
            }
            return readings;
        }

        public void DeactivateAll()
        {
            foreach (var recorder in _active.Values)
            {
                try { recorder.Dispose(); }
                catch { }
            }
            _active.Clear();
        }

        public void Dispose() => DeactivateAll();
    }
}
