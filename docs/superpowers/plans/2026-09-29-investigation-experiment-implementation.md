# Guided Investigation Experiment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the existing Performance Advisor baseline/follow-up comparison into a user-guided, one-setting investigation experiment with explicit capture correlation, contamination detection, Keep/Undo completion, localized UI, and unified export.

**Architecture:** Add a pure Advisor experiment state machine that owns no Unity/game objects, then let `AdvisorSystem` orchestrate existing `AdvisorSettingOperations`, `CaptureRuntimeSystem`, and `AdvisorComparison`. Extend the manual-capture API only enough to return the exact capture started by an explicit request, project experiment state through the existing UI snapshot/binding layer, and serialize it through the existing Advisor report path.

**Tech Stack:** C# / .NET 8 pure tests, CS2 net48 mod project and managed game APIs, NUnit, TypeScript, React 18, Vitest, Colossal UI bindings.

**Spec:** `docs/superpowers/specs/2026-09-29-investigation-experiment-design.md`

## Global Constraints

- Implementation is performed in ChatGPT Work using Native / `superpowers:executing-plans`.
- Implement directly against `pengin0503/CS2-Runtime-Asset-Auditor`; use `main` unless Work requires a temporary isolated worktree.
- Do not wait for user approval between tasks; execute the complete plan continuously.
- Every task follows RED -> GREEN -> focused verification -> relevant full verification -> commit.
- One active experiment tests exactly one Advisor-supported game-setting change.
- All setting writes, confirmation handling, Undo, and conflict handling continue through `AdvisorSettingOperations`; do not create a second writer or change ledger.
- Follow-up evidence must come from the exact manual capture explicitly requested by the experiment; automatic or unrelated captures never qualify.
- Baseline and follow-up must belong to the same loaded-city diagnostic session.
- Comparison must reuse `AdvisorComparison.Compare(...)`; do not add alternate thresholds, metric direction rules, global scores, or causal claims.
- The initial stabilization guidance is 5 seconds and never auto-starts capture.
- Active experiments are in-memory only; do not persist them across restarts.
- Do not add continuous profiler recorders, full-world scans, or expensive per-frame polling.
- Keep existing Runtime, Advisor, Asset, export, privacy, and localization behavior intact outside the new experiment workflow.
- If CS2 managed DLLs/toolchain are unavailable, record adapter/Release/manual-game verification as NOT RUN rather than claiming success.
- Avoid unrelated refactors. If the same structural problem repeats while implementing this feature, fix the local abstraction instead of adding repeated one-off patches.

## Review Focus

- A follow-up request made while another capture is active or the runtime rejects the request must leave the experiment waiting and must never adopt a different later capture; Task 2 pins this behavior.
- A confirmation-required recommendation must not create duplicate change records or advance the experiment until the confirmed Apply actually succeeds; Task 3 pins this behavior.
- A tested setting changed externally, including a graphics-preset side effect, must invalidate the experiment before a result is presented as valid; Task 3 pins this behavior.
- A city-session change while waiting for or running the follow-up must prevent cross-session comparison and must not silently revert the global setting; Task 3 pins this behavior.
- Cancel after Apply must clearly leave the setting in its current value unless the user explicitly chooses Undo; Tasks 1 and 5 pin this behavior.

---

## File structure

Create or extend the following focused responsibilities:

- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperiment.cs` — serializable/pure experiment data only.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentState.cs` — state, validity, invalidation and completion enums.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentCoordinator.cs` — deterministic state transitions and comparison completion.
- `src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs` — game/session/settings/capture orchestration only; no duplicated comparison rules.
- `src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs` — narrow exact manual-capture correlation return value.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs` — exposes the current/latest experiment to projection/export.
- `src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs` — CS2 trigger bindings and JSON projection for experiment commands/state.
- `UI/src/profiler/bindings.ts` — typed experiment projection and trigger wrappers.
- `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx` — `Test change`, progress card, result card, Keep/Undo/Cancel controls.
- `UI/src/i18n/messages.ts` — English/Japanese visible strings.
- `src/CS2RuntimeAssetAuditor/Export/PerformanceReport.cs` and `src/CS2RuntimeAssetAuditor/Export/ProfilerReportBuilder.cs` — additive Advisor experiment report DTO/projection.
- `docs/validation/2026-09-29-investigation-experiment-validation.md` — executed verification and remaining real-game checks.

---

### Task 1: Pure experiment domain and state machine

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentState.cs`
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperiment.cs`
- Create: `src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/InvestigationExperimentCoordinator.cs`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentTests.cs`

**Interfaces:**
- Consumes: existing `AdvisorEvidenceSnapshot`, `SettingRecommendation`, `SettingChange`, and `AdvisorComparison.Compare(...)`.
- Produces:
  - `enum InvestigationExperimentState { BaselineReady, AwaitingApplyConfirmation, AwaitingFollowUp, FollowUpCapturing, Completed, Cancelled, Invalidated }`
  - `enum InvestigationExperimentValidity { Valid, Invalidated }`
  - `enum InvestigationInvalidationReason { None, SessionChanged, AdditionalAdvisorSettingChanged, TestedSettingExternallyModified, TestedSettingNoLongerMatchesExpectedValue, RecommendationBecameStaleBeforeApply, FollowUpCaptureInvalid, FollowUpCaptureInterrupted, FollowUpCaptureWrongSession }`
  - `enum InvestigationCompletionOutcome { None, Kept, Undone, Cancelled }`
  - `sealed class InvestigationExperiment` with the spec fields and `DateTimeOffset? StabilizationReadyAtUtc`, `string LastFailureReason`.
  - `sealed class InvestigationExperimentCoordinator` with `Current` and the methods defined below.

- [ ] **Step 1: Write the failing state-machine tests**

Add tests named:

```csharp
Start_freezes_baseline_and_rejects_a_second_active_experiment()
Confirmation_required_does_not_advance_until_apply_succeeds()
Applied_change_sets_five_second_stabilization_boundary()
Follow_up_completion_reuses_AdvisorComparison()
Multiple_qualifying_changes_invalidate_single_setting_experiment()
Cancel_before_apply_has_no_completion_side_effect()
Cancel_after_apply_records_cancelled_without_claiming_undo()
Invalidated_experiment_rejects_further_progress()
```

Assert exact state/validity/outcome transitions and assert that `BaselineEvidence` remains available without looking up the source capture again.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentTests
```

Expected: FAIL because the experiment types do not exist.

- [ ] **Step 3: Implement the pure domain types and coordinator**

Implement these coordinator methods with deterministic validation and no game/Unity references:

```csharp
public InvestigationExperiment Start(
    string experimentId,
    string sessionId,
    string baselineCaptureId,
    AdvisorEvidenceSnapshot baselineEvidence,
    SettingRecommendation recommendation,
    DateTimeOffset startedAtUtc);

public void AwaitApplyConfirmation();
public void RecordApplied(DateTimeOffset appliedAtUtc);
public void RecordApplyFailure(string machineReason);
public void RecordFollowUpStarted(string captureId);
public void CompleteFollowUp(
    string captureId,
    AdvisorEvidenceSnapshot followUpEvidence,
    IReadOnlyList<SettingChange> qualifyingChanges,
    DateTimeOffset completedAtUtc);
public void Invalidate(InvestigationInvalidationReason reason);
public void Cancel(DateTimeOffset completedAtUtc);
public void Complete(InvestigationCompletionOutcome outcome, DateTimeOffset completedAtUtc);
```

`RecordApplied` sets `StabilizationReadyAtUtc = appliedAtUtc + TimeSpan.FromSeconds(5)`. `CompleteFollowUp` must call the existing `AdvisorComparison.Compare(Current.BaselineEvidence, followUpEvidence, qualifyingChanges)` and invalidate instead of completing when the authoritative qualifying-change set contains more than one effective mutation or a different setting ID.

- [ ] **Step 4: Run focused and full pure tests**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentTests
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentTests.cs
git commit -m "feat: add investigation experiment state machine"
```

---

### Task 2: Exact manual-capture correlation

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs`
- Create: `tests/CS2RuntimeAssetAuditor.AdapterTests/InvestigationCaptureContractTests.cs`
- Modify if needed for source compilation only: `tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj`

**Interfaces:**
- Consumes: existing `CaptureRuntimeSystem.CurrentSession`, `DeepCaptureController.RequestManualCapture(...)`, `CaptureSession.Id`, and existing session lifecycle.
- Produces: `public CaptureSession RequestManualCapture()` returning the exact newly-created manual `CaptureSession`, or `null` when the explicit request did not start a new capture.

- [ ] **Step 1: Add a failing adapter/contract test for the return contract**

Add tests that verify the compiled/source contract exposes:

```csharp
CaptureSession CaptureRuntimeSystem.RequestManualCapture()
```

and that the implementation determines success from the before/after `CurrentSession` transition rather than returning an arbitrary entry from `CompletedSessions`.

- [ ] **Step 2: Run the focused adapter test and verify RED**

Run when CS2 managed DLLs are available:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release --filter InvestigationCaptureContractTests
```

If the environment lacks required game assemblies, the source-level contract test may run if supported; otherwise record the exact missing dependency and continue without weakening the contract.

- [ ] **Step 3: Change `CaptureRuntimeSystem.RequestManualCapture()` to return the exact started session**

Preserve all existing monitoring/session/configuration behavior. Capture `before = _controller?.CurrentSession`, issue the existing manual request, run the existing `BeginCaptureWork`/configuration setup, and return the new current session only when `before` was different/null and the request actually entered a new manual Deep Capture. Return `null` when monitoring is disabled, no city session is active, the request is rejected by current capture/cooldown state, or no new session was created.

Existing callers may ignore the return value; do not change ordinary manual-capture UX.

- [ ] **Step 4: Verify runtime regressions**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

and, when available:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release --filter InvestigationCaptureContractTests
```

Expected: PASS; ordinary capture tests remain green.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs tests/CS2RuntimeAssetAuditor.AdapterTests
git commit -m "feat: correlate explicit manual capture requests"
```

---

### Task 3: Advisor orchestration, setting integrity, and lifecycle

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs`
- Modify only if a narrow reusable observation helper is required: `src/CS2RuntimeAssetAuditor/Advisor/Settings/AdvisorSettingOperations.cs`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationAdvisorPolicyTests.cs`

**Interfaces:**
- Consumes: Task 1 `InvestigationExperimentCoordinator`, Task 2 `CaptureRuntimeSystem.RequestManualCapture()`, existing `Operations.Apply/Undo/ResolveConflict`, `SettingChangeSession`, `CaptureAdvisorEvidenceProjector`, `Mod.Sessions.Generation`, and `Mod.SessionContext.SessionId`.
- Produces on `AdvisorSystem`:

```csharp
public InvestigationExperiment CurrentExperiment { get; }
public bool StartExperiment(string captureId, string settingId, string proposedValue);
public SettingApplyResult ApplyExperimentChange(bool confirmed = false);
public bool StartExperimentFollowUpCapture();
public void CancelExperiment();
public bool KeepExperimentChange();
public SettingApplyResult UndoExperimentChange(bool confirmed = false);
```

`AdvisorState` gains `public InvestigationExperiment Experiment { get; set; }`.

- [ ] **Step 1: Write failing orchestration/policy tests**

Cover at least these cases with pure seams/fakes rather than requiring a live game world:

```text
- start rejects missing/currently stale recommendation and inactive session;
- confirmation-required Apply leaves state awaiting confirmation and creates no duplicate effective change;
- failed Apply does not advance;
- successful Apply advances and uses the existing change ledger;
- successful normal Advisor Apply for a different setting invalidates the experiment;
- failed normal Advisor Apply for a different setting does not falsely contaminate it;
- external read of tested setting differing from TestedValue invalidates it;
- explicit follow-up request stores exactly the CaptureSession ID returned by Task 2;
- rejected follow-up request leaves AwaitingFollowUp and stores no ID;
- automatic/unrelated completed capture cannot satisfy the stored ID;
- wrong-session candidate invalidates/rejects comparison;
- session generation change invalidates without calling Undo;
- baseline evidence still compares after the source baseline capture is evicted;
- Keep completes without another setting write;
- Undo delegates to existing safe Undo and preserves conflict behavior.
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationAdvisorPolicyTests
```

Expected: FAIL because orchestration APIs are missing.

- [ ] **Step 3: Integrate the experiment into `AdvisorSystem`**

Keep the state machine in Task 1. `AdvisorSystem` performs only game-facing work:

- locate the diagnosed baseline capture and matching recommendation;
- freeze projected baseline evidence into `Start(...)`;
- route test Apply through `Operations.Apply(...)`;
- after a successful test Apply, pass the authoritative successful `SettingChange` to experiment state;
- wrap normal `ApplySetting(...)` so a successful different-setting change invalidates an active experiment;
- use Task 2's returned `CaptureSession` to record the exact follow-up ID;
- on `OnUpdate`, if `FollowUpCapturing`, locate completion only by the recorded ID and same session, project evidence, then call `CompleteFollowUp(...)`;
- on city-session generation change, invalidate the experiment and keep the existing global setting/change ledger untouched.

For tested-setting external modification, perform at most one `Gateway.Read(CurrentExperiment.SettingId)` every **500 ms**, only while an experiment has successfully applied its test change and is not terminal. Compare to `TestedValue`; invalidate with `TestedSettingExternallyModified` on mismatch. Do not scan the whole settings catalog every frame.

- [ ] **Step 4: Preserve ordinary Advisor comparison and change behavior**

Do not remove `SelectBaseline`, `DiagnoseCompletedCapture`, ordinary `ApplySetting`, `UndoSetting`, `UndoSession`, or current comparison output. An experiment may set `AdvisorState.Experiment`; existing `AdvisorState.Comparison` remains the ordinary manual baseline/follow-up comparison field.

- [ ] **Step 5: Run focused and full pure tests**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationAdvisorPolicyTests
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Advisor src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorState.cs tests/CS2RuntimeAssetAuditor.Tests/InvestigationAdvisorPolicyTests.cs
git commit -m "feat: orchestrate advisor investigation experiments"
```

---

### Task 4: CS2 bindings and serializable UI projection

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationUiProjectionTests.cs`
- Create or modify: `UI/src/profiler/investigationBindings.test.ts`

**Interfaces:**
- Consumes: Task 3 `AdvisorSystem` experiment methods/state.
- Produces CS2 trigger names exactly:

```text
advisorStartExperiment(captureId, settingId, proposedValue)
advisorApplyExperiment(confirmed)
advisorStartExperimentFollowUp()
advisorCancelExperiment()
advisorKeepExperiment()
advisorUndoExperiment(confirmed)
```

- Produces TypeScript wrappers:

```ts
startAdvisorExperiment(captureId: string, settingId: string, proposedValue: string): void
applyAdvisorExperiment(confirmed?: boolean): void
startAdvisorExperimentFollowUp(): void
cancelAdvisorExperiment(): void
keepAdvisorExperiment(): void
undoAdvisorExperiment(confirmed?: boolean): void
```

- [ ] **Step 1: Add failing projection/binding tests**

Assert that `AdvisorUiState` gains `experiment?: AdvisorExperiment | null`, and `AdvisorExperiment` contains:

```ts
experimentId: string;
state: string;
validity: string;
invalidationReason: string;
baselineCaptureId: string;
followUpCaptureId: string;
settingId: string;
settingDisplayName: string;
originalValue: string;
testedValue: string;
changeAppliedAtUtc: string;
stabilizationReadyAtUtc: string;
completionOutcome: string;
lastFailureReason: string;
comparison: AdvisorComparison | null;
```

The backend JSON writer must output null for no experiment, not a default object that looks observed.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationUiProjectionTests
cd UI && npm test -- investigationBindings.test.ts
```

Expected: FAIL on missing projection/triggers.

- [ ] **Step 3: Add backend trigger bindings and `WriteAdvisorExperiment(...)`**

Each trigger delegates to the Task 3 method and refreshes the visible snapshot with the same pattern as existing Advisor commands. Keep the frontend free of live game objects.

- [ ] **Step 4: Add TypeScript contracts and wrappers**

Extend `EMPTY_ADVISOR` with `experiment: null`; keep all existing binding names unchanged.

- [ ] **Step 5: Verify C# and UI type/tests**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationUiProjectionTests
cd UI && npx tsc --noEmit -p . && npm test -- investigationBindings.test.ts
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs UI/src/profiler/bindings.ts tests/CS2RuntimeAssetAuditor.Tests/InvestigationUiProjectionTests.cs UI/src/profiler/investigationBindings.test.ts
git commit -m "feat: expose investigation experiment bindings"
```

---

### Task 5: Performance Advisor experiment UX and localization

**Files:**
- Modify: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
- Modify: `UI/src/profiler/profiler.module.scss`
- Modify: `UI/src/i18n/messages.ts`
- Modify as needed: `UI/src/shell/RuntimeAssetAuditorRoot.tsx`
- Create: `UI/src/profiler/investigationExperiment.test.tsx`

**Interfaces:**
- Consumes: Task 4 `AdvisorExperiment` and trigger wrappers.
- Produces: recommendation-level `Test change`, active experiment progress card, stabilization guidance, explicit follow-up action, neutral completed-result summary, metric rows, Keep/Undo, invalidation/cancel states, and English/Japanese copy.

- [ ] **Step 1: Write failing UI tests for eligibility and workflow**

Cover:

```text
- Test change appears only for writable, non-no-op applicable recommendations when no experiment is active;
- active experiment disables/prevents accidental Test change on another recommendation;
- BaselineReady/AwaitingApplyConfirmation show Apply test change with existing confirmation behavior;
- AwaitingFollowUp shows the 5-second stabilization guidance and explicit Start follow-up capture action;
- FollowUpCapturing shows the correlated capture ID/status and no second follow-up request action;
- Invalidated renders the localized machine reason and never presents a valid result;
- Completed renders improved/regressed/unchanged/not-comparable counts and metric-level rows;
- completed result always renders the non-causality notice;
- Keep and Undo controls call the dedicated experiment triggers;
- Cancel after Apply uses copy that explicitly says the setting is kept unless Undo is chosen;
- conflict-compatible Undo UI remains available when ordinary change state is `ExternallyModified`;
- English and Japanese keys exist for every new visible string.
```

- [ ] **Step 2: Run the focused UI test and verify RED**

Run:

```bash
cd UI
npm test -- investigationExperiment.test.tsx
```

Expected: FAIL because the experiment UI does not exist.

- [ ] **Step 3: Implement `Test change` and the experiment card inside `PerformanceAdvisorTab`**

Do not add a top-level tab. Keep ordinary diagnosis/recommendation/change controls readable. Prefer disabling the normal Apply button for *other* recommendations while a valid experiment is active, with a short localized explanation; Task 3 still enforces backend contamination if another path performs a change.

Use timestamp-derived readiness only for presentation. A 5-second timer ending must never trigger capture automatically.

- [ ] **Step 4: Add neutral result summary and causal disclaimer**

Count comparison states directly from `experiment.comparison.metrics`. Do not add a weighted result, winner, score, or automatic Keep/Undo recommendation.

- [ ] **Step 5: Add English/Japanese localization and responsive styling**

Add machine-ID-to-localized-reason mapping for all Task 1 invalidation reasons and completion outcomes. Verify at supported UI scales without introducing fixed widths that clip the current panel.

- [ ] **Step 6: Run UI tests, type check and production build**

Run:

```bash
cd UI
npm test
npx tsc --noEmit -p .
npm run build
```

Expected: PASS / webpack compiled successfully.

- [ ] **Step 7: Commit**

```bash
git add UI/src/profiler UI/src/i18n UI/src/shell/RuntimeAssetAuditorRoot.tsx
git commit -m "feat: add guided investigation advisor workflow"
```

---

### Task 6: Unified export and privacy coverage

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Export/PerformanceReport.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Export/ProfilerReportBuilder.cs`
- Modify if required by additive unified projection: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReport.cs`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/InvestigationExperimentExportTests.cs`

**Interfaces:**
- Consumes: Task 3/4 projected `InvestigationExperiment` and existing `ReportAdvisorComparison` mapping/privacy sanitizer.
- Produces: optional `ReportAdvisorExperiment Experiment` on `ReportAdvisor`, with sanitized scalar identifiers/values and reused `ReportAdvisorComparison`.

- [ ] **Step 1: Write failing export tests**

Cover:

```text
- completed valid experiment serializes experimentId/state/validity/baseline/follow-up/setting/original/tested/outcome/comparison;
- invalidated experiment serializes the machine-readable invalidation reason;
- no experiment serializes as absent/null according to the existing DataContract convention;
- comparison metric states/values exactly match existing Advisor comparison export;
- home/account-like strings injected into experiment string fields are sanitized by the same report privacy path;
- no live game objects or hidden setting implementation values are present.
```

Also assert the existing `RuntimeAssetAuditReport.SchemaVersion` remains unchanged **unless** the current project versioning tests/policy explicitly require an increment for this additive nullable field. If an increment is required, update that expectation and document why in the validation file rather than changing it mechanically.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter InvestigationExperimentExportTests
```

Expected: FAIL on missing report field/mapping.

- [ ] **Step 3: Implement additive report DTO/mapping**

Add `ReportAdvisorExperiment` beside existing Advisor report DTOs and reuse the existing comparison mapping rather than copying comparison thresholds or state logic.

- [ ] **Step 4: Verify focused and full pure tests**

Run:

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

### Task 7: Regression hardening and user documentation

**Files:**
- Modify: `README.md`
- Modify or extend existing Advisor regression tests: `tests/CS2RuntimeAssetAuditor.Tests/AdvisorComparisonTests.cs`
- Modify or extend: `UI/src/profiler/performanceAdvisorRegression.test.tsx`
- Create: `docs/validation/2026-09-29-investigation-experiment-validation.md`

**Interfaces:**
- Consumes: completed feature from Tasks 1-6.
- Produces: regression coverage showing ordinary Advisor behavior remains intact and a concise user-facing description of the new workflow.

- [ ] **Step 1: Add regression tests before documentation changes**

Pin these existing behaviors:

```text
- manual baseline/follow-up comparison still works with no experiment;
- ordinary Apply/Undo/UndoSession/conflict controls remain functional with no experiment;
- automatic capture behavior is unchanged when no experiment is active;
- Asset UI/export remains unaffected by absent experiment data;
- normal Advisor comparison still shows MultipleChanges rather than inheriting the experiment single-change restriction.
```

- [ ] **Step 2: Run regression tests**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
cd UI && npm test
```

Expected: PASS.

- [ ] **Step 3: Update README user-facing Advisor description**

Document the concise workflow: diagnose -> `Test change` -> explicit Apply -> wait/stabilize -> explicit follow-up capture -> observed comparison -> Keep/Undo. State that the comparison is observational and does not prove causality. Do not add implementation-internal details to the README.

- [ ] **Step 4: Create the validation record**

Create `docs/validation/2026-09-29-investigation-experiment-validation.md` with separate tables for automated tests and the 14 manual scenarios from the spec. Mark every unexecuted game scenario `NOT RUN`; never infer PASS from unit tests.

- [ ] **Step 5: Commit**

```bash
git add README.md tests/CS2RuntimeAssetAuditor.Tests/AdvisorComparisonTests.cs UI/src/profiler/performanceAdvisorRegression.test.tsx docs/validation/2026-09-29-investigation-experiment-validation.md
git commit -m "docs: document investigation experiment workflow"
```

---

### Task 8: Full verification and repository completion

**Files:**
- Modify only if results need recording: `docs/validation/2026-09-29-investigation-experiment-validation.md`

**Interfaces:**
- Consumes: all prior tasks.
- Produces: verified main-branch implementation and an evidence-backed validation record.

- [ ] **Step 1: Run all game-independent .NET verification**

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

Expected: all tests PASS, type check PASS, webpack compiled successfully.

- [ ] **Step 3: Run adapter tests when CS2 managed assemblies are available**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

Expected: PASS. If assemblies are unavailable, record NOT RUN plus the exact dependency/environment reason.

- [ ] **Step 4: Run the mod Release build when the official CS2 toolchain is available**

Use the repository's documented Release build command/toolchain. Expected: zero compile errors and UI bundle included in the mod output. If unavailable, record NOT RUN.

- [ ] **Step 5: Execute the spec's manual in-game validation when a runnable CS2 environment is available**

Run all 14 scenarios from `docs/superpowers/specs/2026-09-29-investigation-experiment-design.md` section 22, including confirmation-free/confirmation-required Apply, unrelated automatic capture, Keep, Undo, external Options change/conflict, second-recommendation contamination, city reload, export privacy, localization/layout, and ordinary Advisor regression.

Record actual observed evidence for every executed scenario. Leave the rest `NOT RUN`.

- [ ] **Step 6: Inspect the final diff for scope and architecture**

Confirm:

```text
- no alternate comparison thresholds/direction table;
- no second settings writer/change ledger;
- no automatic setting Apply/Keep/Undo/capture sequencing;
- no cross-session comparison path;
- no unbounded experiment history;
- no unrelated Asset/runtime refactor;
- no user-facing causal claim or overall verdict;
- no falsely completed validation row.
```

- [ ] **Step 7: Commit final validation updates if needed**

```bash
git add docs/validation/2026-09-29-investigation-experiment-validation.md
git commit -m "test: verify investigation experiment workflow"
```

If the validation file did not change after Task 7, do not create an empty commit.

- [ ] **Step 8: Confirm repository state**

Ensure all intended commits are on `main` (or merge the temporary Work worktree branch back to `main` if the Work environment required one), working tree is clean, and report the final commit SHA plus executed/not-run verification summary.
