using System.Collections.Generic;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Profiling
{
    internal sealed class ManagedSystemTimingInstrumentationAdapter : IManagedSystemTimingInstrumentation
    {
        private readonly ManagedSystemTimingHarmonyInstrumentation _inner =
            new ManagedSystemTimingHarmonyInstrumentation();

        public bool TryInstall(out string reason) => _inner.TryInstall(out reason);

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class ManagedSystemTimingBridgeAdapter : IManagedSystemTimingBridge
    {
        public bool IsActive => ManagedSystemTimingBridge.IsActive;

        public void BeginCapture() => ManagedSystemTimingBridge.BeginCapture();

        public SystemTimingSnapshot EndCapture(IEnumerable<SystemDescriptor> systems) =>
            ManagedSystemTimingBridge.EndCapture(systems);

        public void AbortCapture() => ManagedSystemTimingBridge.AbortCapture();
    }
}
