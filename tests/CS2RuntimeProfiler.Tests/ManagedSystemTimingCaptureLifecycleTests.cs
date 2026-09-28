using CS2RuntimeProfiler.Core;
using CS2RuntimeProfiler.Profiling;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class ManagedSystemTimingCaptureLifecycleTests
{
    [Test]
    public void Start_and_finish_scope_instrumentation_to_the_active_capture()
    {
        var instrumentation = new FakeInstrumentation();
        var bridge = new FakeBridge();
        var lifecycle = new ManagedSystemTimingCaptureLifecycle(instrumentation, bridge);

        Assert.That(lifecycle.IsActive, Is.False);
        Assert.That(instrumentation.InstallCalls, Is.Zero);

        Assert.That(lifecycle.TryStart(out var reason), Is.True);
        Assert.That(reason, Is.Null);
        Assert.That(lifecycle.IsActive, Is.True);
        Assert.That(instrumentation.InstallCalls, Is.EqualTo(1));
        Assert.That(bridge.BeginCalls, Is.EqualTo(1));

        var snapshot = lifecycle.Finish(Array.Empty<SystemDescriptor>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot, Is.SameAs(bridge.Snapshot));
            Assert.That(lifecycle.IsActive, Is.False);
            Assert.That(bridge.EndCalls, Is.EqualTo(1));
            Assert.That(instrumentation.DisposeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Abort_clears_bridge_state_and_disposes_instrumentation()
    {
        var instrumentation = new FakeInstrumentation();
        var bridge = new FakeBridge();
        var lifecycle = new ManagedSystemTimingCaptureLifecycle(instrumentation, bridge);
        Assert.That(lifecycle.TryStart(out _), Is.True);

        lifecycle.Abort();

        Assert.Multiple(() =>
        {
            Assert.That(lifecycle.IsActive, Is.False);
            Assert.That(bridge.AbortCalls, Is.EqualTo(1));
            Assert.That(instrumentation.DisposeCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Repeated_capture_cycles_reinstall_and_unpatch_cleanly()
    {
        var instrumentation = new FakeInstrumentation();
        var bridge = new FakeBridge();
        var lifecycle = new ManagedSystemTimingCaptureLifecycle(instrumentation, bridge);

        Assert.That(lifecycle.TryStart(out _), Is.True);
        lifecycle.Finish(Array.Empty<SystemDescriptor>());
        Assert.That(lifecycle.TryStart(out _), Is.True);
        lifecycle.Finish(Array.Empty<SystemDescriptor>());

        Assert.Multiple(() =>
        {
            Assert.That(instrumentation.InstallCalls, Is.EqualTo(2));
            Assert.That(instrumentation.DisposeCalls, Is.EqualTo(2));
            Assert.That(bridge.BeginCalls, Is.EqualTo(2));
            Assert.That(bridge.EndCalls, Is.EqualTo(2));
            Assert.That(lifecycle.IsActive, Is.False);
        });
    }

    [Test]
    public void Failed_install_disposes_partial_instrumentation_without_starting_accumulation()
    {
        var instrumentation = new FakeInstrumentation { InstallSucceeds = false };
        var bridge = new FakeBridge();
        var lifecycle = new ManagedSystemTimingCaptureLifecycle(instrumentation, bridge);

        Assert.That(lifecycle.TryStart(out var reason), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.EqualTo("unavailable"));
            Assert.That(instrumentation.DisposeCalls, Is.EqualTo(1));
            Assert.That(bridge.BeginCalls, Is.Zero);
            Assert.That(bridge.IsActive, Is.False);
            Assert.That(lifecycle.IsActive, Is.False);
        });
    }

    [Test]
    public void Install_exception_is_fail_open_and_disposes_instrumentation()
    {
        var instrumentation = new FakeInstrumentation
        {
            InstallException = new InvalidOperationException("install failed")
        };
        var bridge = new FakeBridge();
        var lifecycle = new ManagedSystemTimingCaptureLifecycle(instrumentation, bridge);

        Assert.That(lifecycle.TryStart(out var reason), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.EqualTo("install failed"));
            Assert.That(instrumentation.DisposeCalls, Is.EqualTo(1));
            Assert.That(bridge.BeginCalls, Is.Zero);
            Assert.That(bridge.IsActive, Is.False);
            Assert.That(lifecycle.IsActive, Is.False);
        });
    }

    private sealed class FakeInstrumentation : IManagedSystemTimingInstrumentation
    {
        public int InstallCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool InstallSucceeds { get; set; } = true;
        public Exception InstallException { get; set; }

        public bool TryInstall(out string reason)
        {
            InstallCalls++;
            if (InstallException != null)
                throw InstallException;
            reason = InstallSucceeds ? null : "unavailable";
            return InstallSucceeds;
        }

        public void Dispose() => DisposeCalls++;
    }

    private sealed class FakeBridge : IManagedSystemTimingBridge
    {
        public SystemTimingSnapshot Snapshot { get; } = new();
        public bool IsActive { get; private set; }
        public int BeginCalls { get; private set; }
        public int EndCalls { get; private set; }
        public int AbortCalls { get; private set; }

        public void BeginCapture()
        {
            BeginCalls++;
            IsActive = true;
        }

        public SystemTimingSnapshot EndCapture(IEnumerable<SystemDescriptor> systems)
        {
            EndCalls++;
            IsActive = false;
            return Snapshot;
        }

        public void AbortCapture()
        {
            AbortCalls++;
            IsActive = false;
        }
    }
}
