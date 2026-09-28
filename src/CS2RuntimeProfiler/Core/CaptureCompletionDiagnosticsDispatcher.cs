using System;

namespace CS2RuntimeProfiler.Core
{
    public static class CaptureCompletionDiagnosticsDispatcher
    {
        public static void Dispatch(CaptureSession capture, Action<string> write, Action flush = null)
        {
            if (capture == null || write == null)
                return;

            try
            {
                write(CaptureCompletionLogFormatter.Format(capture));
            }
            catch
            {
                return;
            }

            try
            {
                flush?.Invoke();
            }
            catch
            {
                // Diagnostics must never disturb capture completion.
            }
        }
    }
}
