using System;

namespace CS2RuntimeAssetAuditor.Core.Frames
{
    /// <summary>
    /// The limits <c>SimulationSystem.OnUpdate</c> (game 1.6.2f1) puts on how many simulation steps one rendered
    /// frame may run. 60 steps are one second of game time at 1x speed.
    /// </summary>
    public static class SimulationStepLimits
    {
        public const double StepsPerSecondAtNormalSpeed = 60d;

        /// <summary>The game never runs more simulation steps than this in one rendered frame.</summary>
        public const int MaxStepsPerRenderedFrame = 8;

        /// <summary>The game slows the simulation when fewer pathfinding lead frames than this remain.</summary>
        public const int PathfindThrottleLeadFrames = 48;

        /// <summary>
        /// The render-frame step cap, ignoring the pathfinding factor: <c>max(1, min(8, round(selectedSpeed * 2)))</c>.
        /// Unity's <c>Mathf.RoundToInt</c> rounds halves to even.
        /// </summary>
        public static int RenderFrameStepCap(double selectedSpeed)
        {
            var rounded = (int)Math.Round(selectedSpeed * 2d, MidpointRounding.ToEven);
            return Math.Max(1, Math.Min(MaxStepsPerRenderedFrame, rounded));
        }

        /// <summary>
        /// The highest efficiency (actual / selected speed) the render-frame step cap allows at this frame rate.
        /// At 1x, 2x and 4x it is <c>min(1, fps / 30)</c>: below 30 fps the simulation cannot keep up even when each
        /// step is cheap. Returns null when the simulation is paused or no frame was measured.
        /// </summary>
        public static double? FrameRateEfficiencyCeiling(double selectedSpeed, double framesPerSecond)
        {
            if (selectedSpeed <= 0d || framesPerSecond <= 0d || double.IsNaN(framesPerSecond) || double.IsInfinity(framesPerSecond))
                return null;
            var ceiling = RenderFrameStepCap(selectedSpeed) * framesPerSecond / (StepsPerSecondAtNormalSpeed * selectedSpeed);
            return Math.Min(1d, ceiling);
        }
    }
}
