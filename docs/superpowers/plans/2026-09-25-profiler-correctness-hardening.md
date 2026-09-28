# Profiler Correctness Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct remaining measurement semantics, monitoring lifecycle, mod attribution, export fidelity, capture-selection/UI consistency and toolchain documentation issues.

**Architecture:** Keep the existing collector/controller/UI pipeline, but make evidence validity explicit at the `RecorderReading -> CaptureSession -> SystemTiming` boundary, carry source metadata through timing projection, and distinguish completed/current/selected captures in the UI projection. Avoid new background scanners or forced job completion.

**Tech Stack:** C# / .NET, Unity ProfilerRecorder, NUnit, React/TypeScript, Vitest, webpack, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-25-profiler-correctness-hardening-design.md`

## Global Constraints

- Save all changes directly to `main`; create no other branch.
- Use supplied Cities: Skylines II files for game/toolchain verification.
- Missing measurements stay unavailable; never invent zero.
- Patch ownership never transfers vanilla runtime cost to a mod.
- Keep hot-path changes small and bounded.

## Review Focus

- Recorder `Count == 0`: no marker sample, no coverage increment, no zero system timing.
- Monitoring disabled during active capture: recorder shutdown plus coherent state reset/finalization.
- Vanilla source kind survives timing/UI projection and is excluded from direct MOD totals.
- Selected-capture export/timeline never silently mixes unrelated retained captures.
- Added export fields remain privacy-sanitized and optional/backward-tolerant.

---

### Task 1: Preserve recorder validity and call counts

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/MetricSample.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMetricAggregate.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/DeepCaptureControllerTests.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/SystemMarkerTimingProjectorTests.cs`

**Interfaces:**
- Produces: `MetricSample.CallCount` (`long?`) and system `Calls` based on recorder counts.

- [x] Add failing tests proving zero-count recorder reads do not create captured samples/coverage and counts 2+3 aggregate to 5 calls.
- [x] Run Pure Core CI and verify RED for the intended missing semantics.
- [x] Add optional call-count to `MetricSample`; skip `RecorderReading.Count <= 0` in Deep Capture; aggregate actual call counts.
- [x] Run Pure Core CI and verify GREEN.
- [x] Commit each RED/GREEN checkpoint to `main`.

Evidence: RED `d3ba42be46a51ba129a259db6d3a5ec705c63fa4` / run `36085389204`; GREEN `dd65db0b596ff2b0c9984a32dcfdf46729ac49b0` / run `36085604215`.

### Task 2: Make monitoring OFF interrupt active capture coherently

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/DeepCaptureStateMachine.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/DeepCaptureControllerTests.cs`

**Interfaces:**
- Produces: controller interruption/reset operation used by runtime monitoring transition.

- [x] Add failing test: interrupt during Deep Capture finalizes partial session with warning, deactivates recorders, returns Monitoring.
- [x] Verify RED.
- [x] Implement state reset + controller interruption; wire one-shot monitoring transition in runtime system.
- [x] Verify GREEN and no lifecycle regression.

Evidence: final lifecycle wiring `9177ce519cd48468b7a507b3639eed139573265f`; Pure Core run `36085897349` PASS.

### Task 3: Preserve source kind and exclude vanilla from MOD direct cost

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/SystemTimingSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Test: `tests/CS2RuntimeProfiler.Tests/SystemMarkerTimingProjectorTests.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/UiSnapshotBuilderTests.cs`

**Interfaces:**
- Produces: `SystemTimingEntry.SourceKind` and `SystemUiRow.SourceKind`.

- [x] Add failing test with one vanilla and one mod system; only mod direct cost may appear in `snapshot.Mods`.
- [x] Verify RED.
- [x] Carry source kind through projection/serialization and filter direct MOD aggregation.
- [x] Verify Pure Core + UI GREEN.

Evidence: RED `d993662d857932debe26744376a954e19f866cd5` / run `36085954663`; GREEN `4e09a7f938011a1a5b963732cd6da3adcc87a1af` / run `36086121921`. The later runtime-UI wiring also serializes `sourceKind` explicitly.

### Task 4: Preserve export units and full system statistics

**Files:**
- Modify: `src/CS2RuntimeProfiler/Export/PerformanceReport.cs`
- Modify: `src/CS2RuntimeProfiler/Export/ProfilerReportBuilder.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/ProfilerReportBuilderTests.cs`

**Interfaces:**
- Produces: report fields for median, P99, max, total, calls and metric unit.

- [x] Add failing export test for `UnitType=TimeNanoseconds`, P99/max/calls and sanitizer-safe serialization.
- [x] Verify RED.
- [x] Map units and all supported statistics into report DTOs and sanitized copies.
- [x] Verify GREEN.

Evidence: RED `9ad7a57726f9cf072c03beb53a6953f1ccd73e93` / run `36086170499`; GREEN `e33070f7a6273a990c78ec2a968b639bcf5f7464` / run `36086351898`.

### Task 5: Separate completed/current captures and wire selection

**Files:**
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/ProfilerRoot.tsx`
- Modify: `UI/src/profiler/tabs/OverviewTab.tsx`
- Test: `tests/CS2RuntimeProfiler.Tests/CaptureUiProjectionTests.cs`
- Test: `UI/src/profiler/tabs/CoreTabs.test.tsx` or focused new test.

**Interfaces:**
- Produces: completed-only summaries, selected capture-driven timeline/system timing, manual capture state gating.

- [x] Add failing tests: active capture does not increase completed count/list; selecting older capture uses its timeline/system timing; PostBuffer manual button is disabled.
- [x] Verify RED.
- [x] Store selected capture ID in C# system, rebuild snapshot on selection, project only relevant capture timeline, keep active capture separate.
- [x] Verify Pure Core + UI GREEN.
- [x] Final-review extension: keep historical JSON export in the same capture time scope instead of mixing current-only collector snapshots.

Evidence: initial RED commits `1362ce21db4ecb188e4eb61531213cd5f8bf9824`, `0015fe361f7964d2f533046c7d502283521d04dd`; initial UI run `36088488554` PASS and Pure Core run `36088562657` PASS. Runtime serializer also preserves `sourceKind`, `medianMilliseconds`, and `totalMilliseconds`.

Final-review export extension: RED `ac3fb8a84aabd45ad5a84866154784e6f5d35e88` / `473bf17562b1277c29e3ffd76767ce53e848f3f9`, run `36089481878`; GREEN `d79733defe882c29de28474cdeacbaa547f55067`, Pure Core run `36089778625`, UI/webpack run `36089778609`. Historical export now uses the retained capture's Global sample, omits non-retained live-only Pathfinding/Domain metrics, and records capture ID/scope/timestamp.

### Task 6: Make overhead diagnostics semantically truthful

**Files:**
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/tabs/OverviewTab.tsx`
- Modify: `UI/src/profiler/tabs/DiagnosticsTab.tsx`
- Modify: `UI/src/profiler/bindings.ts`
- Test: relevant UI tests.

**Interfaces:**
- Renames UI meaning from total profiler overhead to capture/runtime measured overhead unless a true total is available.

- [x] Add failing UI/source regression test that rejects ambiguous total-overhead wording.
- [x] Verify RED.
- [x] Rename labels/diagnostics to describe measured capture-processing share; keep validation targets separate.
- [x] Verify UI GREEN.

Ruling: the existing wire/property key `profilerOverheadShare` remains for compatibility, but all user-facing labels explicitly say `キャプチャ処理負荷` and explain that it is not game-wide profiler overhead. Export key `captureOverheadShare` is also retained for schema compatibility.

Evidence: RED `0be803fbac09880919d1e57233823e3fd6143f9c` / UI run `36088653568`; GREEN `f3a068eb0c01cb0112ad35a453f84490d64a27f3` / UI run `36088777589`, Pure Core run `36088777556`.

### Task 7: Correct README toolchain PowerShell instructions

**Files:**
- Modify: `README.md`

**Interfaces:** none.

- [x] Compare supplied `Mod.props` / `Mod.targets` variable names against README.
- [x] Replace `CSII_MANAGED_PATH` with `CSII_MANAGEDPATH` and explain official toolchain prerequisites instead of implying three variables fully configure it.
- [x] Re-read rendered Markdown/source for command consistency.

Evidence: supplied `CS2-managed-reference-fix(1).zip` shows `Mod.props` reading User-scope `CSII_MANAGEDPATH` and related toolchain variables, and `Mod.targets` deploying via `CSII_LOCALMODSPATH\$(TargetName)`. README commit: `6566dc845f968f74ea854e02d579ff02123f9f7a`.

### Task 8: Final verification and validation record

**Files:**
- Modify: `docs/validation/runtime-validation.md`
- Modify: this plan (mark completed tasks/evidence).

- [x] Run/inspect latest Pure Core Tests on the final code-bearing HEAD.
- [x] Run/inspect latest UI Tests and webpack production build on the final code-bearing HEAD.
- [x] Review diff from `a756b1fb4435d04af2ec19de4460f29c20983e04` through the hardening range for accidental scope expansion.
- [x] Confirm no branch other than `main` was created by this work.
- [x] Keep actual in-game scenarios explicitly UNVERIFIED until executed in Cities: Skylines II.

Evidence: final code-bearing HEAD `d79733defe882c29de28474cdeacbaa547f55067` passes Pure Core (`36089778625`) and UI tests + production webpack build (`36089778609`). Diff review from `a756b1fb4435d04af2ec19de4460f29c20983e04` is restricted to profiler core/UI/tests/docs/README work. Repository branch search returned only `main`. Runtime validation remains NOT RUN / UNVERIFIED and is recorded in `docs/validation/runtime-validation.md`.

## Remaining separately tracked item

UI CI currently reports 5 npm dependency vulnerabilities (3 moderate, 1 high, 1 critical) during install. This plan does not apply an unreviewed `npm audit fix --force`; package/advisory-level analysis should be performed as a separate dependency-maintenance pass before choosing upgrades that could alter the CS2 UI toolchain.
