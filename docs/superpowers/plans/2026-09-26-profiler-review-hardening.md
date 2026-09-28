# Profiler Review Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct capability reporting, recover safe domain/pathfinding metrics, and make zero-result system timing captures explain why they failed.

**Architecture:** Keep the existing read-only collectors and export schema shape. Improve evidence at the existing abstraction boundaries rather than introducing new runtime subsystems: metric availability is evaluated in the report builder, service vehicles are one registered ECS query, collection length probing stays inside `PathfindingCollector`, and timing diagnostics are produced by the pure projection/finalization layer.

**Tech Stack:** C#/.NET, NUnit, Unity Entities/CS2 managed APIs, TypeScript/Vitest/Webpack for final UI verification.

**Spec:** `docs/superpowers/specs/2026-09-26-profiler-review-hardening-design.md`

## Global Constraints

- Source-of-truth for game API assumptions is the supplied current Cities: Skylines II managed-reference archive.
- Never fabricate unavailable values as zero.
- Keep timing attribution conservative; no short-name system matching.
- Do not implement unverified pathfinding request/result rates.
- Commit directly to `main`; do not create another branch.

## Review Focus

- A non-empty pathfinding/domain list containing only unavailable rows must still report the capability as unavailable.
- A runtime queue container exposing `Length` but not `ICollection` must be countable without exceptions.
- The service-vehicle aggregate must count matching entities once and exclude `Deleted`/`Temp`.
- System timing diagnostics must distinguish no TimeNanoseconds markers, ambiguous matches, and missing captured samples.
- Existing available metric groups, queue types, and successful system timing projection must remain unchanged.

---

### Task 1: Truthful report capabilities

**Files:**
- Modify: `tests/CS2RuntimeProfiler.Tests/ProfilerReportBuilderTests.cs`
- Modify: `src/CS2RuntimeProfiler/Export/ProfilerReportBuilder.cs`

**Interfaces:**
- Consumes: `UiMetricRow.Availability` and existing `ReportNamedValue` capability entries.
- Produces: capability values that reflect actual available evidence.

- [ ] **Step 1: Write the failing test**

Add `Unavailable_only_metric_groups_are_not_reported_as_available` asserting that non-empty Pathfinding/Domain arrays whose rows are all `Unavailable` yield capability value `unavailable`, while a group with at least one `Available` row yields `available`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`
Expected: FAIL because current capability logic checks only list count.

- [ ] **Step 3: Implement availability-aware capability checks**

Add a focused helper in `ProfilerReportBuilder` that returns true only if any `ReportMetric.Availability` equals `Available` ordinally. Keep system timing count semantics unchanged.

- [ ] **Step 4: Run the Pure Core suite**

Expected: PASS.

### Task 2: Recover safe domain/pathfinding counts

**Files:**
- Modify: `tests/CS2RuntimeProfiler.Tests/PathfindingCollectorTests.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/EntityMetricsCollectorTests.cs`
- Modify: `src/CS2RuntimeProfiler/Collectors/PathfindingCollector.cs`
- Modify: `src/CS2RuntimeProfiler/Collectors/EntityMetricsCollector.cs`
- Modify: `src/CS2RuntimeProfiler/Collectors/DomainMetricsSystem.cs`

**Interfaces:**
- Consumes: existing `IEntityDomainCountSource.TryGetCount(string, out int, out string)`.
- Produces: `serviceVehicles` through source key `Game.Vehicles.ServiceVehicleUnion`; collection counts from `Count`/`Length` compatible containers.

- [ ] **Step 1: Write failing pathfinding test**

Add a fake queue field whose value exposes integer `Length` but does not implement `ICollection`; assert `actionTypeQueue` becomes available with that length.

- [ ] **Step 2: Write failing entity-domain test**

Configure the fake source with `Game.Vehicles.ServiceVehicleUnion` and assert `serviceVehicles` is available/Indirect with the supplied count.

- [ ] **Step 3: Run Pure Core tests to verify RED**

Expected: both new assertions fail against current behavior.

- [ ] **Step 4: Implement minimal collector changes**

Use `TryGetCollectionLength` in `TryReadCollectionCount`. Add `serviceVehicles -> Game.Vehicles.ServiceVehicleUnion` to the verified domain map and remove the hard-coded unavailable row.

- [ ] **Step 5: Register the runtime aggregate query**

In `DomainMetricsSystem`, register `Game.Vehicles.ServiceVehicleUnion` as an `Any` query across `Ambulance`, `Hearse`, `MaintenanceVehicle`, `FireEngine`, `GarbageTruck`, `PoliceCar`, `PostVan`, and `PrisonerTransport`, with the same `Deleted`/`Temp` exclusions as existing queries.

- [ ] **Step 6: Run Pure Core tests**

Expected: PASS. Note that the game-dependent file still requires official-toolchain/in-game build verification.

### Task 3: Explain zero-result system timing

**Files:**
- Modify: `tests/CS2RuntimeProfiler.Tests/CaptureSystemTimingFinalizerTests.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSystemTimingFinalizer.cs`

**Interfaces:**
- Produces: `SystemTimingProjectionDiagnostics` from the same full-name matching rules used for projection.
- Consumes: diagnostics in the finalizer to build one capture warning when zero timing rows are produced.

- [ ] **Step 1: Write the failing test**

Extend the zero-result finalizer test with recorder/system fixtures covering non-time markers, an ambiguous full-name match, and a unique match without captured samples. Assert the warning contains the detailed stage counts.

- [ ] **Step 2: Run Pure Core tests to verify RED**

Expected: FAIL because the current warning has only aggregate counts.

- [ ] **Step 3: Implement shared projection diagnostics**

Add a diagnostics value type and a projector method that calculates counts using the exact existing full-name matching rule. Avoid short-name matching and avoid changing successful projection output.

- [ ] **Step 4: Use diagnostics in finalizer warning**

When `timing.Systems.Count == 0`, report catalog systems, all markers, TimeNanoseconds markers, unique matches, ambiguous matches, unique matches without captured samples, and captured coverage.

- [ ] **Step 5: Run Pure Core tests**

Expected: PASS.

### Task 4: Documentation and final verification

**Files:**
- Modify: `README.md`
- Modify: `docs/validation/runtime-validation.md`

**Interfaces:**
- None.

- [ ] **Step 1: Update stale documentation**

Document `yyyy-MM-dd_HHmmss_fff` export names plus collision suffix behavior. Replace the stale npm-vulnerability OPEN note with the current CI audit evidence and correct the validation text that still refers to a shared recorder manager.

- [ ] **Step 2: Run fresh verification**

Run Pure Core tests; run `npm test` and `npm run build` in `UI` through GitHub Actions.
Expected: Pure Core 0 failures, UI 0 failures, webpack success.

- [ ] **Step 3: Inspect final diff and repository state**

Verify all changes are on `main`, no other branch was created, and no unrelated files were modified.
