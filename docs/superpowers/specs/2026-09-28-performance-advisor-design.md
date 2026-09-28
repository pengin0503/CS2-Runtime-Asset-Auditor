# CS2 Runtime Profiler — Performance Advisor Design Specification

**Date:** 2026-09-28  
**Status:** Implementation staged on `feature/performance-advisor`; in-game validation and full Release build pending
**Target project:** `pengin0503/CS2-Runtime-Profiler`  
**Target game baseline:** Cities: Skylines II 1.6.2-era runtime, with capability-oriented handling for later versions

## 1. Purpose

Performance Advisor extends CS2 Runtime Profiler from measurement-only diagnostics into a guided, user-controlled tuning workflow.

The feature does **not** automatically optimize the game. Instead, the player explicitly starts a diagnosis, reviews evidence-backed recommendations, manually applies selected changes, and can then re-run the diagnosis to compare before/after results.

The intended workflow is:

```text
Manual diagnosis
  -> Runtime measurements
  -> Bottleneck classification
  -> Evidence-backed setting recommendations
  -> User selects Apply on individual recommendations
  -> Optional undo
  -> Re-diagnosis
  -> Before/after comparison
```

The primary questions Performance Advisor should answer are:

- Which currently adjustable game settings are plausible candidates to reduce because the measured bottleneck is consistent with their known performance impact?
- Which settings appear to have headroom and may be raised without an obvious current bottleneck, subject to re-measurement?
- Which settings should remain unchanged because available evidence does not justify a recommendation?
- What evidence supports each recommendation?
- What changed after the user applied one or more recommendations?
- Did the measured runtime condition improve, worsen, or remain effectively unchanged?

## 2. Relationship to Runtime Profiler

Performance Advisor is a **new Runtime Profiler feature**, not a separate mod.

Runtime Profiler remains the source of runtime evidence. Performance Advisor consumes normalized profiler outputs rather than independently sampling the game.

Conceptually:

```text
Collectors / Profiling / Capture Store
                |
                v
      Bottleneck Diagnosis
                |
                v
      Recommendation Engine
                |
                v
          Advisor UI
                |
                v
      Game Settings Gateway
```

The existing profiler remains usable without Performance Advisor. A failure in Advisor logic or a setting adapter must not break monitoring, capture, export, or existing profiler UI.

## 3. Core design principles

### 3.1 User control over automation

Performance Advisor never changes a game setting solely because a diagnosis was produced.

Every change requires a direct user action such as pressing an `Apply` button for a specific recommendation.

There is no background auto-tuning, adaptive governor, automatic bulk apply, or automatic rollback.

### 3.2 Standard-settings boundary

The Advisor may only manipulate settings that are exposed to the player through Cities: Skylines II's standard in-game settings/options UI.

The Advisor must not expose or mutate:

- hidden or developer-only tuning values;
- private ECS internals;
- undocumented simulation constants;
- internal system update intervals that the normal settings UI does not expose;
- arbitrary private fields solely because Reflection makes them writable;
- third-party mod configuration unless separately designed and explicitly supported in the future.

If a value cannot be demonstrated to correspond to a normal user-facing game setting, it is out of scope for direct modification.

### 3.3 Recommendation scope is narrower than access scope

The Settings Gateway should aim to support all standard game settings that the player can normally change in the in-game settings UI.

However, the Recommendation Engine only produces performance recommendations for settings whose performance relationship can be justified by available runtime evidence and a documented rule.

For example, audio volume or key bindings may be accessible through the Settings Gateway but must not receive meaningless performance recommendations.

### 3.4 Evidence before recommendation

Every recommendation must carry explicit evidence and a confidence level.

The Advisor must not infer a recommendation from one ambiguous metric when the profiler already reports that metric as unavailable, indirect, degraded, or low confidence.

### 3.5 Reversible user actions

Every setting change applied through the Advisor must capture the value that existed immediately before the change.

The user must be able to undo an individual Advisor-applied change while it is still safe to do so.

The user must also be able to request a session-level undo for the set of changes applied during the current Advisor tuning session.

### 3.6 Respect external changes

If a setting is modified after the Advisor applies a recommendation—whether through the normal game settings UI or another source—the Advisor must not silently overwrite that newer value during undo.

Instead, it must report the conflict and require an explicit choice before restoring the original value.

### 3.7 Honest headroom language

A recommendation to lower a setting and an observation that a setting may have headroom are not equivalent claims.

The UI should distinguish:

- `Lower recommended`
- `Keep current`
- `Headroom available`
- `No recommendation`

`Headroom available` means that current evidence does not show an obvious constraint preventing an increase. It is not a guarantee that increasing the setting is free.

The UI should encourage re-diagnosis after an increase.

## 4. Non-goals

The initial Performance Advisor will not:

- automatically tune settings in the background;
- automatically apply all recommendations;
- modify hidden or internal game parameters;
- modify simulation system internals that are not user-facing settings;
- disable mods or assets;
- change third-party mod settings;
- claim that one setting is the proven cause of a slowdown when only correlation or general performance characteristics are known;
- guarantee that a headroom recommendation has zero cost;
- replace the game's settings UI as the authoritative owner of configuration;
- use Asset Performance Auditor as a required dependency.

Optional future integration with Asset Performance Auditor may provide additional evidence, but the initial Advisor must work independently.

## 5. High-level architecture

```text
+--------------------------------------------------+
| Existing Runtime Profiler                        |
|                                                  |
| Collectors -> Profiler Engine -> Capture Store   |
|                      |                           |
+----------------------|---------------------------+
                       v
+--------------------------------------------------+
| Advisor Diagnosis Layer                          |
|                                                  |
| BottleneckClassifier                             |
| EvidenceNormalizer                               |
| RecommendationEngine                             |
+----------------------|---------------------------+
                       v
+--------------------------------------------------+
| Settings Integration Layer                       |
|                                                  |
| GameSettingCatalog                               |
| SettingAdapterRegistry                           |
| SettingChangeSession                             |
| Conflict Detection                              |
+----------------------|---------------------------+
                       v
+--------------------------------------------------+
| Advisor UI                                       |
|                                                  |
| Diagnosis summary                                |
| Recommendation groups                            |
| Apply / Undo / Details                           |
| Session changes                                  |
| Re-diagnose / Compare                            |
+--------------------------------------------------+
```

The diagnosis and recommendation layers must not depend directly on concrete CS2 settings objects. They operate on stable domain records and setting identifiers.

## 6. Diagnosis model

Performance Advisor consumes completed or explicitly bounded profiling evidence.

The initial diagnosis should consider, where available:

- frame time and FPS;
- GPU timing or reliable GPU-pressure evidence;
- simulation efficiency;
- main-thread / simulation timing;
- render-thread timing;
- memory and GC behavior;
- system timing distributions;
- pathfinding and domain counters where relevant;
- profiler measurement confidence and capability state;
- profiler self-overhead.

A diagnosis produces one or more bottleneck observations rather than one forced global label.

Example:

```text
BottleneckObservation
  Category: Rendering/GPU
  Severity: High
  Confidence: High
  Evidence:
    Frame P95 = 25.8 ms
    GPU pressure = High
    Simulation efficiency = 0.97
```

Multiple bottlenecks may coexist.

## 7. Recommendation model

A recommendation is a first-class domain object.

Conceptually:

```text
SettingRecommendation
  SettingId
  DisplayName
  CurrentValue
  RecommendedValue
  Direction
  Priority
  Confidence
  Rationale
  Evidence[]
  ApplyCapability
  ApplyBehavior
```

### 7.1 Direction

Allowed initial values:

- `LowerRecommended`
- `KeepCurrent`
- `HeadroomAvailable`
- `NoRecommendation`

### 7.2 Priority

Recommended grouping:

- `High`
- `Medium`
- `Low`

Priority describes likely usefulness under the current diagnosis, not an absolute ranking of game settings.

### 7.3 Confidence

Recommendations should preserve the profiler's evidence quality and derive a recommendation confidence such as:

- `High`
- `Medium`
- `Low`
- `InsufficientEvidence`

The exact mapping must be deterministic and testable.

### 7.4 Recommendation rules

Rules must be explicit and independently testable.

Examples of rule inputs include:

- rendering/GPU pressure plus available graphics setting state;
- VRAM/memory headroom plus texture-related settings;
- CPU/simulation bottleneck combined with evidence that lowering a purely GPU-bound setting is unlikely to address the current bottleneck;
- degraded or unavailable measurements suppressing a recommendation.

A rule may recommend a single-step change rather than jumping directly from the highest to lowest value.

The initial design should prefer conservative one-step recommendations where the setting has ordered quality levels.

## 8. Game Setting Catalog and boundary enforcement

The project must maintain a runtime catalog of settings that are exposed through the normal game settings UI and that can be safely read.

Each catalog entry should include, where discoverable:

```text
GameSettingDescriptor
  SettingId
  Category
  DisplayName
  ValueType
  CurrentValue
  AllowedValues / Range
  IsUserFacing
  IsReadable
  IsWritable
  ApplyBehavior
  RestartRequirement
  CapabilityState
```

Only entries verified as normal user-facing game settings can be writable through the Advisor.

A writable adapter must fail closed if the current game version no longer exposes the expected setting safely.

## 9. Setting adapter architecture

Concrete game integration lives behind adapters.

Conceptually:

```text
SettingAdapterRegistry
  -> Graphics adapters
  -> Display adapters
  -> Gameplay/user-facing performance-related adapters
  -> Other standard setting adapters where safe
```

Each adapter is responsible for:

- reading the current value;
- validating a proposed value;
- applying the value through the same supported game setting pathway where practical;
- reporting whether application is immediate, deferred, confirmation-sensitive, or restart-requiring;
- verifying the post-apply value;
- reporting failure without corrupting the Advisor session.

Recommendation logic must not call game setting APIs directly.

## 10. Apply behavior categories

Standard settings do not necessarily apply identically.

Each adapter should classify its setting into one of these initial behaviors:

- `Immediate` — change is applied immediately;
- `ApplyRequired` — the game requires an apply/commit action;
- `ConfirmationRequired` — the game normally requires confirmation because the change can affect display usability;
- `RestartRequired` — the value is accepted but takes full effect only after restart;
- `ReadOnlyForAdvisor` — user-facing setting exists, but no safe direct write path is available to the mod.

`ReadOnlyForAdvisor` entries remain visible, but the UI shows instructions or a normal-settings fallback instead of an Apply button.

## 11. Change sessions and undo

All Advisor-applied setting modifications belong to an explicit `SettingChangeSession`.

Conceptually:

```text
SettingChange
  SettingId
  OriginalValue
  AppliedValue
  CurrentObservedValue
  AppliedAt
  Status
```

Possible statuses include:

- `Applied`
- `Undone`
- `ExternallyModified`
- `ApplyFailed`
- `UndoFailed`
- `RestartPending`

### 11.1 Individual undo

Undo is safe only when the current observed value still equals the value the Advisor applied.

Example:

```text
Original = High
Advisor applied = Medium
Current = Medium
```

The Advisor may restore `High` directly.

### 11.2 Conflict-aware undo

If the current value no longer equals the Advisor-applied value:

```text
Original = High
Advisor applied = Medium
Current = Low
```

then the change is marked `ExternallyModified`.

The Advisor must not silently restore `High`.

The UI should offer an explicit choice such as:

- keep the current value;
- restore the pre-Advisor value.

### 11.3 Session undo

`Undo session changes` processes eligible changes in reverse application order.

Conflicted entries are skipped from silent rollback and presented to the user.

The operation must return a per-setting result rather than pretending the entire rollback was atomic when the game's setting APIs do not provide real transaction semantics.

## 12. Advisor UI

Performance Advisor should be a dedicated section/tab within the existing Runtime Profiler UI.

The default view should emphasize the current diagnosis and actionable recommendations rather than raw profiler tables.

Recommended layout:

```text
Performance Advisor

Diagnosis
  Rendering/GPU pressure — High confidence

High priority (2)
  Shadow Quality     High -> Medium   [Apply] [Details]
  Volumetrics        High -> Medium   [Apply] [Details]

Medium priority (3)
  ...

Headroom available (2)
  Texture Quality    Medium -> High   [Apply] [Details]

No recommendation (collapsed)
  ...

Session changes (2)
  Shadow Quality     High -> Medium   [Undo]
  Volumetrics        High -> Medium   [Undo]

[Re-diagnose] [Undo session changes]
```

### 12.1 Grouping and scalability

The UI must remain usable when many standard settings are cataloged.

Recommendations are grouped by state and priority instead of rendering one flat list.

Low-value groups such as `No recommendation` are collapsed by default.

### 12.2 Recommendation details

Expanded details should show:

- current and proposed value;
- direction;
- confidence;
- evidence summary;
- why this setting is related to the measured bottleneck;
- apply behavior;
- whether restart or confirmation is required;
- warning if the recommendation is based on indirect evidence.

### 12.3 Read-only adapter fallback

If a standard game setting is detected but cannot be changed safely through an available API, the Advisor still shows it as part of the setting catalog.

Instead of `Apply`, it shows a non-destructive fallback such as:

- `Open settings` if a supported navigation path exists; or
- a concise instruction identifying the normal game settings category.

## 13. Manual diagnosis workflow

Diagnosis is user-driven.

The initial Advisor workflow should be:

1. User opens Performance Advisor.
2. User starts or selects a bounded diagnosis/capture.
3. Runtime Profiler gathers the required evidence.
4. Advisor produces bottleneck observations and recommendations.
5. User applies zero or more individual recommendations.
6. Advisor tracks the resulting change session.
7. User may undo changes at any time when safe.
8. User initiates re-diagnosis.
9. Advisor compares the new evidence with the selected baseline.

Existing automatic capture may continue to exist for profiler diagnostics, but automatic capture must not automatically trigger setting changes.

## 14. Before/after comparison

The Advisor should support explicit comparison between a baseline diagnosis and a later re-diagnosis.

Comparison should include only metrics that are meaningfully comparable and available in both captures.

Examples:

- mean / P95 frame time;
- FPS where appropriate;
- simulation efficiency;
- GPU timing/pressure if available;
- main-thread / simulation timing;
- GC activity;
- selected domain metrics relevant to the original recommendation.

The result should use neutral language such as:

- `Improved`
- `Regressed`
- `No material change`
- `Not comparable`

Thresholds for `material change` must be explicit and tested rather than based on tiny numerical noise.

The comparison must not claim that a specific setting caused an improvement when several settings changed together.

## 15. Multiple simultaneous recommendations

The Advisor may display many recommendations at once, but the default interaction is individual application.

Initial version requirements:

- each recommendation has its own Apply control;
- each applied change has its own Undo control;
- no default `Apply all recommendations` button;
- session-level undo is supported;
- UI records which settings changed before a re-diagnosis.

This preserves interpretability of before/after results.

A future bulk-apply workflow may be considered separately if it preserves clear attribution and rollback behavior.

## 16. Capability and failure model

Performance Advisor should expose capability state separately for:

- diagnosis inputs;
- recommendation rules;
- setting discovery;
- setting read access;
- setting write access;
- undo verification.

A broken adapter must degrade only that setting.

A broken recommendation rule must not prevent the setting catalog or unrelated recommendations from working.

The existing profiler's fail-open collector philosophy should be preserved.

## 17. Export and diagnostics

Advisor state should be available to the existing diagnostics/export pipeline where practical.

Machine-readable export should be able to record:

- diagnosis summary;
- recommendation set;
- confidence/evidence references;
- setting capability states;
- Advisor-applied changes;
- conflict/undo outcomes;
- baseline and comparison capture identifiers;
- before/after comparison summary.

Exports should not include arbitrary private configuration data unrelated to performance diagnosis.

## 18. Testing strategy

### 18.1 Pure-core tests

The following logic should be testable without the game runtime:

- bottleneck classification;
- recommendation rule evaluation;
- confidence mapping;
- grouping and priority;
- headroom language/state;
- session change tracking;
- conflict detection;
- reverse-order session undo planning;
- before/after comparison thresholds;
- unavailable/degraded evidence behavior.

### 18.2 Adapter contract tests

Where practical, contract tests should verify the expected CS2 settings APIs and member shapes used by adapters.

The tests should fail clearly after a game update when a relied-upon setting API changes.

### 18.3 UI tests

UI tests should cover at least:

- many simultaneous recommendations;
- collapsed low-priority groups;
- Apply -> Applied state;
- individual Undo;
- external-change conflict state;
- session undo results;
- read-only adapter fallback;
- restart/confirmation indicators;
- Japanese localization regression;
- bounded rendering/no runaway update loop.

### 18.4 In-game validation

Release validation should include:

- discovery of the standard game settings catalog;
- direct comparison with the settings visible in the game's own settings UI;
- Apply/Undo for representative immediate settings;
- conflict detection after manually changing the same setting in the standard settings UI;
- restart-required and confirmation-sensitive settings where present;
- diagnosis before/after a graphics setting change;
- verification that no hidden/internal-only setting is exposed as writable;
- verification that existing profiler monitoring/capture remains functional when Advisor support is unavailable.

## 19. Migration and compatibility

Performance Advisor is additive to the existing Runtime Profiler.

Existing profiler settings in `Setting.cs` remain settings for the profiler itself. They must not be conflated with the game's own settings catalog.

Conceptually:

```text
Profiler Setting.cs
  -> monitoring/capture/UI/profiler behavior

Game Settings Gateway
  -> Cities: Skylines II user-facing game settings
```

Existing report/capture data should remain readable unless a separate schema change is explicitly designed.

Advisor-specific export additions should be versioned conservatively.

## 20. Initial implementation boundaries

The first implementation should establish the architecture in a safe order:

1. setting catalog and read-only discovery;
2. pure-core diagnosis/recommendation contracts;
3. Advisor UI with recommendations but no writes;
4. safe writable adapters for standard settings with clear APIs;
5. change session / individual undo / conflict handling;
6. before/after comparison;
7. broader standard-setting adapter coverage;
8. runtime validation and hardening.

The goal is eventually to make every normal in-game setting accessible through the Advisor's settings layer when safe, while keeping recommendations limited to performance-relevant settings with defensible evidence.

## 21. Success criteria

The feature is ready for normal use when it can demonstrate all of the following:

1. A user can explicitly run a diagnosis and receive evidence-backed performance recommendations.
2. Recommendations distinguish lower/keep/headroom/no-recommendation states.
3. The Advisor never changes a setting without explicit user action.
4. Only normal user-facing Cities: Skylines II settings are writable.
5. A user can apply a supported recommendation directly from the Advisor UI.
6. The Advisor records the immediately preceding value for every applied change.
7. Individual undo works when no external modification occurred.
8. External changes are detected and never silently overwritten during undo.
9. Session-level undo reports per-setting outcomes.
10. The UI remains usable with many cataloged settings and multiple simultaneous recommendations.
11. A user can re-diagnose and compare relevant before/after measurements.
12. Unsupported or broken setting adapters degrade independently.
13. Existing Runtime Profiler monitoring, capture, diagnostics, and export continue to operate if Advisor functionality is unavailable.
14. In-game validation confirms that writable Advisor settings correspond to settings available through the game's standard settings UI.
