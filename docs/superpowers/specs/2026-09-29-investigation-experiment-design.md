# CS2 Runtime Asset Auditor — Guided Investigation Experiment Design Specification

**Date:** 2026-09-29  
**Status:** Design approved; implementation plan pending user review  
**Target repository:** `pengin0503/CS2-Runtime-Asset-Auditor`  
**Target branch:** `main`  
**Execution policy:** Implementation will be performed later in ChatGPT Work using Native / `executing-plans`; this design task does not implement product code.

## 1. Purpose

The Guided Investigation Experiment feature turns the existing Performance Advisor baseline/follow-up comparison into a controlled, user-guided single-setting experiment workflow.

The repository already supports:

- selecting a completed Runtime capture as an Advisor baseline;
- projecting normalized Advisor evidence from a capture;
- applying individual supported game-setting recommendations through the verified Settings Gateway;
- tracking Advisor-applied changes and conflicts;
- manually starting another Runtime capture;
- comparing baseline and follow-up Advisor evidence as `Improved`, `Regressed`, `NoMaterialChange`, or `NotComparable`;
- exporting Advisor baseline/comparison/change information.

The new feature does **not** replace those mechanisms. It orchestrates them into one explicit experiment lifecycle:

```text
Diagnose a completed capture
        |
        v
Choose one recommendation to test
        |
        v
Freeze baseline evidence
        |
        v
Apply exactly one test change
        |
        v
Let the game stabilize
        |
        v
User starts a follow-up manual capture
        |
        v
Validate and associate that capture
        |
        v
Compare measured evidence
        |
        +----> Keep tested setting
        |
        +----> Undo tested setting
```

The primary outcome is to reduce manual bookkeeping while preserving the current project's conservative evidence semantics. The feature may report that improvement or regression was **observed after** a setting change. It must not claim that the setting change proved causation.

## 2. Existing implementation to reuse

The implementation must extend, not duplicate, the current Advisor and Runtime infrastructure.

Primary existing components:

- `src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs`
  - current diagnosis and baseline selection;
  - current `SettingChangeSession`;
  - current setting Apply/Undo/conflict integration.
- `src/CS2RuntimeAssetAuditor/Advisor/Settings/AdvisorSettingOperations.cs`
  - verified game-setting mutation path;
  - confirmation handling;
  - individual/session undo;
  - conflict-safe restoration.
- `src/CS2RuntimeAssetAuditor/Core/Advisor/AdvisorComparison.cs`
  - normalized before/after comparison;
  - metric comparability checks;
  - material-change tolerances;
  - existing non-causal comparison semantics.
- `src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs`
  - manual Deep Capture lifecycle;
  - completed capture store;
  - city-session scoping;
  - capture configuration and safety behavior.
- `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
  - current diagnosis, baseline, recommendation, Apply/Undo, re-diagnose and comparison UI.
- existing Advisor export DTOs/builders and unified report envelope.

No new profiler backend, alternate setting writer, or parallel comparison engine is required.

## 3. Design principles

### 3.1 One experiment tests one setting change

The first version supports exactly one Advisor recommendation / one setting mutation per experiment.

The purpose is interpretability. If multiple performance-relevant settings change between baseline and follow-up, the experiment can no longer represent a controlled single-change investigation.

Normal Performance Advisor behavior outside an active experiment remains unchanged; users may still apply multiple recommendations in the ordinary workflow.

### 3.2 User remains in control

The feature does not become an auto-tuner.

The mod may guide the sequence and automatically record state, but it must not automatically:

- apply a recommendation solely because an experiment started;
- begin a follow-up capture solely because a timer expired;
- keep an experimental setting after comparison;
- undo an experimental setting without a direct user action except where existing lifecycle cleanup already requires safe abandonment and no setting write is involved;
- iterate through recommendations.

The user explicitly applies the test change, starts the follow-up capture, and chooses Keep or Undo.

### 3.3 Reuse the authoritative settings gateway

All experimental setting writes use the same `AdvisorSettingOperations` / Settings Gateway path as normal Advisor Apply/Undo.

The experiment must preserve:

- `Available` versus read-only capabilities;
- confirmation-required semantics;
- stale recommendation checks;
- external-change conflict detection;
- safe Undo behavior.

The experiment layer must never write directly to game settings to bypass existing safety policies.

### 3.4 Preserve non-causal language

An experiment is a before/after observation under a changing city simulation, not a laboratory-isolated benchmark.

The UI and export may state:

- a metric improved after the tested change;
- a metric regressed after the tested change;
- a metric did not materially change;
- a metric could not be compared.

They must not state that the setting **caused** the result or attach a causal percentage to the setting.

### 3.5 Existing Advisor remains usable

A failure or invalidation in the experiment subsystem must not break:

- Runtime monitoring;
- automatic/manual Deep Capture;
- normal Performance Advisor diagnosis;
- normal recommendation Apply/Undo;
- Asset Audit;
- unified export.

The experiment is an orchestration layer over existing features, not a new dependency for those features.

## 4. Scope

### 4.1 In scope

The initial implementation includes:

- starting an experiment from one currently diagnosed, applicable Advisor recommendation;
- freezing baseline capture/evidence at experiment start;
- applying the selected recommendation through the existing setting operation path;
- allowing a short stabilization period to be displayed before measurement;
- explicitly requesting a follow-up **manual** capture;
- associating only the intended manual capture with the experiment;
- validating session identity, ordering and setting integrity;
- comparing baseline and follow-up via the existing `AdvisorComparison` engine;
- displaying detailed metric-by-metric results;
- displaying counts of improved/regressed/unchanged/not-comparable metrics without producing a global score;
- Keep and Undo completion actions;
- conflict handling when Undo is no longer automatically safe;
- explicit cancellation/invalidation states;
- Japanese and English UI;
- unified JSON export of experiment state/result;
- pure/core, UI and relevant adapter/regression tests.

### 4.2 Non-goals

The first version does not include:

- multiple simultaneous setting changes in one experiment;
- multi-factor experiments;
- automatic recommendation sequencing;
- automatic setting tuning;
- automatic Keep decisions;
- automatic rollback based solely on measured results;
- repeated A/B/A/B trials;
- statistical significance testing;
- FPS benchmarking or deterministic replay;
- persisted historical experiments across game restarts;
- Historical Baseline functionality;
- Spatial Asset Hotspot functionality;
- automatic Asset Audit triggered by an experiment;
- experimentation with third-party mod settings;
- hidden/internal game parameters outside the existing Advisor settings boundary.

## 5. Experiment domain model

Create a focused pure-core experiment model under an Advisor sub-namespace/directory, for example:

```text
src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/
  InvestigationExperiment.cs
  InvestigationExperimentState.cs
  InvestigationExperimentCoordinator.cs
  InvestigationExperimentValidity.cs
```

Exact file names may differ if existing project conventions favor another arrangement, but the state machine must remain outside `AdvisorSystem` so that the game system does not become a monolithic workflow implementation.

### 5.1 `InvestigationExperiment`

The active/completed experiment should retain only immutable or safely serializable domain values.

Required conceptual fields:

```text
ExperimentId
SessionId
StartedAtUtc
CompletedAtUtc?

BaselineCaptureId
BaselineEvidence

SettingId
SettingDisplayName
OriginalValue
TestedValue
ApplyBehavior

ChangeAppliedAtUtc?
FollowUpCaptureId?
FollowUpEvidence?

State
Validity
InvalidationReason?
Comparison?
```

The experiment must not retain Unity `Entity`, `World`, setting-object instances, or other live game object references.

`BaselineEvidence` is copied/projected at experiment creation so the experiment does not become invalid merely because the bounded Runtime capture store later evicts the baseline capture.

### 5.2 Experiment states

Use explicit machine-readable states. A suitable model is:

```text
Idle / no experiment
BaselineReady
AwaitingApplyConfirmation
ChangeApplied
AwaitingFollowUp
FollowUpCapturing
Comparing
Completed
Cancelled
Invalidated
```

`Comparing` may be transient if comparison is synchronous, but retaining an explicit state is acceptable where it improves UI/test clarity.

State transitions must be deterministic and pure-testable.

### 5.3 Validity

Validity is distinct from workflow state.

Suggested values:

```text
Valid
Invalidated
```

with a machine-readable reason, such as:

```text
SessionChanged
AdditionalAdvisorSettingChanged
TestedSettingExternallyModified
TestedSettingNoLongerMatchesExpectedValue
BaselineUnavailableAtStart
RecommendationBecameStaleBeforeApply
FollowUpCaptureInvalid
FollowUpCaptureInterrupted
FollowUpCaptureWrongSession
ExperimentCancelled
```

Do not encode user-facing localized prose as the authoritative reason identifier.

## 6. Starting an experiment

An experiment starts from a currently diagnosed recommendation in Performance Advisor.

UI action: `Test change` / equivalent localized label.

The start request must validate all of the following:

1. an active city session exists;
2. the current Advisor diagnosis references a completed capture from that session;
3. the recommendation still belongs to the current diagnosis;
4. the recommendation has an apply capability supported by the existing setting gateway;
5. the recommendation represents an actual value change;
6. no other experiment is active;
7. baseline evidence can be projected successfully.

On success:

- generate an `ExperimentId`;
- freeze the current `SessionId`;
- freeze `BaselineCaptureId` and projected `BaselineEvidence`;
- freeze the selected recommendation identity, original value and proposed/test value;
- transition to `BaselineReady` or `AwaitingApplyConfirmation` depending on apply requirements.

Starting an experiment does not itself modify the setting.

## 7. Applying the test change

The test change must call the existing Advisor setting operation path.

Conceptual API:

```text
ApplyExperimentChange(confirmed)
```

Behavior:

- revalidate that the experiment is active and not invalidated;
- revalidate that the recommendation/expected current setting state is still current enough for the existing apply policy;
- delegate to existing `AdvisorSettingOperations.Apply(...)`;
- if existing confirmation is required, remain in a confirmation-waiting state until explicit confirmation;
- if Apply fails, preserve the failure as an experiment-visible action result and do not advance to follow-up;
- on success, record the exact applied time and transition to `ChangeApplied` / `AwaitingFollowUp`.

The experiment does not introduce a second setting-change ledger. The existing `SettingChangeSession` remains authoritative for setting mutations and conflicts.

## 8. Single-change integrity and contamination detection

### 8.1 Additional Advisor-applied setting change

While an experiment is active after its baseline is frozen, applying any **other** Advisor setting invalidates the single-change experiment.

This includes a second recommendation applied through the normal Advisor controls.

The ordinary setting operation may still proceed if normal Advisor rules allow it, but the active experiment transitions to `Invalidated` with `AdditionalAdvisorSettingChanged`.

The system must not silently continue and attribute later differences to the original experimental setting.

### 8.2 Tested setting externally modified

If the tested setting's observed value changes outside the expected experiment path after Apply, use the existing setting conflict observation where possible and invalidate the experiment.

Examples:

- user changes the tested setting in the standard Options UI;
- another source modifies the same setting;
- a preset operation changes the tested value unexpectedly.

Reason: `TestedSettingExternallyModified` or the more specific machine-readable equivalent.

### 8.3 Other game-state changes

The mod cannot reliably detect every city-state difference that may affect performance. Traffic, camera, simulation state and population can naturally change.

These normal simulation changes do **not** automatically invalidate the experiment. Instead the UI always shows the non-causal comparison disclaimer.

Do not add fragile heuristics that pretend to guarantee identical simulation conditions.

## 9. Stabilization period

After a test change is applied, the experiment UI should show a short recommended stabilization period before asking the user to capture follow-up evidence.

Initial recommended duration: **5 seconds**.

This is guidance/UI state, not a guarantee that the game is fully stabilized.

Requirements:

- the mod does not automatically start the follow-up capture when the period ends;
- the user may wait longer;
- the user may cancel the experiment;
- the UI may show elapsed/remaining readiness information if doing so can be implemented without introducing unnecessary timing complexity;
- no causality or comparability guarantee is attached to the five-second duration.

The implementation plan may choose whether this is represented as an explicit state or a timestamp-derived readiness flag.

## 10. Follow-up capture association

The experiment must deliberately associate a follow-up capture; it must not simply use any arbitrary next completed capture.

### 10.1 Only explicit experiment/manual capture qualifies

Automatic capture triggers must not be silently adopted as the experiment follow-up.

The preferred UX is an experiment action such as:

`Start follow-up capture`

which internally requests the existing manual capture path and records the identity of the capture started by that request.

If the current Runtime API cannot return the new capture identity directly, implementation may add a narrow correlation contract/event so the experiment can identify the manual capture it requested. Do not infer identity from an unbounded "latest capture" lookup when concurrent/automatic capture behavior could make that ambiguous.

### 10.2 Follow-up requirements

A candidate follow-up must satisfy at least:

- same diagnostic `SessionId` as the experiment;
- started after the test change was successfully applied;
- originated from the experiment's explicit manual capture request;
- completed with enough evidence for normal Advisor projection/comparison;
- was not abandoned by a city-session change.

If a capture ends early for a supported reason but remains a completed Runtime capture, the implementation must make the status visible and may still compare available metrics if the ordinary evidence projector supports them. It must not hide the interrupted status or fabricate missing metrics.

If the capture is unusable, the experiment remains unable to complete and reports `FollowUpCaptureInvalid` / `FollowUpCaptureInterrupted` as appropriate. The user may request another follow-up capture if the state machine can do so without ambiguity.

## 11. City-session lifecycle

Experiments are scoped to the current loaded-city diagnostic session.

When `Mod.Sessions.Generation` changes or no gameplay session is active:

- an active experiment must no longer accept Apply/capture/compare actions;
- it transitions to `Invalidated` or is cleared after publishing an invalidation result long enough for UI/export visibility;
- the tested setting is **not automatically reverted** merely because the city changed, because game settings are global and the existing Advisor change session already owns safe Undo semantics;
- the user may still use normal Advisor session Undo later if the setting change remains tracked and safe.

Baseline and follow-up evidence from different city sessions must never be compared as one experiment.

## 12. Comparison behavior

The experiment uses the existing `AdvisorComparison.Compare(...)` implementation.

Do not create a second threshold set or second metric-direction table.

The result retains existing states:

```text
Improved
Regressed
NoMaterialChange
NotComparable
```

Existing measurement-quality and unit compatibility rules remain authoritative.

### 12.1 No overall winner/score

Do not produce:

- an overall performance score;
- an experiment "success percentage";
- a weighted winner;
- an automatic Keep/Undo recommendation solely from the metric counts.

The summary may report neutral counts, for example:

```text
2 improved
1 regressed
3 unchanged
1 not comparable
```

The user makes the final Keep/Undo decision.

### 12.2 Multiple-change flag

A valid first-version experiment should normally contain one setting change.

If the reused `AdvisorComparison` nevertheless reports multiple changed settings because the authoritative change ledger observed more than one qualifying mutation, surface that as an invalid/contaminated experiment rather than presenting it as a valid single-setting result.

## 13. Completing the experiment

### 13.1 Keep

`Keep change` means:

- leave the tested game setting at its current value;
- mark the experiment `Completed`;
- retain the result in the current in-memory Advisor state/export data;
- do not remove or falsify the underlying `SettingChangeSession` record.

A kept change may still be undoable through the ordinary Advisor session-change controls if existing semantics allow it.

### 13.2 Undo

`Undo change` delegates to the existing single-setting Undo path.

If Undo succeeds:

- mark the experiment completed with an outcome indicating Undo;
- retain the measured comparison as historical evidence for the current session/report.

If Undo requires confirmation, use existing confirmation semantics.

If the setting was externally modified and safe Undo is blocked:

- present the existing conflict-resolution choices;
- do not silently restore the baseline value;
- keep the experiment result visible while conflict resolution is pending.

### 13.3 Cancel

Before the test change is applied, Cancel simply terminates the experiment.

After the test change is applied, Cancel must **not** imply the value was restored. The UI must distinguish:

- `Cancel experiment and keep current setting`;
- `Undo tested change`.

If the product uses one Cancel action, it must clearly state that cancellation does not automatically undo the applied game setting.

## 14. UI design

The feature remains inside the existing **Performance Advisor** top-level area. Do not add another top-level panel tab.

### 14.1 Recommendation card

Applicable recommendation cards gain a `Test change` action in addition to normal `Apply`.

Do not show `Test change` for:

- read-only recommendations;
- no-op recommendations;
- stale/unavailable diagnosis states;
- cases where an experiment is already active.

### 14.2 Experiment card

When active, show a prominent but compact experiment card above the normal recommendation groups.

Example:

```text
Investigation Experiment — Step 3 / 5

Depth of Field
High -> Disabled

✓ Baseline captured
✓ Test change applied
● Capture follow-up
○ Compare
○ Keep / Undo

[Start follow-up capture] [Cancel]
```

The step labels are presentation, not the authoritative state model.

### 14.3 Result card

After comparison:

```text
Experiment completed

Tested setting
Depth of Field
High -> Disabled

Observed after the change
2 improved
1 regressed
3 unchanged
1 not comparable

[Keep change] [Undo change]
```

Then list metric-level existing comparison rows.

Always include a concise notice equivalent to:

> Improvement or regression was observed after this change. The city simulation may also have changed; this comparison does not establish causality.

### 14.4 Existing Advisor controls

Normal Advisor controls remain visible unless a specific action would violate the experiment's single-change integrity.

Preferred behavior:

- diagnosis details remain readable;
- recommendations remain readable;
- applying a different recommendation is either disabled with an explanation or allowed while explicitly invalidating the experiment. The implementation should prefer preventing accidental contamination in the UI while still defending against backend contamination if another path performs the change;
- normal captures outside the explicit experiment follow-up must not be mistaken for the follow-up.

## 15. Backend/UI commands and projection

Exact names may follow current binding conventions, but the backend needs commands equivalent to:

```text
startExperiment(captureId, settingId, proposedValue)
applyExperimentChange(confirmed)
startExperimentFollowUpCapture()
cancelExperiment()
keepExperimentChange()
undoExperimentChange(confirmed)
```

The Advisor UI projection should expose a serializable experiment section with at least:

```text
experimentId
state
validity
invalidationReason
baselineCaptureId
followUpCaptureId
settingId
settingDisplayName
originalValue
testedValue
changeAppliedAtUtc
stabilizationReadyAtUtc
comparison
completionOutcome
```

Do not pass live game-setting objects to the frontend.

## 16. Runtime capture integration

Avoid coupling the pure experiment state machine directly to `CaptureRuntimeSystem`.

`AdvisorSystem` or a narrow game-integration coordinator should bridge:

- experiment request;
- manual capture request;
- capture-start identity;
- capture completion;
- evidence projection.

If necessary, add a small capture lifecycle notification/correlation API to `CaptureRuntimeSystem`. Keep it generic enough to express "the manual capture created by this explicit request" without embedding UI concerns in the Runtime profiler.

Do not alter automatic-capture behavior merely to support experiments.

## 17. Export

Extend the Advisor/unified JSON report with an optional experiment section.

Conceptual form:

```json
{
  "experiment": {
    "experimentId": "...",
    "state": "Completed",
    "validity": "Valid",
    "baselineCaptureId": "...",
    "followUpCaptureId": "...",
    "settingId": "...",
    "settingDisplayName": "Depth of Field",
    "originalValue": "High",
    "testedValue": "Disabled",
    "completionOutcome": "Kept",
    "comparison": { }
  }
}
```

Rules:

- preserve existing privacy sanitization;
- do not export private game-setting implementation objects or hidden/internal values;
- machine-readable invalidation reason IDs may be exported;
- comparison semantics remain the same as existing Advisor export;
- absent experiment data remains absent/null rather than zero/default data that looks observed.

If the unified schema version policy requires a schema-version increment for the additive field, update it deliberately and test compatibility expectations. Do not change schema version mechanically without checking the project's existing versioning policy.

## 18. Localization

All new visible UI strings require English and Japanese entries in the existing localization system.

Machine-readable backend state/reason IDs remain English identifiers and are mapped to localized UI text.

At minimum localize:

- Test change;
- experiment title/status/steps;
- apply test change;
- stabilization guidance;
- start follow-up capture;
- experiment invalidated reasons;
- Keep change;
- Undo change;
- Cancel experiment;
- completion outcome;
- neutral result counts;
- non-causality notice.

Do not introduce new backend free-form Japanese diagnostic strings; backend/export canonical text remains English where the existing project follows that rule.

## 19. Failure handling

Experiment failures become Advisor-visible diagnostic/action state and are logged through the project's existing failure-reporting convention where an exception is involved.

Examples:

- missing current diagnosis;
- stale recommendation;
- setting gateway write failure;
- capture request rejected;
- capture correlation failure;
- evidence projection failure;
- city session changed;
- conflict detected.

A handled experiment failure must not crash the UI system or disable Runtime/Asset subsystems.

## 20. Performance requirements

The experiment feature is primarily state orchestration and should add negligible continuous overhead.

Requirements:

- no additional continuous profiler recorders;
- no full-world scans;
- no polling of expensive APIs every frame solely for the experiment;
- reuse normal Advisor/UI refresh cadence where possible;
- setting integrity checks should reuse existing change/conflict observation mechanisms rather than inventing broad reflection scans;
- experiment history remains bounded: the initial version only needs the current/latest experiment result in memory unless implementation finds an already-bounded Advisor history abstraction suitable for reuse.

## 21. Testing strategy

Implementation in Work must use TDD and add focused regression tests.

### 21.1 Pure/core tests

Cover at least:

1. start from valid baseline/recommendation;
2. reject start without active session;
3. reject second concurrent experiment;
4. state transition from baseline -> applied -> awaiting follow-up -> completed;
5. confirmation-required Apply does not advance before confirmation;
6. failed Apply does not advance;
7. second Advisor setting change invalidates the experiment;
8. tested setting external modification invalidates the experiment;
9. session change invalidates the experiment;
10. automatic/unrelated capture cannot become follow-up;
11. wrong-session capture cannot become follow-up;
12. follow-up capture before setting Apply cannot qualify;
13. baseline evidence remains available even if original capture leaves the bounded capture store;
14. valid follow-up uses existing `AdvisorComparison` semantics;
15. multiple qualifying changes cannot be presented as a valid single-setting experiment;
16. Keep completes without changing the setting again;
17. Undo delegates to existing safe Undo semantics;
18. conflict blocks silent Undo;
19. Cancel before Apply has no setting side effect;
20. Cancel after Apply does not falsely claim the setting was restored.

### 21.2 Runtime/integration tests

Where game-independent contracts allow, cover:

- explicit manual-capture correlation identity;
- automatic capture ignored by experiment association;
- city-session generation boundary;
- `CaptureRuntimeSystem` existing behavior unchanged when no experiment is active.

Any tests requiring CS2 managed assemblies remain in the adapter-test layer.

### 21.3 UI tests

Cover at least:

- `Test change` appears only on eligible recommendations;
- active experiment card and step/status rendering;
- another recommendation cannot be accidentally applied without an explicit contamination outcome;
- stabilization guidance;
- follow-up capture action;
- invalidation reason rendering;
- neutral metric-count summary;
- metric-level comparison rendering;
- causal disclaimer always visible with completed results;
- Keep/Undo actions;
- conflict UI compatibility;
- English/Japanese localization coverage.

### 21.4 Export tests

Cover:

- valid completed experiment serialization;
- invalidated experiment serialization;
- missing experiment stays absent/null;
- comparison values/states match existing Advisor comparison output;
- privacy sanitizer still protects the full unified report.

## 22. Manual in-game validation

Automated tests do not replace in-game validation.

At minimum validate:

1. start an experiment from a real completed capture;
2. test a confirmation-free setting if available;
3. test a confirmation-required setting such as the supported graphics-quality preset path;
4. wait and start the explicit follow-up capture;
5. verify an unrelated automatic capture is not adopted as follow-up;
6. verify result values correspond to the two intended captures;
7. Keep leaves the tested value in place;
8. Undo restores the original value when safe;
9. modifying the tested setting in Options causes visible invalidation/conflict rather than silent overwrite;
10. attempting another recommendation during the experiment cannot produce a falsely valid single-setting result;
11. loading another city invalidates the experiment and never compares cross-session evidence;
12. export contains the experiment and no unexpected private data;
13. Japanese and English UI are readable and not clipped in normal supported UI scales;
14. ordinary Advisor diagnosis/Apply/Undo/comparison still works with no active experiment.

## 23. Compatibility and migration

This feature does not persist active experiments across restarts, so no migration of old experiment state is required.

Existing Advisor settings/change behavior remains the source of truth. No existing saved setting ID changes are required by this feature.

The implementation must not break legacy report fields unnecessarily. If export schema version changes, document the reason and keep the change additive wherever practical.

## 24. Expected implementation areas

The Work implementation is expected to touch/create files in these areas:

```text
src/CS2RuntimeAssetAuditor/Core/Advisor/Experiment/**
src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs
src/CS2RuntimeAssetAuditor/Advisor/Settings/** (only where hooks are needed)
src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs (narrow correlation hook if needed)
src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs
src/CS2RuntimeAssetAuditor/Export/**
UI/src/profiler/tabs/PerformanceAdvisorTab.tsx
UI/src/profiler/bindings.ts or current Advisor binding contracts
UI/src/i18n/messages.ts
relevant C#/UI/export/adapter tests
README.md only if user-facing behavior needs documentation
```

Avoid unrelated refactors. If implementation discovers a repeated structural problem in the same area, redesign that local abstraction rather than layering repeated one-off patches.

## 25. Work execution requirements

Implementation is intentionally deferred to ChatGPT Work.

The implementation plan created after this specification is approved must instruct Work to:

- use Native / `executing-plans` mode;
- implement directly against `pengin0503/CS2-Runtime-Asset-Auditor`;
- use `main` unless the Work environment requires a temporary isolated worktree;
- not wait for user approval between implementation tasks;
- follow RED -> GREEN -> verification -> commit for each task;
- keep changes scoped to this Guided Investigation Experiment feature and necessary regressions;
- run all game-independent .NET and UI verification available;
- run adapter/Release validation when the required CS2 managed DLL/toolchain is available;
- clearly record any remaining real-game validation rather than reporting it as passed;
- commit completed work to the repository.

## 26. Definition of done

The feature is complete when all of the following are true:

1. a user can start a one-setting experiment from an eligible diagnosed Advisor recommendation;
2. baseline evidence is frozen and remains usable independently of bounded capture-store eviction;
3. the test change uses existing verified Apply/confirmation semantics;
4. the experiment cannot silently remain valid after another performance setting change or tested-setting external modification;
5. an explicitly requested manual capture is reliably associated as the follow-up, while unrelated/automatic captures are ignored;
6. baseline and follow-up must belong to the same loaded-city session;
7. comparison reuses `AdvisorComparison` and preserves `Improved/Regressed/NoMaterialChange/NotComparable` semantics;
8. UI reports metric results and neutral counts without an overall performance verdict or causal claim;
9. Keep and Undo work through existing safe setting semantics;
10. cancellation does not falsely imply an applied setting was restored;
11. experiment data is represented in unified export with privacy behavior preserved;
12. English/Japanese UI coverage is complete;
13. existing Runtime, Advisor, Asset and export regression suites remain green;
14. Work records any game-dependent validation that could not be executed rather than claiming completion without evidence.
