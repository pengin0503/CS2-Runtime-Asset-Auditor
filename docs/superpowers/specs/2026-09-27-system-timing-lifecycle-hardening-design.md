# System Timing Lifecycle Hardening — Design Specification

Date: 2026-09-27
Status: Approved for autonomous implementation by project instruction
Repository: `pengin0503/CS2-Runtime-Profiler`

## Purpose

Harden the recently added managed System Timing fallback as one coherent capture-scoped subsystem rather than continuing to add local patches around marker matching, Harmony loading, aggregation, and catalog freshness.

The design preserves the existing evidence model:

- verified Unity profiler-marker timing remains `Full` and authoritative;
- managed `SystemBase.Update()` timing fills only systems without usable Full timing and remains `Managed` confidence;
- Job/Burst worker cost remains unattributed unless directly measured;
- normal monitoring stays read-mostly and low overhead.

## Problems confirmed in current `main`

### 1. Bounded managed samples incorrectly define whole-capture totals

`ManagedSystemTimingAccumulator` keeps at most 512 values per system. When more than 512 calls occur, older samples are discarded, but `BuildSnapshot()` computes `Calls`, `Mean`, and `TotalMilliseconds` only from the retained values. A 30-second capture or high-frequency managed system can therefore under-report total call count and total managed time.

This is a data-model problem, not a capacity tuning problem. Distribution sampling and whole-capture accounting have different retention requirements.

### 2. System Catalog lifecycle is stale

The approved base design says the System Catalog is built during initialization and refreshed on explicit or relevant lifecycle events. Current runtime code discovers `_systems` once in `CaptureRuntimeSystem.OnCreate()` and never refreshes it.

That can leave capture-time metadata stale when assemblies, systems, runtime marker identities, or Harmony patch metadata become available later in startup. Managed instrumentation can observe such systems by CLR type name, but ownership/patch attribution can remain missing or stale.

### 3. Managed fallback instrumentation is installed for the entire game session

`ManagedSystemTimingHarmonyInstrumentation` currently patches `SystemBase.Update()` in `OnCreate()` and leaves the patch installed while normal monitoring is running. The prefix/postfix return quickly while inactive, but every managed ECS update still crosses the Harmony patch boundary.

This conflicts with the original operating-mode goal that full system timing is disabled during Normal Monitoring and that Harmony instrumentation is a fallback used during Deep Capture.

## Architecture

Introduce one capture-scoped managed-timing lifecycle boundary with three responsibilities:

1. refresh capture-time system metadata;
2. install and activate managed instrumentation only for Deep Capture;
3. stop/unpatch instrumentation when Deep Capture ends or is interrupted.

The runtime remains fail-open. If catalog refresh or Harmony instrumentation fails, the capture continues with marker timing and other collectors. A warning records the unavailable managed path.

### Capture start

When a capture transitions into `DeepCapture`:

1. refresh the System Catalog once;
2. attempt to install Harmony managed instrumentation;
3. if installation succeeds, start a fresh `ManagedSystemTimingAccumulator`;
4. if installation fails, keep capture running and retain the failure reason for diagnostics.

No reflection/catalog refresh runs every frame.

### Capture end

When Deep Capture transitions to PostBuffer, Monitoring, interruption, or shutdown:

1. stop the managed accumulator and build its snapshot;
2. unpatch only `CS2RuntimeProfiler.ManagedSystemTiming` instrumentation;
3. retain the managed snapshot until the corresponding `CaptureSession` finalizes;
4. merge it with Full marker timing, with Full winning per system.

Shutdown is allowed to discard an incomplete capture after safely unpatching; it must not leave profiler patches installed.

## Managed statistics model

Replace the current per-system queue-only model with a per-system streaming state:

- exact `CallCount` across the whole Deep Capture;
- exact `TotalMilliseconds` across the whole Deep Capture;
- exact `CurrentMilliseconds` from the most recent call;
- exact `MaxMilliseconds` across the whole Deep Capture;
- exact `MeanMilliseconds = Total / CallCount`;
- bounded retained samples used only for Median/P95/P99.

The bounded distribution sample may continue using deterministic recent-value retention for now. Its limitation must not corrupt exact whole-capture counters.

`SystemMetricAggregate` / `MetricStatistics` must support constructing an aggregate from exact streaming counters plus bounded distribution statistics without pretending that the bounded sample count equals total calls.

## System Catalog refresh

A small cache/provider boundary owns the latest `IReadOnlyList<SystemDescriptor>` and can refresh it explicitly. Runtime code must refresh on capture start, not on every update.

The initial catalog may still be created in `OnCreate()` for diagnostics, but capture-time timing/attribution must use the refreshed snapshot.

A refresh failure keeps the last known good catalog and adds a capture warning. It must never replace a valid catalog with an empty/error result solely because refresh failed.

## Harmony lifecycle

`ManagedSystemTimingHarmonyInstrumentation` remains responsible for resolving bundled Harmony and patching `SystemBase.Update()`.

Changes:

- do not install it unconditionally in `CaptureRuntimeSystem.OnCreate()`;
- install on capture start;
- dispose/unpatch on Deep Capture end and interruption;
- repeated `TryInstall()` / `Dispose()` cycles must be safe;
- normal-monitoring path must have no CS2 Runtime Profiler Harmony patch on `SystemBase.Update()`.

The private `0Harmony.dll` packaging contract remains unchanged.

## Error handling

- Catalog refresh failure: retain previous catalog, warn capture, continue.
- Harmony install failure: managed fallback unavailable for that capture, marker path continues.
- Managed snapshot finalization failure: warn capture, unpatch instrumentation, continue with marker path.
- Unpatch failure: swallowed/logged as optional-instrumentation cleanup, consistent with current fail-open behavior.

## Testing

Pure tests must cover:

1. more samples than the bounded distribution capacity still produce exact whole-capture `Calls`, `TotalMilliseconds`, `MeanMilliseconds`, `MaxMilliseconds`, and latest `CurrentMilliseconds`;
2. bounded sample eviction affects only percentile/median input, not exact counters;
3. catalog cache refresh replaces data only on successful refresh and retains the last known good snapshot on failure;
4. capture timing lifecycle installs managed instrumentation only for active Deep Capture and disposes it when the deep phase ends/interruption occurs;
5. Full marker timing still wins over Managed fallback for the same system;
6. existing pure suite remains green.

## Non-goals

- No attempt to attribute unmanaged `ISystem` or Burst worker time without direct evidence.
- No per-frame System Catalog refresh.
- No change to gameplay systems.
- No unrelated nullable-warning cleanup or UI redesign.
