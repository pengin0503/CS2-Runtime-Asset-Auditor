#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Applies system-timing projection to newly completed captures exactly once.
    /// Runtime callers can invoke this after each capture-controller update without reprocessing retained history.
    /// </summary>
    public sealed class CaptureCompletionTimingProcessor
    {
        private readonly IReadOnlyList<SystemDescriptor> _systems;
        private readonly Func<IEnumerable<RecorderDescriptor>> _recorderProvider;
        private readonly HashSet<CaptureSession> _processed = new HashSet<CaptureSession>();

        public CaptureCompletionTimingProcessor(
            IEnumerable<SystemDescriptor> systems,
            IEnumerable<RecorderDescriptor> recorders)
            : this(systems, () => recorders ?? Array.Empty<RecorderDescriptor>())
        {
        }

        public CaptureCompletionTimingProcessor(
            IEnumerable<SystemDescriptor> systems,
            Func<IEnumerable<RecorderDescriptor>> recorderProvider)
        {
            _systems = (systems ?? Array.Empty<SystemDescriptor>()).Where(x => x != null).ToArray();
            _recorderProvider = recorderProvider ?? (() => Array.Empty<RecorderDescriptor>());
        }

        public int ProcessedCount { get; private set; }
        public CaptureSession? LastProcessedCapture { get; private set; }

        public void ProcessNew(IReadOnlyList<CaptureSession> completedCaptures)
        {
            if (completedCaptures == null)
                return;

            var retained = new HashSet<CaptureSession>(completedCaptures.Where(capture => capture != null));
            _processed.RemoveWhere(capture => !retained.Contains(capture));

            foreach (var capture in completedCaptures)
            {
                if (capture == null || !_processed.Add(capture))
                    continue;

                var recorders = (_recorderProvider() ?? Array.Empty<RecorderDescriptor>())
                    .Where(x => x != null)
                    .ToArray();

                ProcessedCount++;
                LastProcessedCapture = capture;
                CaptureSystemTimingFinalizer.Apply(capture, _systems, recorders);
            }
        }
    }
}
