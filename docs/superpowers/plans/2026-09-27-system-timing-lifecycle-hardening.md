# System Timing Lifecycle Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make managed System Timing capture-scoped, refresh system metadata at capture start, and preserve exact whole-capture managed totals while keeping bounded distribution memory.

**Architecture:** Separate exact streaming accounting from bounded percentile samples, introduce an explicit last-known-good System Catalog cache, then move Harmony instrumentation install/uninstall to Deep Capture lifecycle boundaries. Full profiler-marker timing remains authoritative and managed timing remains a fallback only.

**Tech Stack:** C# 9 / .NET Framework 4.8 runtime, .NET 8 NUnit pure tests, Unity Entities, Lib.Harmony 2.2.2.

**Spec:** `docs/superpowers/specs/2026-09-27-system-timing-lifecycle-hardening-design.md`

## Global Constraints

- Work directly on existing `main`; do not create another branch.
- Do not redistribute Cities: Skylines II game DLLs.
- Preserve Full > Managed timing precedence.
- Do not attribute unmanaged/Burst worker cost without direct evidence.
- Runtime failures in optional timing instrumentation must fail open.
- Do not fix unrelated nullable warnings or other out-of-scope findings.

## Review Focus

- A managed system executing more calls than the retained percentile-sample capacity must still report exact total/calls/mean/max/current.
- A failed catalog refresh must retain the last known good catalog rather than erase timing ownership metadata.
- Repeated Deep Capture sessions must support install → unpatch → reinstall without stale bridge state.
- Monitoring disable/interruption during Deep Capture must stop the managed bridge and unpatch instrumentation.
- Marker Full timing for a system must continue to override Managed fallback after lifecycle changes.

---

### Task 1: Exact streaming managed timing statistics

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/ManagedSystemTimingAccumulator.cs`
- Modify: `src/CS2RuntimeProfiler/Core/MetricStatistics.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMetricAggregate.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/ManagedSystemTimingFallbackTests.cs`

**Interfaces:**
- Produces: managed `SystemMetricAggregate` whose current/mean/max/total/calls represent the full capture while median/P95/P99 use bounded retained samples.

- [x] **Step 1: Write failing tests**
  - Add a test recording more than `maxSamplesPerSystem` values and assert exact `Calls`, `TotalMilliseconds`, `MeanMilliseconds`, `MaxMilliseconds`, and latest current value across all calls.
  - Assert bounded sample eviction does not change exact counters.

- [x] **Step 2: Verify RED**
  - Run `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`.
  - Expected: new exact-counter assertions fail because current implementation derives all statistics from retained samples.

- [x] **Step 3: Implement minimal streaming summary support**
  - Track exact per-system count, total, max, current alongside the bounded queue.
  - Add the minimal statistics/aggregate factory needed to combine exact counters with bounded percentile samples.

- [x] **Step 4: Verify GREEN**
  - Run the full pure test suite; expected 0 failures.

- [x] **Step 5: Commit**
  - Commit Task 1 changes to `main`.

### Task 2: Last-known-good System Catalog refresh

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/SystemCatalogCache.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj`
- Create: `tests/CS2RuntimeProfiler.Tests/SystemCatalogCacheTests.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`

**Interfaces:**
- Produces: `SystemCatalogCache` with current snapshot and explicit refresh operation that replaces data only after a successful provider call.
- Consumes: `ProfilerCatalog(world: World).Discover()` as runtime provider.

- [x] **Step 1: Write failing cache tests**
  - Successful refresh publishes a new snapshot.
  - Provider exception leaves prior snapshot unchanged and returns failure information.

- [x] **Step 2: Verify RED**
  - Run full pure tests; expected compile failure because `SystemCatalogCache` does not exist.

- [x] **Step 3: Implement cache and runtime refresh boundary**
  - Add cache class with last-known-good semantics.
  - Initialize it in `OnCreate()`.
  - Refresh once whenever a capture newly enters Deep Capture, before managed accumulation begins.
  - Add capture warning on refresh failure while retaining previous snapshot.

- [x] **Step 4: Verify GREEN**
  - Run full pure tests; expected 0 failures.

- [x] **Step 5: Commit**
  - Commit Task 2 changes to `main`.

### Task 3: Capture-scoped managed instrumentation lifecycle

**Files:**
- Create: `src/CS2RuntimeProfiler/Profiling/ManagedSystemTimingCaptureLifecycle.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj`
- Create: `tests/CS2RuntimeProfiler.Tests/ManagedSystemTimingCaptureLifecycleTests.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/ManagedSystemTimingHarmonyInstrumentation.cs` only if needed to support repeatable install/dispose cleanly.

**Interfaces:**
- Produces: capture-scoped lifecycle with start, finish, and abort operations.
- Consumes: instrumentation install/dispose and `ManagedSystemTimingBridge.BeginCapture/EndCapture/AbortCapture`.

- [x] **Step 1: Write failing lifecycle tests**
  - Inactive lifecycle has no installed instrumentation.
  - Start installs instrumentation before beginning accumulation.
  - Finish returns a managed snapshot and disposes instrumentation.
  - Abort clears accumulation and disposes instrumentation.
  - Repeated start/finish cycles are supported.

- [x] **Step 2: Verify RED**
  - Run full pure tests; expected compile failure because lifecycle type does not exist.

- [x] **Step 3: Implement lifecycle and wire runtime**
  - Remove unconditional Harmony install from `CaptureRuntimeSystem.OnCreate()`.
  - Start managed lifecycle only when entering Deep Capture.
  - Finish it when leaving Deep Capture and retain the snapshot for capture finalization.
  - Abort it when monitoring is disabled or system is destroyed.
  - Keep marker capture and capture finalization fail-open if managed instrumentation cannot start.

- [x] **Step 4: Verify GREEN**
  - Run full pure tests; expected 0 failures.

- [x] **Step 5: Commit**
  - Commit Task 3 changes to `main`.

### Task 4: Whole-change review and verification

**Files:**
- Review all files changed by Tasks 1–3.
- Modify only Critical/Important findings discovered in this review, each with RED→GREEN tests.

- [x] **Step 1: Re-read the design and inspect the final diff**
  - Confirm normal monitoring no longer installs the SystemBase Harmony patch.
  - Confirm capture-time catalog refresh uses last-known-good semantics.
  - Confirm exact whole-capture managed totals cannot be truncated by percentile sample capacity.

- [x] **Step 2: Run full pure suite**
  - `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`
  - Expected: 0 failures.

- [x] **Step 3: Check GitHub Actions for final HEAD**
  - Pure Core Tests must complete successfully.
  - Existing unrelated warnings are reported but not fixed.

- [x] **Step 4: Report runtime-validation boundary**
  - State clearly that actual CS2 runtime validation still requires a new in-game log/report after installing the new build.
