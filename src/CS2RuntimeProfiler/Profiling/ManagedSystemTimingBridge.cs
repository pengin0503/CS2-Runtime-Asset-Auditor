using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Profiling
{
    internal static class ManagedSystemTimingBridge
    {
        private static readonly object Gate = new object();
        private static volatile ManagedSystemTimingAccumulator _active;
        private static int _startFrame;

        // This check runs from the Harmony prefix of every managed SystemBase.Update call.
        // Keep the normal-monitoring path lock-free; locking is limited to active Deep Capture.
        public static bool IsActive => _active != null;

        public static void BeginCapture()
        {
            lock (Gate)
            {
                _startFrame = UnityEngine.Time.frameCount;
                _active = new ManagedSystemTimingAccumulator();
            }
        }

        public static void Record(string systemId, double milliseconds)
        {
            var active = _active;
            if (active == null)
                return;

            lock (Gate)
            {
                if (ReferenceEquals(_active, active))
                    active.Record(systemId, milliseconds);
            }
        }

        public static SystemTimingSnapshot EndCapture(IEnumerable<SystemDescriptor> systems)
        {
            ManagedSystemTimingAccumulator completed;
            int windowFrames;
            lock (Gate)
            {
                completed = _active;
                _active = null;
                // Begin/End run on the main thread; frames rendered in between normalize
                // per-system totals so rarely updating systems are not ranked by one spike.
                windowFrames = UnityEngine.Time.frameCount - _startFrame;
            }

            return completed?.BuildSnapshot(systems, windowFrames > 0 ? windowFrames : (int?)null) ?? new SystemTimingSnapshot();
        }

        public static void AbortCapture()
        {
            lock (Gate)
                _active = null;
        }
    }
}
