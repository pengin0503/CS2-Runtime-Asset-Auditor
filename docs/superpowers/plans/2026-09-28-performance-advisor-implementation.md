# Performance Advisor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a manual, evidence-driven Performance Advisor to CS2 Runtime Profiler that diagnoses current bottlenecks, recommends reversible changes to standard in-game settings, lets the user explicitly apply/undo them, and compares before/after captures without automatic tuning.

**Architecture:** Establish the approved safe rollout in layers: first pin and discover the built-in Options settings read-only; then add pure-core diagnosis/recommendation; then expose a read-only Advisor UI; only after that add verified setting writes, conflict-aware undo, comparison, and broader adapter coverage. Keep diagnosis/recommendation/comparison/undo policy in pure-core `Core/Advisor` types and CS2-specific settings behavior behind `Advisor/Settings`, with a thin `AdvisorSystem` and existing UI/export pipelines above them.

**Tech Stack:** C# 9 / .NET Framework 4.8 for the mod, NUnit pure-core tests on .NET 8, metadata contract tests for current CS2 assemblies, React 18 + TypeScript + GameFace/`cs2` UI, Vitest, existing JSON export stack.

**Spec:** `docs/superpowers/specs/2026-09-28-performance-advisor-design.md`

## Global Constraints

- Performance Advisor is a feature of `pengin0503/CS2-Runtime-Profiler`, not a separate mod.
- Target baseline is Cities: Skylines II 1.6.2-era runtime; later versions degrade by capability rather than guessing.
- No background auto-tuning, automatic bulk apply, or automatic rollback.
- Every setting change requires an explicit user action.
- Direct writes are limited to settings exposed by the standard Cities: Skylines II in-game Options UI.
- Never write hidden/developer-only values, private ECS internals, undocumented simulation constants, internal update intervals, or third-party mod settings.
- Recommendation scope is narrower than setting-access scope: settings may be cataloged without receiving a performance recommendation.
- Every Advisor-applied change stores the immediately preceding value and supports conflict-aware undo when safe.
- If the current value differs from the Advisor-applied value, undo must not silently overwrite it.
- A broken setting adapter/rule/capability must not break existing monitoring, capture, export, or profiler UI.
- Asset Performance Auditor is not a required dependency.
- Existing `Setting.cs` remains the configuration for Runtime Profiler itself; it is not the game-settings gateway.
- No new runtime dependency is added unless a later task proves the existing Game/Colossal APIs cannot implement the approved design.

## Review Focus

- **External modification after Apply:** if the player changes a value in the normal Options UI after Advisor Apply, individual/session undo must detect the mismatch and refuse silent restoration. Task 9 pins this with conflict tests.
- **Game update changes setting metadata/API:** missing `SharedSettings`, UI-attribute, setter, or apply members must make only the affected capability read-only/unavailable. Tasks 1–2 and 8 pin this with metadata contract and fail-closed tests.
- **Dynamic visibility/disable/confirmation:** a currently hidden/disabled or confirmation-sensitive option must never be written through a generic unsafe path. Tasks 2 and 8 pin visibility/enablement/apply-behavior tests.
- **Degraded profiler evidence:** unavailable/indirect/low-confidence evidence must suppress strong recommendations rather than become zero or a guessed bottleneck. Tasks 4–5 pin this.
- **Several settings changed before follow-up:** before/after comparison may describe metric movement but must not assign causality to one setting. Task 10 pins this wording/model behavior.

## File Structure

### Pure core

- `src/CS2RuntimeProfiler/Core/Advisor/AdvisorEvidenceSnapshot.cs` — normalized capture evidence consumed by Advisor.
- `src/CS2RuntimeProfiler/Core/Advisor/BottleneckObservation.cs` — bottleneck category/severity/confidence/evidence records.
- `src/CS2RuntimeProfiler/Core/Advisor/GameSettingDescriptor.cs` — stable setting identity/capability/value metadata independent of CS2 objects.
- `src/CS2RuntimeProfiler/Core/Advisor/SettingRecommendation.cs` — recommendation direction/priority/confidence and evidence.
- `src/CS2RuntimeProfiler/Core/Advisor/BottleneckClassifier.cs` — deterministic evidence-to-bottleneck logic.
- `src/CS2RuntimeProfiler/Core/Advisor/RecommendationEngine.cs` — deterministic bottleneck + descriptor to recommendation logic.
- `src/CS2RuntimeProfiler/Core/Advisor/SettingChangeSession.cs` — Apply/Undo bookkeeping and conflict policy.
- `src/CS2RuntimeProfiler/Core/Advisor/AdvisorComparison.cs` — baseline/follow-up metric comparison.
- `src/CS2RuntimeProfiler/Core/Advisor/AdvisorState.cs` — immutable state projected to UI/export.

### CS2 setting integration

- `src/CS2RuntimeProfiler/Advisor/Settings/IStandardGameSettingCatalog.cs` — read-only catalog contract used before writes exist.
- `src/CS2RuntimeProfiler/Advisor/Settings/GameSettingCatalogBuilder.cs` — enumerates built-in `SharedSettings` and creates descriptors.
- `src/CS2RuntimeProfiler/Advisor/Settings/SettingUiMetadataReader.cs` — interprets Options UI attributes/conditions/apply behavior.
- `src/CS2RuntimeProfiler/Advisor/Settings/IGameSettingGateway.cs` — read/catalog/apply/restore contract introduced when writes are enabled.
- `src/CS2RuntimeProfiler/Advisor/Settings/AutomaticSettingAdapter.cs` — safe reflection adapter for reversible standard UI value controls.
- `src/CS2RuntimeProfiler/Advisor/Settings/GameSettingGateway.cs` — adapter registry, current reads, verified writes, fail-closed results.
- `src/CS2RuntimeProfiler/Advisor/AdvisorSystem.cs` — manual diagnosis, recommendation session, apply/undo/re-diagnose orchestration.

### Existing integration points

- `src/CS2RuntimeProfiler/Mod.cs` — register `AdvisorSystem` at `UIUpdate`.
- `src/CS2RuntimeProfiler/UI/UiSnapshot.cs` — add Advisor DTOs/state.
- `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs` — project Advisor state.
- `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs` — bindings/triggers for diagnose/apply/undo/conflict resolution.
- `src/CS2RuntimeProfiler/Export/PerformanceReport.cs` — Advisor report DTOs.
- `src/CS2RuntimeProfiler/Export/ProfilerReportBuilder.cs` — include Advisor state in reports.
- `UI/src/profiler/bindings.ts` — Advisor TypeScript contracts/triggers.
- `UI/src/profiler/ProfilerRoot.tsx` — add `advisor` tab.
- `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx` — Advisor UI.
- `UI/src/profiler/profiler.module.scss` — recommendation/session/conflict layout.

### Tests

- `tests/CS2RuntimeProfiler.AdapterTests/CS2RuntimeProfiler.AdapterTests.csproj`
- `tests/CS2RuntimeProfiler.AdapterTests/SettingsApiContractTests.cs`
- `tests/CS2RuntimeProfiler.Tests/GameSettingCatalogPolicyTests.cs`
- `tests/CS2RuntimeProfiler.Tests/AdvisorDomainTests.cs`
- `tests/CS2RuntimeProfiler.Tests/BottleneckClassifierTests.cs`
- `tests/CS2RuntimeProfiler.Tests/RecommendationEngineTests.cs`
- `tests/CS2RuntimeProfiler.Tests/AdvisorCoordinatorPolicyTests.cs`
- `tests/CS2RuntimeProfiler.Tests/GameSettingGatewayPolicyTests.cs`
- `tests/CS2RuntimeProfiler.Tests/SettingChangeSessionTests.cs`
- `tests/CS2RuntimeProfiler.Tests/AdvisorComparisonTests.cs`
- `tests/CS2RuntimeProfiler.Tests/AdvisorUiProjectionTests.cs`
- `tests/CS2RuntimeProfiler.Tests/AdvisorExportTests.cs`
- `UI/src/profiler/performanceAdvisorRegression.test.ts`

---

### Task 1: Pin the CS2 standard-settings API contract

**Files:**
- Create: `tests/CS2RuntimeProfiler.AdapterTests/CS2RuntimeProfiler.AdapterTests.csproj`
- Create: `tests/CS2RuntimeProfiler.AdapterTests/SettingsApiContractTests.cs`
- Modify: `CS2RuntimeProfiler.sln`

**Interfaces:**
- Produces no runtime code; defines the external API assumptions all setting work relies on.
- Baseline input: installed Cities: Skylines II 1.6.2f1 managed assemblies via `CSII_MANAGEDPATH`.

- [ ] **Step 1: Create failing metadata contract tests**

Using `MetadataLoadContext`, assert the baseline provides:
- `Game.Settings.SharedSettings` built-in roots including general/audio/gameplay/radio/graphics/editor/userInterface/input/userState/keybinding/benchmark/modding;
- `Game.Settings.Setting.Apply()` and `ApplyAndSave()`;
- UI metadata types used by normal Options, including hidden/developer/platform/hide-by-condition/disable-by-condition/advanced/slider/dropdown/confirmation/setter attributes;
- public member metadata sufficient to distinguish reversible value controls from action buttons.

Fail clearly when `CSII_MANAGEDPATH` is absent rather than silently passing.

- [ ] **Step 2: Run contract tests and verify RED for incorrect assumptions**

Run: `dotnet test tests/CS2RuntimeProfiler.AdapterTests/CS2RuntimeProfiler.AdapterTests.csproj -v minimal`

Expected initial state: compile/test RED until the project and exact 1.6.2f1 member names/signatures are correct.

- [ ] **Step 3: Complete the metadata-test project and solution wiring**

Mirror the existing Asset Performance Auditor contract-test pattern: `net48`, C# 9, NUnit, `System.Reflection.MetadataLoadContext`, and no committed game DLLs.

- [ ] **Step 4: Run adapter contracts against local 1.6.2f1 references**

Expected: PASS, zero failures.

- [ ] **Step 5: Commit**

```bash
git add tests/CS2RuntimeProfiler.AdapterTests CS2RuntimeProfiler.sln
git commit -m "test: pin standard game settings API contract"
```

### Task 2: Build the read-only standard game setting catalog

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/GameSettingDescriptor.cs`
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/IStandardGameSettingCatalog.cs`
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/SettingUiMetadataReader.cs`
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/GameSettingCatalogBuilder.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/GameSettingCatalogPolicyTests.cs`

**Interfaces:**
- `IReadOnlyList<GameSettingDescriptor> IStandardGameSettingCatalog.GetCatalog()`.
- `GameSettingDescriptor.SettingId` is `<settings-type-full-name>::<member-name>`; core never stores CS2 runtime objects.
- `SettingUiMetadataReader` reports `IsUserFacing`, `IsCurrentlyVisible`, `IsCurrentlyEnabled`, `ValueKind`, allowed values/range, `ApplyBehavior`, and whether a safe reversible write path appears to exist; Task 2 performs no writes.

- [ ] **Step 1: Write failing catalog-policy tests with fake metadata**

Pin:
- standard reversible bool/int/float/enum/string/keybinding value members are catalogable;
- action buttons are not modeled as reversible settings;
- hidden/developer-only members are not user-facing writable settings;
- dynamic hidden/disabled state is preserved;
- confirmation/restart semantics are preserved;
- lack of a proven write path yields `ReadOnlyForAdvisor`, not a guessed writer.

- [ ] **Step 2: Verify RED**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter GameSettingCatalogPolicyTests -v minimal`

- [ ] **Step 3: Implement read-only catalog from `SharedSettings` + official UI metadata**

The catalog source is the game's built-in `SharedSettings` instances only, never the global `ModSetting` registry. Reflection may inspect the public built-in setting surface and official UI attributes; it must not turn arbitrary private fields into settings.

- [ ] **Step 4: Run catalog policy + metadata contract tests**

Expected: PASS in both suites; no setting writes occur.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor/GameSettingDescriptor.cs src/CS2RuntimeProfiler/Advisor/Settings tests/CS2RuntimeProfiler.Tests/GameSettingCatalogPolicyTests.cs
git commit -m "feat: discover standard CS2 settings read-only"
```

### Task 3: Add pure Advisor domain contracts

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/AdvisorEvidenceSnapshot.cs`
- Create: `src/CS2RuntimeProfiler/Core/Advisor/BottleneckObservation.cs`
- Create: `src/CS2RuntimeProfiler/Core/Advisor/SettingRecommendation.cs`
- Create: `src/CS2RuntimeProfiler/Core/Advisor/AdvisorState.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AdvisorDomainTests.cs`

**Interfaces:**
- Produces the stable Advisor evidence/diagnosis/recommendation/state records used by Tasks 4–12.
- Value transport uses strings plus `SettingValueKind` from Task 2 so core remains game-assembly independent.

- [ ] **Step 1: Write failing domain tests**

Assert descriptors preserve read/write/apply capability separately; recommendations distinguish `LowerRecommended`, `KeepCurrent`, `HeadroomAvailable`, `NoRecommendation`; unavailable evidence remains unavailable instead of becoming zero.

- [ ] **Step 2: Verify RED**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter AdvisorDomainTests -v minimal`

- [ ] **Step 3: Implement minimal records/enums**

Required types:

```csharp
public sealed class AdvisorEvidenceSnapshot { ... }
public sealed class BottleneckObservation { ... }
public sealed class SettingRecommendation { ... }
public sealed class AdvisorState { ... }
```

Reuse existing profiler confidence/availability terminology where compatible.

- [ ] **Step 4: Run focused and full pure-core tests**

Expected: PASS, zero failures.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor tests/CS2RuntimeProfiler.Tests/AdvisorDomainTests.cs
git commit -m "feat: add Performance Advisor domain model"
```

### Task 4: Project capture evidence and classify bottlenecks

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/CaptureAdvisorEvidenceProjector.cs`
- Create: `src/CS2RuntimeProfiler/Core/Advisor/BottleneckClassifier.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/BottleneckClassifierTests.cs`

**Interfaces:**
- `AdvisorEvidenceSnapshot CaptureAdvisorEvidenceProjector.Project(CaptureSession capture)`.
- `IReadOnlyList<BottleneckObservation> BottleneckClassifier.Classify(AdvisorEvidenceSnapshot evidence)`.
- Consumes existing global/recorder/system/pathfinding/domain metrics plus confidence/availability and profiler self-overhead.

- [ ] **Step 1: Write failing classifier tests**

Cover:
- high frame/GPU pressure with healthy simulation => `RenderingGpu`;
- low simulation efficiency without GPU evidence => CPU/simulation observation, not GPU;
- simultaneous GPU and simulation pressure => multiple observations;
- missing/degraded GPU evidence => no `High` confidence GPU conclusion;
- high profiler self-overhead downgrades Advisor confidence.

- [ ] **Step 2: Verify RED**

Run the focused filter.

- [ ] **Step 3: Implement deterministic projector/classifier**

Keep thresholds as named constants and never convert unavailable data to zero.

- [ ] **Step 4: Verify focused + complete pure-core tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor tests/CS2RuntimeProfiler.Tests/BottleneckClassifierTests.cs
git commit -m "feat: classify profiler bottlenecks for Advisor"
```

### Task 5: Generate evidence-backed setting recommendations

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/PerformanceSettingRule.cs`
- Create: `src/CS2RuntimeProfiler/Core/Advisor/RecommendationEngine.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/RecommendationEngineTests.cs`

**Interfaces:**
- `IReadOnlyList<SettingRecommendation> RecommendationEngine.Build(IReadOnlyList<BottleneckObservation> bottlenecks, IReadOnlyList<GameSettingDescriptor> settings)`.
- `PerformanceSettingRule` contains semantic setting tags, relevant bottleneck category, lowering/headroom eligibility, and evidence/confidence floor; it never writes settings.

- [ ] **Step 1: Write failing recommendation tests**

Pin:
- rendering/GPU bottleneck + performance-relevant graphics descriptor => conservative one-step lower recommendation;
- CPU/simulation-only bottleneck => no claim that lowering a purely GPU setting fixes it;
- verified headroom can produce `HeadroomAvailable`, never an unconditional guarantee;
- degraded/insufficient evidence suppresses strong recommendations;
- unrelated standard settings such as volume/keybindings remain cataloged but get `NoRecommendation`;
- output preserves evidence IDs and confidence.

- [ ] **Step 2: Verify RED**

Run the focused filter.

- [ ] **Step 3: Implement rules + engine**

Rules consume semantic descriptors supplied by the setting catalog. Do not hard-code private game fields.

- [ ] **Step 4: Verify focused + full pure-core tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor tests/CS2RuntimeProfiler.Tests/RecommendationEngineTests.cs
git commit -m "feat: generate evidence-backed setting recommendations"
```

### Task 6: Add a read-only Advisor coordinator and C# UI projection

**Files:**
- Create: `src/CS2RuntimeProfiler/Advisor/AdvisorSystem.cs`
- Modify: `src/CS2RuntimeProfiler/Mod.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AdvisorCoordinatorPolicyTests.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AdvisorUiProjectionTests.cs`

**Interfaces:**
- Register `AdvisorSystem` at `SystemUpdatePhase.UIUpdate`.
- Initial commands are read-only: choose/start diagnosis capture and select baseline.
- `AdvisorState CurrentState` includes catalog, bottlenecks, recommendations, capability state, and selected baseline; no setting mutation commands yet.
- `UiSnapshot.Advisor` projects the same state.

- [ ] **Step 1: Write failing coordinator/projection tests**

Assert:
- only explicitly selected/completed bounded captures are diagnosed;
- unavailable Advisor support does not affect existing profiler snapshot construction;
- read-only setting descriptors/recommendations preserve current/proposed/confidence/apply-capability fields;
- no Apply/Undo trigger exists at this stage.

- [ ] **Step 2: Verify RED**

Run focused Advisor coordinator/UI projection filters.

- [ ] **Step 3: Implement the thin system and read-only bindings**

Keep decisions in `Core/Advisor`; `AdvisorSystem` only obtains captures/catalog and publishes state.

- [ ] **Step 4: Run pure tests and Release build**

Run full pure tests then `dotnet build CS2RuntimeProfiler.sln -c Release`. Expected: PASS/build success.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Advisor src/CS2RuntimeProfiler/Mod.cs src/CS2RuntimeProfiler/UI tests/CS2RuntimeProfiler.Tests
git commit -m "feat: expose read-only Performance Advisor state"
```

### Task 7: Add the read-only Performance Advisor tab

**Files:**
- Create: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
- Create: `UI/src/profiler/performanceAdvisorRegression.test.ts`
- Modify: `UI/src/profiler/bindings.ts`
- Modify: `UI/src/profiler/ProfilerRoot.tsx`
- Modify: `UI/src/profiler/profiler.module.scss`

**Interfaces:**
- Root tab id: `advisor`; Japanese label is `改善提案`.
- Shows diagnosis, high/medium/low recommendations, headroom, no-recommendation group, evidence details, and write capability.
- No Apply/Undo control exists in this task.

- [ ] **Step 1: Write failing UI regression tests**

Assert:
- Advisor tab exists and uses Japanese label `改善提案`;
- high/medium/headroom/no-recommendation groups are distinct;
- `No recommendation` is collapsed by default;
- read-only entries show capability/instruction text;
- no `Apply all`, Apply, or Undo action is wired yet;
- long recommendation lists remain inside the existing scroll viewport;
- GameFace compatibility tests remain satisfied.

- [ ] **Step 2: Verify RED**

Run: `cd UI && npm test -- performanceAdvisorRegression.test.ts`

- [ ] **Step 3: Implement read-only bindings/tab/root/styles**

Reuse existing `cs2/ui` and project GameFace-safe patterns.

- [ ] **Step 4: Run all UI tests + production build**

Run `cd UI && npm test && npm run build`. Expected: zero failures/build errors.

- [ ] **Step 5: Commit**

```bash
git add UI/src/profiler
git commit -m "feat: add read-only Performance Advisor UI"
```

### Task 8: Implement verified standard-setting writes

**Files:**
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/IGameSettingGateway.cs`
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/AutomaticSettingAdapter.cs`
- Create: `src/CS2RuntimeProfiler/Advisor/Settings/GameSettingGateway.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/GameSettingGatewayPolicyTests.cs`
- Modify: `src/CS2RuntimeProfiler/Advisor/AdvisorSystem.cs`

**Interfaces:**
- `IGameSettingGateway.GetCatalog()`, `Read(settingId)`, `Apply(settingId, value)`, `Restore(settingId, expectedCurrentValue, originalValue)`.
- `GameSettingGateway.Apply` returns structured `Succeeded`, `ObservedBefore`, `Requested`, `ObservedAfter`, `ApplyBehavior`, and failure reason.
- Generic adapters honor official UI setter metadata and owning `Setting.ApplyAndSave()` semantics where applicable.

- [ ] **Step 1: Write failing gateway-policy tests with fake adapters**

Pin:
- validate requested value before write;
- custom setter semantics are used when metadata requires them;
- verify post-apply value;
- refuse hidden/disabled/read-only entries;
- confirmation-sensitive setting returns `ConfirmationRequired` until explicitly acknowledged;
- one broken adapter degrades only that setting;
- no bulk Apply API exists.

- [ ] **Step 2: Verify RED**

Run focused gateway tests.

- [ ] **Step 3: Implement safe gateway/adapters and explicit single-setting Apply path**

Action/button controls remain outside reversible value adapters. Any setting lacking a safe path remains `ReadOnlyForAdvisor` rather than receiving a private-field workaround.

- [ ] **Step 4: Run pure tests, adapter contracts, Release build**

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Advisor tests/CS2RuntimeProfiler.Tests/GameSettingGatewayPolicyTests.cs
git commit -m "feat: apply standard settings through verified gateway"
```

### Task 9: Add change sessions, Apply/Undo UI, and external-change conflicts

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/SettingChangeSession.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/SettingChangeSessionTests.cs`
- Modify: `src/CS2RuntimeProfiler/Advisor/AdvisorSystem.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Modify: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`
- Modify: `UI/src/profiler/performanceAdvisorRegression.test.ts`

**Interfaces:**
- `SettingChangeSession.RecordApplied(settingId, originalValue, appliedValue, appliedAt)`.
- `UndoDecision EvaluateUndo(settingId, currentValue)` => `SafeRestore`, `AlreadyRestored`, or `Conflict`.
- New UI triggers: `advisorApply`, `advisorUndo`, `advisorUndoSession`, `advisorResolveConflict`.

- [ ] **Step 1: Write failing core + UI tests**

Cover:
- safe individual undo when current == Advisor-applied value;
- external modification creates conflict and blocks silent restore;
- session undo plans reverse application order;
- conflicted entries are skipped from silent session rollback;
- failed Apply is not recorded as undoable;
- UI offers individual Apply, individual Undo, session Undo, and conflict choices;
- UI contains no `Apply all` control.

- [ ] **Step 2: Verify RED in pure-core and UI test suites**

- [ ] **Step 3: Implement session policy, trigger routing, and manual controls**

The conflict UI choices are `現在値を維持` and `変更前の値へ戻す`. Explicit conflict restoration is a separate acknowledged action.

- [ ] **Step 4: Run full pure tests, full UI tests/build, Release build**

Expected: all pass/build.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor src/CS2RuntimeProfiler/Advisor src/CS2RuntimeProfiler/UI UI/src/profiler tests/CS2RuntimeProfiler.Tests
git commit -m "feat: add conflict-aware Advisor apply and undo"
```

### Task 10: Add re-diagnosis and before/after comparison

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/Advisor/AdvisorComparison.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AdvisorComparisonTests.cs`
- Modify: `src/CS2RuntimeProfiler/Advisor/AdvisorSystem.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Modify: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `UI/src/profiler/bindings.ts`
- Modify: `UI/src/profiler/tabs/PerformanceAdvisorTab.tsx`

**Interfaces:**
- `AdvisorComparison Compare(AdvisorEvidenceSnapshot baseline, AdvisorEvidenceSnapshot followUp, IReadOnlyList<SettingChange> changes)`.
- Per-metric state: `Improved`, `Regressed`, `NoMaterialChange`, `NotComparable`.
- New manual trigger: `advisorRediagnose`.

- [ ] **Step 1: Write failing comparison tests**

Pin:
- meaningful frame-time reduction => `Improved`;
- tiny numerical noise => `NoMaterialChange` via named tested tolerance constants;
- unavailable in either capture => `NotComparable`;
- regression => `Regressed`;
- multiple changed settings => comparison records `MultipleChanges` and has no single-setting causal field;
- definition/capability mismatch between captures => not comparable.

- [ ] **Step 2: Verify RED**

- [ ] **Step 3: Implement calculator, system workflow, and comparison UI**

Use metric-specific directionality: lower frame/CPU/GC time is better; higher simulation efficiency is better.

- [ ] **Step 4: Run full pure/UI tests and Release build**

Expected: all pass/build.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Core/Advisor src/CS2RuntimeProfiler/Advisor src/CS2RuntimeProfiler/UI UI/src/profiler tests/CS2RuntimeProfiler.Tests
git commit -m "feat: compare Advisor baseline and follow-up captures"
```

### Task 11: Close standard-setting coverage gaps safely

**Files:**
- Modify: `src/CS2RuntimeProfiler/Advisor/Settings/GameSettingCatalogBuilder.cs`
- Modify: `src/CS2RuntimeProfiler/Advisor/Settings/SettingUiMetadataReader.cs`
- Modify: `src/CS2RuntimeProfiler/Advisor/Settings/AutomaticSettingAdapter.cs`
- Modify/Create: focused special adapters under `src/CS2RuntimeProfiler/Advisor/Settings/` only where official Options behavior cannot be represented generically.
- Modify: `tests/CS2RuntimeProfiler.AdapterTests/SettingsApiContractTests.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/StandardSettingCoveragePolicyTests.cs`

**Interfaces:**
- Coverage audit compares cataloged reversible standard Options values against writable/read-only adapter capability.
- Release target: every normal reversible in-game setting is cataloged; every one with a safe Options-equivalent write path is writable. Action-only controls are explicitly classified as non-settings, not counted as missing coverage.

- [ ] **Step 1: Write failing coverage tests/audit**

Pin:
- each discovered normal reversible value control appears exactly once in catalog;
- known widget/value kinds have a gateway capability;
- unsupported safe-write cases are explicit `ReadOnlyForAdvisor`, never silently omitted;
- hidden/developer-only values remain non-writable;
- keybinding/display/confirmation-sensitive categories use explicit supported behavior rather than generic unsafe reflection.

- [ ] **Step 2: Run policy + adapter contracts and inspect RED gaps**

Use failures as the concrete list of missing widget categories/adapters.

- [ ] **Step 3: Add category-level adapters, not one-off per-setting patches**

If several failures share the same Options widget semantics, fix the abstraction once. Do not reach into private internal tuning values.

- [ ] **Step 4: Re-run coverage, adapter contracts, pure tests, and Release build**

Expected: no silent catalog omissions; safe supported categories pass.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeProfiler/Advisor/Settings tests/CS2RuntimeProfiler.AdapterTests tests/CS2RuntimeProfiler.Tests/StandardSettingCoveragePolicyTests.cs
git commit -m "feat: complete safe standard-setting Advisor coverage"
```

### Task 12: Export, diagnostics, docs, and end-to-end verification

**Files:**
- Modify: `src/CS2RuntimeProfiler/Export/PerformanceReport.cs`
- Modify: `src/CS2RuntimeProfiler/Export/ProfilerReportBuilder.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AdvisorExportTests.cs`
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-09-28-performance-advisor-design.md` — update implementation status only; do not rewrite approved requirements.

**Interfaces:**
- Export records diagnosis, recommendations, confidence/evidence IDs, setting capability states, Advisor-applied changes, conflict/undo outcomes, baseline/follow-up capture IDs, and comparison summary.
- Export never dumps hidden/private configuration unrelated to performance diagnosis.

- [ ] **Step 1: Write failing export tests**

Assert Advisor report serialization contains approved fields, preserves unavailable values, excludes hidden/unrelated setting internals, and round-trips through `PerformanceReportSerializer`.

- [ ] **Step 2: Verify RED**

Run focused export tests.

- [ ] **Step 3: Extend report builder, diagnostics, README, and implementation status**

Document the exact workflow: diagnose → inspect evidence → Apply individual setting → re-diagnose → compare/Undo. State that normal Options remains authoritative and no automatic tuning occurs.

- [ ] **Step 4: Run the complete verification matrix**

```bash
dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal
dotnet test tests/CS2RuntimeProfiler.AdapterTests/CS2RuntimeProfiler.AdapterTests.csproj -v minimal
cd UI && npm test && npm run build && cd ..
dotnet build CS2RuntimeProfiler.sln -c Release
```

Expected: every command exits 0 with zero test failures/build errors.

Then perform in-game validation on the current CS2 baseline:
- open Advisor and run manual diagnosis;
- compare the catalog against the standard Options UI across General/Graphics/Gameplay/UI/Input and mode-specific built-in pages that are currently visible;
- verify no hidden/developer-only/internal value is writable;
- apply representative immediate settings and verify Options reflects the same values;
- undo and verify exact preceding values return;
- apply a setting, modify it manually in Options, then verify conflict detection prevents silent overwrite;
- verify confirmation-sensitive/display settings preserve their safety behavior;
- verify restart-required state where applicable;
- run follow-up diagnosis and verify before/after comparison;
- temporarily make an Advisor adapter unavailable and verify existing profiler monitoring/capture/export still function;
- export JSON and inspect Advisor fields/privacy.

- [ ] **Step 5: Commit final integration/docs**

```bash
git add src/CS2RuntimeProfiler/Export tests/CS2RuntimeProfiler.Tests/AdvisorExportTests.cs README.md docs/superpowers/specs/2026-09-28-performance-advisor-design.md
git commit -m "docs: finalize Performance Advisor integration"
```

## Execution Notes

- Tasks 1–2 intentionally establish read-only setting discovery before diagnosis or UI depends on it.
- Tasks 3–5 are pure-core and must stay free of Game/Unity references.
- Tasks 6–7 deliberately ship a read-only Advisor slice before any setting mutation exists.
- Tasks 8–9 are the highest-risk section because they introduce writes and reversibility; do not start them until the read-only slice is green.
- Task 11 is a category-level coverage pass, not permission to add private-field hacks for difficult settings.
- Do not broaden scope into Asset Performance Auditor integration, third-party mod settings, hidden configuration, automatic tuning, or simulation-internal optimization during this plan.
- If repeated adapter exceptions occur for the same Options widget category, redesign the category adapter instead of adding one-off fixes.
