# Runtime Capture Diagnostics Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Deep Capture system attribution and diagnostics reflect verified runtime evidence while preserving current report compatibility.

**Architecture:** Add exact runtime profiler marker identity to system descriptors, separate activation/sample coverage counters, retain trigger and profiler-memory evidence in each capture, and expose those values through the existing UI/report projection. Keep changes additive and conservative: no fuzzy attribution and no gameplay writes.

**Tech Stack:** C#/.NET, Unity Entities/Unity Profiling APIs, NUnit, existing TypeScript UI tests.

**Spec:** `docs/superpowers/specs/2026-09-27-runtime-capture-diagnostics-hardening-design.md`

## Global Constraints

- Work directly on `main`; do not create another branch.
- Do not change unrelated UI/gameplay behavior.
- Preserve schema version 2 and existing report fields.
- Do not use short-name or heuristic system attribution.
- Maintain read-only profiling behavior.

## Review Focus

- A catalogued CLR system type that is not live in the current World must not receive a guessed runtime marker name.
- Duplicate/ambiguous marker names must remain unattributed.
- Failed recorder activation must increase attempted but not activated/sample coverage.
- Missing profiler-memory recorder must not change capture behavior.
- Historical exports must retain the capture's own trigger/configuration evidence rather than current live values.

---

### Task 1: Exact runtime ECS marker identity

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/SystemDescriptor.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/ProfilerCatalog.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/SystemMarkerTimingProjectorTests.cs`
- Test: add/update profiler catalog tests as appropriate

**Interfaces:**
- Produces: `SystemDescriptor.ProfilerMarkerName : string`
- Consumes: live Unity Entities World/system identity from runtime catalog discovery.

- [ ] Write failing tests proving an exact runtime marker name is preferred and a nonmatching CLR full name is not guessed.
- [ ] Verify RED.
- [ ] Add optional exact marker identity to `SystemDescriptor` and derive it only from live systems.
- [ ] Update projection/diagnostics to match by exact marker identity first and preserve strict fallback compatibility.
- [ ] Run full C# tests and verify GREEN.
- [ ] Commit.

### Task 2: Marker coverage semantics

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiModels.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/Export/*` as required
- Test: `tests/CS2RuntimeProfiler.Tests/CaptureSessionTests.cs`
- Test: Deep Capture controller/projection tests

**Interfaces:**
- Produces: discovered/attempted/activated/sampled counts and ratios; `Captured` stays an alias for sampled.

- [ ] Write failing tests for activation success/failure and sampled coverage.
- [ ] Verify RED.
- [ ] Track attempted and activated marker IDs separately from sampled IDs.
- [ ] Add additive UI/report fields and ratios while retaining legacy fields.
- [ ] Run full C# tests and verify GREEN.
- [ ] Commit.

### Task 3: Trigger audit evidence

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiModels.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Test: capture/UI projection tests

**Interfaces:**
- Produces: optional trigger selected speed, actual speed, efficiency; timeline `selectedSpeed` series.

- [ ] Write failing tests for automatic/manual trigger snapshots and selected-speed timeline output.
- [ ] Verify RED.
- [ ] Store trigger evidence from the current global sample when capture begins.
- [ ] Export/project trigger fields and timeline selected speed.
- [ ] Run full C# tests and verify GREEN.
- [ ] Commit.

### Task 4: Profiler-memory growth guard

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Test: controller/session tests

**Interfaces:**
- Consumes: profiler-used-memory bytes when available from current global recorder readings.
- Produces: baseline/peak/delta bytes and warning/degradation at >= 128 MiB growth.

- [ ] Write failing tests for unavailable metric, sub-threshold growth, and >=128 MiB growth.
- [ ] Verify RED.
- [ ] Record baseline/peak memory and invoke existing degradation path once per threshold breach episode.
- [ ] Add one capture warning describing the memory growth and mitigation.
- [ ] Run full C# tests and verify GREEN.
- [ ] Commit.

### Task 5: Effective configuration export and concise completion log

**Files:**
- Modify: `src/CS2RuntimeProfiler/Export/ProfilerReportBuilder.cs`
- Modify: report model/input files as required
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Test: report builder tests and capture completion tests

**Interfaces:**
- Produces: additive effective capture settings in `captureConfig`; one completion log message per capture.

- [ ] Write failing tests for exported effective settings and deterministic completion summary formatting.
- [ ] Verify RED.
- [ ] Add settings to report input/projection without changing schema version.
- [ ] Add a pure formatter for completion summaries and log each newly completed capture once.
- [ ] Run full C# tests and verify GREEN.
- [ ] Commit.

### Task 6: Final verification and focused review

**Files:** none unless review finds an Important/Critical issue.

- [ ] Run GitHub Actions C# and UI suites against final `main` head.
- [ ] Review the complete diff against the spec and the five Review Focus items.
- [ ] For any Important/Critical finding, add a failing regression test, verify RED, fix, verify GREEN, and rerun both suites.
- [ ] Report final main commit and any deferred minor findings.