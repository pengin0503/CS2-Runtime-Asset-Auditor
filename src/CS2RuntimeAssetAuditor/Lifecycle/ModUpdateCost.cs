using System.Diagnostics;
using System.Globalization;
using CS2RuntimeAssetAuditor.Core.Frames;

namespace CS2RuntimeAssetAuditor.Lifecycle
{
    /// <summary>
    /// Times every update of the mod's systems. The diagnostic log writes the per-frame total, and an update
    /// slower than <see cref="ModUpdateCostTracker.DefaultSlowUpdateMilliseconds"/> is written to the mod log, so a
    /// long frame shows whether the mod's own code was part of it.
    /// </summary>
    internal static class ModUpdateCost
    {
        private static readonly ModUpdateCostTracker Tracker = new ModUpdateCostTracker();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        public static ModFrameCost LastCompletedFrame => Tracker.LastCompletedFrame;

        public static long Start() => Stopwatch.GetTimestamp();

        public static void Stop(string system, long start)
        {
            try
            {
                var milliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                var slow = Tracker.Add(UnityEngine.Time.frameCount, system, milliseconds, Clock.Elapsed.TotalSeconds);
                if (!slow.HasValue)
                    return;
                Mod.Log.Info(string.Format(
                    CultureInfo.InvariantCulture,
                    "Slow mod update: system={0} ms={1:0.0} frame={2} suppressedSinceLastReport={3}",
                    slow.Value.System,
                    slow.Value.Milliseconds,
                    slow.Value.FrameIndex,
                    slow.Value.SuppressedSinceLastReport));
            }
            catch
            {
                // Timing is diagnostic only and must never break a system update.
            }
        }
    }
}
