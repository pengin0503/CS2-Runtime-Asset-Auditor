# Runtime Profiler Issues 15–22 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve Issues #15–#22 without removing the permitted Harmony managed timing fallback, while keeping measurements conservative and the in-game UI Gameface-compatible.

**Architecture:** Keep the existing capture/advisor/UI structure. Tighten runtime safety at existing boundaries: verified live-system marker matching, capture degradation/abort policy, pause gating, window-based Advisor evidence, collision-aware Timeline labels, Japanese user-facing Advisor text, and durable capture-completion diagnostics. Do not invent unavailable metrics or attribute Job/Burst work to managed timing.

**Tech Stack:** C#/.NET 4.8 runtime mod, .NET 8 NUnit pure tests, React/TypeScript/Vitest UI, Cities: Skylines II Game/Unity managed APIs.

**Spec:** GitHub Issues #15, #16, #17, #18, #19, #20, #21 and #22 in `pengin0503/CS2-Runtime-Profiler`.

## Global Constraints

- Work only on `main`; do not create feature branches.
- Harmony is allowed and the managed `SystemBase.Update` fallback may remain.
- Game-managed DLLs supplied with the project are the source of truth for runtime members.
- Never guess unavailable profiler/system metrics.
- Preserve existing exported schema fields unless adding clarifying data is backward-compatible.
- Keep user-facing UI primarily Japanese and compatible with Coherent Gameface.
- Do not make unrelated refactors.

## Review Focus

- Paused/loading simulation must not automatically trigger low-efficiency capture, while manual capture remains available.
- Repeated overhead or memory pressure must converge to bounded capture cost rather than degrade forever.
- Managed SystemBase timing must never be presented as Job/Burst worker timing.
- Advisor CPU diagnosis must not depend on one terminal sample or one transient trigger sample alone.
- Timeline/Advisor labels must remain distinguishable at high series counts and UI scale.

---

### Task 1: Automatic-capture runtime state gate (#18)

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/RuntimeGameStateProbe.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Create: `src/CS2RuntimeProfiler/Core/AutomaticCapturePolicy.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/AutomaticCapturePolicyTests.cs`

**Interfaces:**
- Produces: `AutomaticCapturePolicy.IsAllowed(bool loading, bool simulationPaused)`.
- Runtime probe supplies loading and `SimulationSystem.simulationPaused` state; fail-open only when state discovery itself is unavailable.

- [ ] Write tests covering loading, paused, normal, and manual-capture independence.
- [ ] Verify RED in Pure Core Tests.
- [ ] Implement the pure policy and runtime simulation-pause probe using the game DLL member `simulationPaused`.
- [ ] Verify Pure Core Tests GREEN.
- [ ] Commit.

### Task 2: Deep Capture overhead and memory safety (#16, #17)

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/DeepCaptureControllerTests.cs`

**Interfaces:**
- Produces: bounded staged degradation for repeated overhead/memory breaches and an early-finalization path when pressure remains extreme.
- Existing `profilerOverheadLimit` remains the configured soft ceiling; exported maximum remains evidence, not a claim of total profiler cost.

- [ ] Add failing tests for repeated memory threshold crossings, hard memory abort, and persistent-overhead abort.
- [ ] Verify RED.
- [ ] Replace one-shot memory degradation with staged 128 MiB steps; abort at a conservative hard threshold after recording a warning.
- [ ] Add persistent-overhead safety stop after repeated degradation actions; reset state between captures.
- [ ] Add warning text clarifying that controller overhead does not include all managed-instrumentation cost when fallback is used.
- [ ] Verify full Pure Core Tests GREEN.
- [ ] Commit.

### Task 3: Runtime-safe system marker matching and coverage diagnostics (#15)

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/ProfilerCatalog.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemMarkerTimingProjector.cs`
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSystemTimingFinalizer.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/SystemMarkerTimingProjectorTests.cs`
- Test: `tests/CS2RuntimeProfiler.Tests/CaptureSystemTimingFinalizerTests.cs`

**Interfaces:**
- Produces: exact full-type fallback matching only for systems confirmed live in the current World when the runtime marker-name API does not yield a name.
- No short-name guessing; inactive catalog entries remain ineligible for fallback attribution.

- [ ] Add RED tests for verified-live exact full-name fallback and inactive-system rejection.
- [ ] Verify RED.
- [ ] Separate “live system verified” from “marker name available” in catalog discovery and allow strict full-name fallback only for the former.
- [ ] Expand diagnostics/warnings to state native marker coverage versus managed fallback coverage clearly.
- [ ] Verify Pure Core Tests GREEN.
- [ ] Commit.

### Task 4: Advisor evidence stability and Japanese output (#19, #21)

**Files:**
- Modify: `src/CS2RuntimeProfiler/Core/Advisor/CaptureAdvisorEvidenceProjector.cs`
- Modify: `src/CS2RuntimeProfiler/Core/Advisor/BottleneckClassifier.cs`
- Modify: `src/CS2RuntimeProfiler/Core/Advisor/RecommendationEngine.cs`
- Modify: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
- Modify: `UI/src/profiler/profiler.module.scss`
- Test: `tests/CS2RuntimeProfiler.Tests/BottleneckClassifierTests.cs`
- Test: Advisor projector tests (existing or new)
- Test: `UI/src/profiler/performanceAdvisorRegression.test.tsx`

**Interfaces:**
- Produces: post-trigger window median `simulation.efficiency`, separate trigger evidence, and Medium severity recovery observation when trigger was low but the measured window recovered.
- UI maps enum/state strings to Japanese labels while export may retain stable enum values.

- [ ] Add RED tests for low trigger + recovered window, terminal-only low sample, and persistent low window.
- [ ] Add UI RED tests for Japanese priority/confidence/status/apply labels and stable one-line group headings.
- [ ] Verify Pure/UI RED.
- [ ] Implement window aggregation and classifier distinction between sustained low and recovered trigger.
- [ ] Translate recommendation/classifier user-facing rationale and UI enum labels.
- [ ] Add Gameface-safe nowrap/layout rules for Advisor group headings.
- [ ] Verify Pure/UI suites GREEN.
- [ ] Commit.

### Task 5: Collision-aware Timeline labels (#20)

**Files:**
- Modify: `UI/src/profiler/tabs/TimelineTab.tsx`
- Test: Timeline Vitest coverage (existing or new)

**Interfaces:**
- Produces: collision-aware labels that include source/category only when needed; tooltip and selected-point labels use the same resolver.

- [ ] Add RED test with recorder/marker series sharing the same short name.
- [ ] Verify UI RED.
- [ ] Implement deterministic collision resolution without depending on color alone.
- [ ] Verify UI suite GREEN.
- [ ] Commit.

### Task 6: Durable capture completion diagnostics (#22)

**Files:**
- Modify: `src/CS2RuntimeProfiler/Profiling/CaptureRuntimeSystem.cs`
- Create/Modify: pure logging helper under `src/CS2RuntimeProfiler/Core/`
- Test: `tests/CS2RuntimeProfiler.Tests/CaptureConfigurationAndLoggingTests.cs`

**Interfaces:**
- Produces: one capture-completion message sent through the existing logger plus best-effort flush, with logging failures isolated from capture finalization.

- [ ] Add RED test proving the diagnostic dispatch invokes both write and flush hooks and remains non-throwing if flush fails.
- [ ] Verify RED.
- [ ] Implement helper; use the existing `Mod.Log` and a best-effort runtime `Flush` invocation rather than introducing a second log file.
- [ ] Verify Pure Core Tests GREEN.
- [ ] Commit.

### Task 7: Final verification and issue closure

**Files:**
- No product-code changes unless verification finds a Critical/Important regression.

- [ ] Verify latest `main` Pure Core Tests workflow is green.
- [ ] Verify latest `main` UI Tests workflow is green.
- [ ] Review the combined diff against Issues #15–#22 and the supplied runtime evidence.
- [ ] If a Critical/Important finding exists, fix it with RED→GREEN and rerun suites.
- [ ] Comment on and close #15–#22 with the implementing commit(s) and any runtime-only follow-up that still requires an in-game validation run.
