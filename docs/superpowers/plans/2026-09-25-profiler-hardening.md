# CS2 Runtime Profiler Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Reduce profiler self-overhead and long-session memory growth while fixing capture lifecycle edge cases without changing the user-facing profiling model.

**Architecture:** Keep the existing collector/controller/UI architecture. Fix lifecycle boundaries in `DeepCaptureController` and `GlobalMetricsCollector`, avoid repeated marker snapshot materialization, make adaptive overhead degradation hysteretic and per-capture, bound completed capture retention, and split lightweight HUD snapshot delivery from full panel snapshot delivery.

**Tech Stack:** C#/.NET, Unity Entities/ProfilerRecorder, Cities: Skylines II GameSystemBase, React/TypeScript UI, NUnit, Vitest, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-24-cs2-runtime-profiler-design.md`

## Global Constraints

- Work only on `main`; do not create another branch.
- Use the supplied CS2 managed reference files as the authoritative API reference for runtime-facing changes.
- Preserve Japanese-first UI behavior.
- Preserve existing public UI binding names unless a task explicitly adds a new lightweight binding.
- Every behavior change follows RED → GREEN TDD where the test harness permits direct behavioral execution.
- Do not make captured data less accurate unless the existing profiler-overhead ceiling requires adaptive degradation.

## Review Focus

1. A large time jump that crosses `DeepCapture -> PostBuffer -> Cooldown` in one observation must still finalize exactly once.
2. Disabling monitoring during either normal sampling or a deep capture must deactivate profiler recorders and re-enable only the normal set after monitoring resumes.
3. Marker sample projection must not repeatedly clone the complete capture dictionary during one projection/build operation.
4. A transient overhead spike must not permanently collapse future captures to tiny recorder batches or high sample stride.
5. Long sessions and a closed profiler panel must have bounded capture memory and avoid rebuilding full-detail UI snapshots unnecessarily.

---

### Task 1: Make capture finalization transition-safe

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj`
- Create: `tests/CS2RuntimeProfiler.Tests/DeepCaptureControllerTests.cs`

- [x] Add the controller source to the pure test project and add a fake recorder backend test fixture.
- [x] Add a failing regression test where one `Observe()` jumps from DeepCapture through PostBuffer into Cooldown and assert the session is finalized exactly once.
- [x] Confirm the new test fails on current main (`36017908590`).
- [x] Change controller lifecycle handling to finalize based on resulting state/lifecycle ownership rather than requiring the exact `PostBuffer -> Cooldown` adjacent transition.
- [x] Run the focused test and full pure-core suite to green (`36018048295`).
- [x] Commit the fix (`e660ed38f90c9457a8bd5650e9116b837f025b01`).

### Task 2: Stop recorder work while monitoring is disabled

**Files:**
- Modify: `src/CS2RuntimeProfiler/Collectors/GlobalMetricsCollector.cs`
- Create: `src/CS2RuntimeProfiler/Core/MonitoringLifecycleGate.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/MonitoringLifecycleGateTests.cs`

- [x] Add a failing test for the extracted monitoring transition state helper.
- [x] Confirm RED (`36018253472`).
- [x] Implement one-shot disable/enable transition handling so the shared recorder manager is deactivated while monitoring is disabled and the normal recorder set is restored once on re-enable.
- [x] Confirm GREEN and run the full pure suite (`36018414141`).
- [x] Commit the runtime wiring (`ef5c7fa54556981702306f9b8b6e9176bde9ab1e`).

### Task 3: Eliminate repeated marker snapshot materialization

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Modify: relevant NUnit tests.

- [x] Add a failing behavior/API test proving per-marker lookup can occur without requesting the entire dictionary snapshot.
- [x] Confirm RED (`36018556742`).
- [x] Add `TryGetMarkerSamples` and update system timing projection to use narrow per-series snapshots.
- [x] Confirm GREEN and full suite (`36018865062`).
- [x] Commit (`1e5972342db4691b1a68edfb763de7981eeb2ae2`, `6f70b002879d60aa795713d20a5c5d264a61f0e3`).

### Task 4: Add hysteresis and per-capture reset to adaptive overhead control

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/DeepCaptureControllerTests.cs`

- [x] Add failing tests for normal-monitoring spikes, a single transient active-capture spike, sustained spikes, and a new capture after degradation.
- [x] Confirm RED (`36019012978`).
- [x] Require three consecutive over-ceiling observations during an active capture and reset adaptive fields in `BeginCapture()`.
- [x] Confirm GREEN and full suite (`36019152958`).
- [x] Commit (`b7386dd9dbefd74d948ce0b15ffddf5c799646bd`).

### Task 5: Bound completed capture retention

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/Core/CaptureCompletionTimingProcessor.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Modify: relevant tests.

- [x] Add failing tests for timing processing across front eviction and for a completed-history retention cap.
- [x] Confirm RED (`36019314362`, `36019518819`).
- [x] Replace index-only timing processing with capture-identity-aware processing compatible with eviction.
- [x] Bound completed capture history to the newest 20 sessions by default.
- [x] Route timing-projection failures to the actual processed capture rather than treating cumulative count as a list index.
- [x] Confirm GREEN and full suite (`36019647891`).
- [x] Commit (`f5313ee80f09b14c1ee8bf25b0bdd0551a7e180b`, `0be7cd16d2ff3dea429d51fd348823d4409cc277`, `f473a48bbc28dfc38bfd64eec02b6f20c4b5a225`).

### Task 6: Avoid full-detail UI rebuilds while panel is closed

**Files:**
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Modify: `UI/src/index.tsx`
- Modify: `UI/src/profiler/components/ProfilerHud.tsx`
- Add/modify UI regression tests and CS2 API test stub.

- [x] Add a dedicated `hudSnapshot` contract containing only selected speed, actual speed, capture state and Deep Capture state.
- [x] Move the always-visible HUD to the lightweight binding.
- [x] Update only `hudSnapshot` at the periodic cadence while the full panel is closed.
- [x] Refresh the full snapshot immediately when the panel opens.
- [x] Add source-level regression checks for closed-panel and panel-open refresh semantics.
- [x] Add a `cs2/api` Vitest stub so binding-level tests execute in CI. The first test-only run (`36019966835`) failed in the harness before reaching product behavior and is not treated as a behavioral RED result.
- [x] Run UI tests, production webpack build and Pure Core Tests to green (`36020955605`, `36020956448`).
- [x] Commit the runtime split (`d03b68a06a02bac1fa41496a8f13e5e4287277a2`) and UI/test wiring through `2f1ad4a7de474704613844ef0304acdd408ff28a`.

### Task 7: Final verification and documentation

**Files:**
- Modify: `docs/validation/runtime-validation.md`.
- Update this plan's execution notes/checkmarks.

- [x] Run all pure core tests on the final code state before documentation-only completion commits (`36020956448`).
- [x] Run all UI Vitest tests and production build (`36020955605`).
- [x] Inspect GitHub Actions for the main-branch verification runs.
- [x] Re-check runtime-facing type assumptions against the supplied CS2 managed references; `Colossal.UI.Binding.dll` contains `RawValueBinding` and `IJsonWriter`, and no new game API family was introduced.
- [x] Review the complete diff from baseline `a3ee48dab361b39a7cfc7cdc046eae9ab29ab860` for lifecycle regressions, profiler overhead, bounded storage, exception routing and UI↔C# binding consistency.
- [x] Record final automated evidence and preserve in-game/full-Windows-build checks as explicitly unverified in `docs/validation/runtime-validation.md`.

## Execution notes

- No new worktree or branch was created because the project instruction explicitly requires all changes to be saved on `main`. Recovery was provided by small main-branch commits and this plan document.
- Baseline reviewed: `a3ee48dab361b39a7cfc7cdc046eae9ab29ab860`.
- Full official Windows CS2 mod compilation and real in-game performance measurements remain **NOT RUN / UNVERIFIED**; automated CI does not claim otherwise.
- Managed reference archive inspected locally: `CS2-managed-reference-fix(1).zip` contains the expected `Game.dll`, `Unity.Entities.dll`, `Unity.Profiling.Core.dll`, `Colossal.UI.Binding.dll` and related references.
