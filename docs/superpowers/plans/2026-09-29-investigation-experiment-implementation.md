# Guided Investigation Experiment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the existing Performance Advisor baseline/follow-up comparison into a user-guided, one-setting investigation experiment with exact follow-up capture correlation, contamination detection, Keep/Undo completion, localization, and unified export.

**Architecture:** Keep all deterministic experiment state and eligibility rules in pure `Core/Advisor/Experiment` classes so they are covered by the existing .NET 8 pure-test project. `AdvisorSystem` only bridges those rules to the existing `AdvisorSettingOperations`, `CaptureRuntimeSystem`, session registry, and evidence projector. `CaptureRuntimeSystem.RequestManualCapture()` returns the exact newly-created `CaptureSession` so experiments never infer their follow-up from “latest capture.”

**Tech Stack:** C#, .NET 8 pure tests, CS2 net48 mod project, NUnit, TypeScript, React 18, Vitest, Colossal UI bindings.

**Spec:** `docs/superpowers/specs/2026-09-29-investigation-experiment-design.md`

## Global Constraints

- Execute later in ChatGPT Work using Native / `superpowers:executing-plans`.
- Work directly against `pengin0503/CS2-Runtime-Asset-Auditor`; use `main` unless Work requires a temporary isolated worktree.
- Do not wait for user approval between tasks; complete the plan continuously.
- Every task follows RED -> GREEN -> focused verification -> relevant full verification -> commit.
- One experiment tests exactly one Advisor-supported game-setting change.
- All setting writes, confirmation handling, Undo, and conflict handling continue through `AdvisorSettingOperations`; do not add another writer or change ledger.
- Follow-up evidence must come from the exact manual capture explicitly requested by the experiment. Automatic/unrelated captures never qualify.
- Baseline and follow-up must share the same loaded-city diagnostic `SessionId`.
- Reuse `AdvisorComparison.Compare(...)`; do not add alternate thresholds, metric-direction rules, global scores, winners, or causal claims.
- Stabilization guidance is exactly 5 seconds and never auto-starts capture.
- Experiments are in-memory only; no restart persistence or migration.
- No new continuous profiler recorders, full-world scans, or broad per-frame settings scans.
- If CS2 DLL/toolchain/game runtime is unavailable, mark dependent verification `NOT RUN`; never infer PASS.
- Avoid unrelated refactors. If the same local structural issue repeats, fix the abstraction rather than layering one-off patches.

## Review Focus

- Rejected follow-up request while another capture is active must remain `AwaitingFollowUp` and must not adopt a later unrelated capture — Task 2/3.
- Confirmation-required Apply must not create duplicate effective changes or advance before confirmed success — Task 3.
- External modification of the tested value, including preset side effects, must invalidate before comparison is presented as valid — Task 3.
- City-session change while waiting/running follow-up must prevent cross-session comparison and must not auto-revert the global game setting — Task 3.
- Cancel after Apply must state that the current setting remains unless Undo is explicitly chosen — Task 1/5.

---

## File Structure

- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentState.cs` — state/validity/reason/outcome enums.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperiment.cs` — pure experiment snapshot/data.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentCoordinator.cs` — deterministic state transitions and `AdvisorComparison` completion.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentGuard.cs` — pure contamination/session/follow-up eligibility rules used by `AdvisorSystem`.
- `src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs` — game-facing orchestration only.
- `src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs` — exact manual capture return/correlation.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs` — exposes experiment state.
- `src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs` and `UI/src/profiler/bindings.ts` — commands/projection.
- `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx` and localization/style files — workflow UX.
- `src/CS2RuntimeAssetAuditor/Export/PerformanceReport.cs` and `ProfilerReportBuilder.cs` — additive export.
- `docs/validation/2026-09-29-investigation-experiment-validation.md` — executed/not-run evidence.

---

### Task 1: Pure experiment model and state machine

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentState.cs`
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperiment.cs`
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentCoordinator.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentTests.cs`

**Interfaces:**
- Consumes: `AdvisorEvidenceSnapshot`, `SettingRecommendation`, `SettingChange`, `AdvisorComparison.Compare(...)`.
- Produces:

```csharp
public enum InvestigationExperimentState
{
    BaselineReady,
    AwaitingApplyConfirmation,
    AwaitingFollowUp,
    FollowUpCapturing,
    Completed,
    Cancelled,
    Invalidated
}

public enum InvestigationExperimentValidity { Valid, Invalidated }
public enum InvestigationInvalidationReason
{
    None,
    SessionChanged,
    AdditionalAdvisorSettingChanged,
    TestedSettingExternallyModified,
    TestedSettingNoLongerMatchesExpectedValue,
    RecommendationBecameStaleBeforeApply,
    FollowUpCaptureInvalid,
    FollowUpCaptureInterrupted,
    FollowUpCaptureWrongSession
}
public enum InvestigationCompletionOutcome { None, Kept, Undone, Cancelled }
```

`InvestigationExperiment` contains the spec fields plus `DateTimeOffset? StabilizationReadyAtUtc` and `string LastFailureReason` and retains copied `BaselineEvidence`/optional `FollowUpEvidence`; it retains no Unity/game objects.

`InvestigationExperimentCoordinator` exposes:

```csharp
public InvestigationExperiment Current { get; }
public InvestigationExperiment Start(string experimentId, string sessionId, string baselineCaptureId,
    AdvisorEvidenceSnapshot baselineEvidence, SettingRecommendation recommendation, DateTimeOffset startedAtUtc);
public void AwaitApplyConfirmation();
public void RecordApplied(DateTimeOffset appliedAtUtc);
public void RecordApplyFailure(string machineReason);
public void RecordFollowUpStarted(string captureId);
public void CompleteFollowUp(string captureId, AdvisorEvidenceSnapshot followUpEvidence,
    IReadOnlyList<SettingChange> qualifyingChanges, DateTimeOffset completedAtUtc);
public void Invalidate(InvestigationInvalidationReason reason);
public void Cancel(DateTimeOffset completedAtUtc);
public void Complete(InvestigationCompletionOutcome outcome, DateTimeOffset completedAtUtc);
```

- [x] **Step 1: Write failing tests**

Add tests:

```text
Start_freezes_baseline_and_rejects_second_active_experiment
Confirmation_required_does_not_advance_until_success
Applied_change_sets_stabilization_ready_exactly_five_seconds_later
Follow_up_completion_reuses_AdvisorComparison_states
Multiple_qualifying_changes_invalidate_single_setting_experiment
Cancel_before_apply_has_no_setting_outcome
Cancel_after_apply_records_cancelled_without_claiming_undo
Invalidated_experiment_rejects_progress
Baseline_evidence_survives_source_capture_eviction
```

- [x] **Step 2: Run RED**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentTests
```

Expected: FAIL because types are missing.

- [x] **Step 3: Implement minimal domain/state logic**

`RecordApplied(at)` must set `StabilizationReadyAtUtc = at + TimeSpan.FromSeconds(5)`. `CompleteFollowUp(...)` must call `AdvisorComparison.Compare(Current.BaselineEvidence, followUpEvidence, qualifyingChanges)` and invalidate instead of completing if effective changes include more than one mutation or another setting ID. `RecordApplyFailure(...)` leaves the experiment before follow-up and records only a machine reason.

- [x] **Step 4: Verify GREEN and full pure suite**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentTests
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentTests.cs
git commit -m "feat: add investigation experiment state machine"
```

---

### Task 2: Exact explicit-manual-capture correlation

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs`
- Create: `tests/CS2RuntimeAssetAuditor.AdapterTests/InvestigationCaptureContractTests.cs`
- Modify only if needed: `tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj`

**Interfaces:**
- Produces:

```csharp
public CaptureSession RequestManualCapture();
```

Return the exact `CaptureSession` newly created by this request; return `null` when the request does not start a new capture.

- [x] **Step 1: Add failing contract test**

Verify the signature and that the implementation correlates by the before/after `CurrentSession` transition, not by reading `CompletedSessions.Last()` or another “latest capture” heuristic.

- [x] **Step 2: Run RED when adapter dependencies are available**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release --filter InvestigationCaptureContractTests
```

If CS2 assemblies are unavailable, record the exact dependency failure and continue without weakening the contract.

- [x] **Step 3: Implement return contract**

Preserve existing monitoring/session checks and configuration. Capture `before = _controller?.CurrentSession`, issue the existing request, perform current `BeginCaptureWork`/configuration work, and return the new current session only if this request actually created it. Return `null` for disabled monitoring, inactive city session, already-active/cooldown/rejected request, or any no-new-session result. Existing callers may ignore the return value.

- [x] **Step 4: Verify runtime regressions**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

and adapter test when available. Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs tests/CS2RuntimeAssetAuditor.AdapterTests
git commit -m "feat: correlate explicit manual capture requests"
```

---

### Task 3: Pure guard rules and AdvisorSystem orchestration

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentGuard.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs`
- Modify only if a narrow reusable hook is required: `src/CS2RuntimeAssetAuditor/Advisor/Settings/AdvisorSettingOperations.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentGuardTests.cs`
- Test where assemblies permit: `tests/CS2RuntimeAssetAuditor.AdapterTests/InvestigationAdvisorIntegrationContractTests.cs`

**Interfaces:**
- `InvestigationExperimentGuard` produces pure rules:

```csharp
public static InvestigationInvalidationReason? EvaluateAdvisorApply(
    InvestigationExperiment experiment, string changedSettingId, bool applySucceeded);
public static InvestigationInvalidationReason? EvaluateObservedSetting(
    InvestigationExperiment experiment, string observedValue);
public static InvestigationInvalidationReason? EvaluateSession(
    InvestigationExperiment experiment, string currentSessionId);
public static bool IsExpectedFollowUp(
    InvestigationExperiment experiment, string captureId, string captureSessionId, DateTimeOffset? captureStartedAtUtc);
public static IReadOnlyList<SettingChange> SelectQualifyingChanges(
    InvestigationExperiment experiment, IReadOnlyList<SettingChange> changes);
```

`SelectQualifyingChanges` uses `AppliedAt >= experiment.StartedAtUtc.UtcDateTime` and excludes `Pending`/`ApplyFailed`; Task 1 then enforces one effective setting mutation.

- `AdvisorSystem` produces:

```csharp
public InvestigationExperiment CurrentExperiment { get; }
public bool StartExperiment(string captureId, string settingId, string proposedValue);
public SettingApplyResult ApplyExperimentChange(bool confirmed = false);
public bool StartExperimentFollowUpCapture();
public void CancelExperiment();
public bool KeepExperimentChange();
public SettingApplyResult UndoExperimentChange(bool confirmed = false);
```

`AdvisorState` gains:

```csharp
public InvestigationExperiment Experiment { get; set; }
```

- [x] **Step 1: Write RED pure guard tests**

Cover successful/failed different-setting Apply, expected tested value vs external mismatch, same/different session, exact/mismatched follow-up ID, follow-up started before Apply, and filtering of pre-experiment/pending/failed changes.

- [x] **Step 2: Run RED**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentGuardTests
```

Expected: FAIL because guard is missing.

- [x] **Step 3: Implement guard and make pure tests GREEN**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentGuardTests
```

Expected: PASS.

- [x] **Step 4: Integrate `AdvisorSystem`**

Implement only game-facing orchestration:

- start only from the current diagnosed capture/recommendation and active `Mod.SessionContext.SessionId`;
- freeze projected baseline evidence with Task 1 coordinator;
- test Apply always delegates to `Operations.Apply(...)`; confirmation failure `ConfirmationRequired` moves/keeps the experiment in confirmation state without duplicate effective change;
- a successful ordinary `ApplySetting(...)` for another setting uses `EvaluateAdvisorApply(...)` and invalidates; failed writes do not contaminate;
- exact follow-up capture comes only from Task 2's returned `CaptureSession`; a rejected request leaves `AwaitingFollowUp` and stores no ID;
- while `FollowUpCapturing`, completion lookup matches only stored capture ID and session; automatic/unrelated completed captures are ignored;
- `SelectQualifyingChanges(...)` feeds Task 1 comparison completion;
- city-session generation change invalidates without calling Undo;
- Keep performs no setting write; Undo delegates to existing `Operations.Undo(...)` and marks `Undone` only on success; conflict/confirmation keeps result visible and incomplete until resolved.

For tested-setting integrity, read only the single tested setting through `Gateway.Read(...)` at most once every **500 ms**, only after successful Apply and before terminal state. On mismatch invalidate with `TestedSettingExternallyModified`. Do not scan the settings catalog each frame.

- [x] **Step 5: Add adapter/source contract assertions for orchestration wiring**

Pin that `AdvisorSystem` calls the existing `AdvisorSettingOperations`, Task 2 return contract, and pure guard/coordinator rather than adding a direct settings writer or second comparison implementation.

- [x] **Step 6: Verify**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Run adapter test if dependencies exist. Expected: PASS / otherwise documented NOT RUN.

- [x] **Step 7: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment src/CS2RuntimeAssetAuditor/Advisor src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentGuardTests.cs tests/CS2RuntimeAssetAuditor.AdapterTests
git commit -m "feat: orchestrate advisor investigation experiments"
```

---

### Task 4: Backend UI projection and command bindings

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationUiProjectionTests.cs`
- Test: `UI/src/profiler/investigationBindings.test.ts`

**Interfaces:**
- Add CS2 triggers exactly:

```text
advisorStartExperiment(captureId, settingId, proposedValue)
advisorApplyExperiment(confirmed)
advisorStartExperimentFollowUp()
advisorCancelExperiment()
advisorKeepExperiment()
advisorUndoExperiment(confirmed)
```

- Add TS wrappers exactly:

```ts
startAdvisorExperiment(captureId: string, settingId: string, proposedValue: string): void
applyAdvisorExperiment(confirmed?: boolean): void
startAdvisorExperimentFollowUp(): void
cancelAdvisorExperiment(): void
keepAdvisorExperiment(): void
undoAdvisorExperiment(confirmed?: boolean): void
```

- Add `AdvisorExperiment` projection with: `experimentId`, `state`, `validity`, `invalidationReason`, `baselineCaptureId`, `followUpCaptureId`, `settingId`, `settingDisplayName`, `originalValue`, `testedValue`, `changeAppliedAtUtc`, `stabilizationReadyAtUtc`, `completionOutcome`, `lastFailureReason`, `comparison`.

- [x] **Step 1: Write RED C#/TS projection tests**

Assert no experiment projects `null`, never a default-looking observed object.

- [x] **Step 2: Run RED**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationUiProjectionTests
cd UI && npm test -- investigationBindings.test.ts
```

- [x] **Step 3: Add backend triggers and `WriteAdvisorExperiment(...)`**

Each trigger delegates to Task 3 and refreshes visible snapshot using existing Advisor-command patterns.

- [x] **Step 4: Add TS types/wrappers; set `EMPTY_ADVISOR.experiment = null`**

Do not rename any existing binding.

- [x] **Step 5: Verify**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationUiProjectionTests
cd UI && npx tsc --noEmit -p . && npm test -- investigationBindings.test.ts
```

Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs UI/src/profiler/bindings.ts tests/CS2RuntimeAssetAuditor.Tests/InvestigationUiProjectionTests.cs UI/src/profiler/investigationBindings.test.ts
git commit -m "feat: expose investigation experiment bindings"
```

---

### Task 5: Performance Advisor experiment UI and localization

**Files:**
- Modify: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
- Modify: `UI/src/profiler/profiler.module.scss`
- Modify: `UI/src/i18n/messages.ts`
- Modify if wiring requires: `UI/src/shell/RuntimeAssetAuditorRoot.tsx`
- Test: `UI/src/profiler/investigationExperiment.test.tsx`

**Interfaces:**
- Consumes Task 4 bindings/projection.
- Produces recommendation `Test change`, active progress card, explicit Apply, stabilization guidance, explicit follow-up capture, invalidation/result card, Keep/Undo/Cancel, English/Japanese strings.

- [x] **Step 1: Write RED UI tests**

Cover eligibility, confirmation state, 5-second guidance, explicit follow-up action, follow-up-in-progress state, localized invalidation reason, neutral state counts, metric rows, always-visible non-causality notice, Keep/Undo calls, cancel-after-Apply wording, conflict compatibility, and English/Japanese key coverage.

- [x] **Step 2: Run RED**

```bash
cd UI
npm test -- investigationExperiment.test.tsx
```

- [x] **Step 3: Implement UI inside existing Performance Advisor tab**

Do not add a new top-level tab. While a valid experiment is active, keep recommendations readable but disable normal Apply for other recommendations with a localized explanation; backend Task 3 remains authoritative if another path changes a setting.

The UI may derive readiness from `stabilizationReadyAtUtc`, but reaching it only enables/displays the explicit follow-up button and never triggers capture.

- [x] **Step 4: Implement result summary without verdict**

Count `Improved`, `Regressed`, `NoMaterialChange`, `NotComparable` from existing comparison rows. No weighted result, winner, success percentage, or automatic Keep/Undo suggestion.

- [x] **Step 5: Add English/Japanese strings and responsive styling**

Map machine invalidation/outcome IDs to localized copy. Cancel after Apply must explicitly say cancellation keeps the current setting; Undo is a separate action.

- [x] **Step 6: Verify all UI**

```bash
cd UI
npm test
npx tsc --noEmit -p .
npm run build
```

Expected: PASS / webpack compiled successfully.

- [x] **Step 7: Commit**

```bash
git add UI/src/profiler UI/src/i18n UI/src/shell/RuntimeAssetAuditorRoot.tsx
git commit -m "feat: add guided investigation advisor workflow"
```

---

### Task 6: Unified Advisor experiment export and privacy

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Export/PerformanceReport.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Export/ProfilerReportBuilder.cs`
- Modify only if needed for additive unified envelope: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReport.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentExportTests.cs`

**Interfaces:**
- Add optional `ReportAdvisorExperiment Experiment` to existing `ReportAdvisor`.
- `ReportAdvisorExperiment` contains the machine-readable experiment fields plus reused `ReportAdvisorComparison Comparison`; implement `SanitizedCopy()` using existing `ReportPrivacy.Sanitize` for all strings.

- [ ] **Step 1: Write RED export tests**

Cover valid completed serialization, invalidated serialization/reason, missing experiment absent/null, comparison equality with existing Advisor export, privacy sanitization, and absence of live/private implementation objects.

Also pin current `RuntimeAssetAuditReport.SchemaVersion`; change it only if an existing repository versioning test/policy explicitly requires an increment for this additive nullable field, and document the reason.

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentExportTests
```

- [ ] **Step 3: Implement DTO/mapping and sanitizer**

Reuse the existing comparison-to-report mapping; do not duplicate comparison logic.

- [ ] **Step 4: Verify focused/full pure tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentExportTests
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Export tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentExportTests.cs
git commit -m "feat: export investigation experiment results"
```

---

### Task 7: Regression coverage, README, and validation matrix

**Files:**
- Modify: `README.md`
- Modify: `tests/CS2RuntimeAssetAuditor.Tests/AdvisorComparisonTests.cs`
- Modify: `UI/src/profiler/performanceAdvisorRegression.test.tsx`
- Create: `docs/validation/2026-09-29-investigation-experiment-validation.md`

**Interfaces:**
- Produces evidence that the new orchestration is additive and ordinary Advisor remains intact.

- [ ] **Step 1: Add regression tests**

Pin: ordinary manual baseline/follow-up comparison without experiment; ordinary Apply/Undo/UndoSession/conflict; automatic capture unchanged with no experiment; absent experiment does not affect Asset/unified export; ordinary Advisor `MultipleChanges` comparison remains allowed outside experiment mode.

- [ ] **Step 2: Run regression suites**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
cd UI && npm test
```

Expected: PASS.

- [ ] **Step 3: Update README user-facing Advisor description**

Document: diagnose -> `Test change` -> explicit Apply -> wait/stabilize -> explicit follow-up capture -> observed comparison -> Keep/Undo. State that results are observational and do not prove causality. Keep implementation details out of README.

- [ ] **Step 4: Create validation record**

Create separate automated/manual tables. Include all 14 manual scenarios from spec section 22. Every unexecuted scenario is `NOT RUN`, never inferred PASS.

- [ ] **Step 5: Commit**

```bash
git add README.md tests/CS2RuntimeAssetAuditor.Tests/AdvisorComparisonTests.cs UI/src/profiler/performanceAdvisorRegression.test.tsx docs/validation/2026-09-29-investigation-experiment-validation.md
git commit -m "docs: document investigation experiment workflow"
```

---

### Task 8: Full verification and completion

**Files:**
- Modify only if results change: `docs/validation/2026-09-29-investigation-experiment-validation.md`

- [ ] **Step 1: Run all pure .NET tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 2: Run all UI verification**

```bash
cd UI
npm test
npx tsc --noEmit -p .
npm run build
cd ..
```

Expected: tests PASS, type check PASS, production build PASS.

- [ ] **Step 3: Run adapter tests when CS2 managed assemblies are available**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

Expected: PASS; otherwise record exact environment cause as `NOT RUN`.

- [ ] **Step 4: Run mod Release build when official CS2 toolchain is available**

Use the repository-documented Release build path. Expected: zero compile errors and UI bundle included. Otherwise record `NOT RUN`.

- [ ] **Step 5: Execute spec section 22 manual in-game validation when runnable CS2 is available**

Run all 14 scenarios, including confirmation-free/required Apply, unrelated automatic capture, Keep, Undo, Options conflict, second-setting contamination defense, city reload, export privacy, Japanese/English layout, and ordinary Advisor regression. Record actual evidence; leave unexecuted rows `NOT RUN`.

- [ ] **Step 6: Final diff/architecture review**

Confirm no alternate comparison engine, second settings writer/ledger, automatic Apply/Keep/Undo/capture sequencing, cross-session comparison, unbounded experiment history, unrelated Asset/runtime refactor, causal wording/global verdict, or falsely-passed validation row.

- [ ] **Step 7: Commit validation updates if changed**

```bash
git add docs/validation/2026-09-29-investigation-experiment-validation.md
git commit -m "test: verify investigation experiment workflow"
```

Do not create an empty commit.

- [ ] **Step 8: Finish on repository `main`**

If Work used a temporary isolated branch/worktree, integrate the completed commits back to `main`. Confirm a clean working tree and report final commit SHA plus PASS/NOT RUN verification summary. Do not open a PR unless the Work environment specifically requires one.
