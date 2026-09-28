using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Profiling
{
    public interface IManagedSystemTimingInstrumentation : IDisposable
    {
        bool TryInstall(out string reason);
    }

    public interface IManagedSystemTimingBridge
    {
        bool IsActive { get; }
        void BeginCapture();
        SystemTimingSnapshot EndCapture(IEnumerable<SystemDescriptor> systems);
        void AbortCapture();
    }

    public sealed class ManagedSystemTimingCaptureLifecycle : IDisposable
    {
        private readonly IManagedSystemTimingInstrumentation _instrumentation;
        private readonly IManagedSystemTimingBridge _bridge;
        private bool _active;

        public ManagedSystemTimingCaptureLifecycle(
            IManagedSystemTimingInstrumentation instrumentation,
            IManagedSystemTimingBridge bridge)
        {
            _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool IsActive => _active;

        public bool TryStart(out string reason)
        {
            if (_active)
            {
                reason = null;
                return true;
            }

            try
            {
                if (!_instrumentation.TryInstall(out reason))
                {
                    reason = DisposeInstrumentation(reason);
                    _active = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                reason = DisposeInstrumentation(ex.GetBaseException().Message);
                _active = false;
                return false;
            }

            try
            {
                _bridge.BeginCapture();
                if (!_bridge.IsActive)
                    throw new InvalidOperationException("Managed timing bridge did not enter the active state.");

                _active = true;
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.GetBaseException().Message;
                try
                {
                    _bridge.AbortCapture();
                }
                catch (Exception abortException)
                {
                    reason += "; bridge cleanup failed: " + abortException.GetBaseException().Message;
                }

                reason = DisposeInstrumentation(reason);
                _active = false;
                return false;
            }
        }

        public SystemTimingSnapshot Finish(IEnumerable<SystemDescriptor> systems)
        {
            if (!_active)
                return new SystemTimingSnapshot();

            try
            {
                return _bridge.EndCapture(systems) ?? new SystemTimingSnapshot();
            }
            finally
            {
                _active = false;
                DisposeInstrumentation(null);
            }
        }

        public void Abort()
        {
            if (!_active && !_bridge.IsActive)
                return;

            try
            {
                _bridge.AbortCapture();
            }
            catch
            {
                // Instrumentation must still be disposed when bridge shutdown fails.
            }
            finally
            {
                _active = false;
                DisposeInstrumentation(null);
            }
        }

        public void Dispose() => Abort();

        private string DisposeInstrumentation(string reason)
        {
            try
            {
                _instrumentation.Dispose();
            }
            catch (Exception ex)
            {
                var cleanupError = "instrumentation cleanup failed: " + ex.GetBaseException().Message;
                return string.IsNullOrWhiteSpace(reason) ? cleanupError : reason + "; " + cleanupError;
            }

            return reason;
        }
    }
}
