using System;

namespace CS2RuntimeAssetAuditor.Core
{
    public static class ManualCaptureClock
    {
        /// <summary>
        /// The time a manual capture request is stamped with: the runtime clock at the moment of the click. The
        /// latest sample can be up to one sample period old, and stamping the request with it would start the
        /// capture window in the past, so it could end (or skip its post-buffer) before any frame is recorded.
        /// Without a runtime clock the last observed time is the best available value.
        /// </summary>
        public static double RequestTime(double? runtimeClockSeconds, double lastObservedSeconds)
            => runtimeClockSeconds ?? Math.Max(0d, lastObservedSeconds);
    }
}
