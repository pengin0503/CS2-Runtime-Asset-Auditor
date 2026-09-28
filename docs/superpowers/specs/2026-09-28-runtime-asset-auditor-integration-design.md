# CS2 Runtime Asset Auditor — Integration Design Specification

**Date:** 2026-09-28  
**Status:** Approved through implementation planning. Implementation must not begin until the user explicitly approves the plan and continues the work in ChatGPT Work.  
**Target repository:** `pengin0503/CS2-Runtime-Asset-Auditor`  
**Target game baseline:** Cities: Skylines II 1.6.2f1-era managed API/toolchain, with capability-oriented degradation for later builds  
**Runtime Profiler source baseline:** `pengin0503/CS2-Runtime-Profiler@41836ded2c4cafd1eb5c7e8947ae75de737ea848`  
**Asset Auditor source baseline:** `pengin0503/CS2-Asset-Performance-Auditor@5f0650c980175d3e84451ff62227605da2447cd2`

## 1. Purpose

`CS2-Runtime-Asset-Auditor` combines CS2 Runtime Profiler and CS2 Asset Performance Auditor into one diagnostic Code Mod, one repository, one installable product, and one in-game panel.

The integration exists because the two tools observe different parts of the same performance problem:

- Runtime Profiler observes time-dependent runtime behavior: simulation efficiency, profiler markers, ECS system timing, mod ownership, pathfinding, memory/GC, captures, diagnostics, and Performance Advisor evidence.
- Asset Performance Auditor observes asset structure and city exposure: Prefabs, instance census, render graphs, geometry, LODs, surfaces, materials, textures, findings, and peer comparisons.

The merged product must let those evidence sets coexist and be linked without turning correlation into causation. A static asset with high geometry or high instance exposure is not automatically the cause of GPU or frame-time cost. Likewise, a runtime rendering bottleneck does not prove which asset caused it.

The primary user outcome is a single workflow:

1. Observe runtime health.
2. Capture a slowdown or other runtime condition.
3. Inspect systems/mods/pathfinding/advisor evidence.
4. Inspect relevant asset catalog/census evidence in the same city session.
5. Compare the two evidence sets with explicit timestamps, snapshot IDs, and confidence/availability metadata.
6. Export one report that preserves the distinction between runtime measurement and asset analysis.

## 2. Success criteria

The first integrated release is successful when all of the following are true:

1. Only one Code Mod entry point is loaded and only one top-left launcher is shown.
2. Existing Runtime Profiler capabilities remain available: monitoring, Deep Capture, systems, mods, pathfinding, timeline, captures, Performance Advisor, diagnostics, panel movement/resizing, Esc/B close, settings, and JSON export.
3. Existing Asset Auditor capabilities remain available: Prefab catalog, manual census, geometry/LOD/surface/texture analysis, findings, search/filter/sort/paging, comparison, diagnostics, JSON/CSV asset export, and bounded self-telemetry.
4. Heavy Asset Audit work never silently contaminates a Deep Capture. The two operations are coordinated explicitly.
5. Runtime captures and asset snapshots share a city-session identity and timestamp model so the UI/export can show whether evidence belongs to the same loaded city session and how far apart it was collected.
6. The integrated UI has one coherent navigation hierarchy rather than concatenating fourteen flat tabs.
7. The integrated JSON report can contain runtime, advisor, asset, cross-context, capability, and diagnostics sections without falsifying unavailable data as zero.
8. Existing pure/core tests from both repositories are retained or migrated, not discarded.
9. Existing adapter tests remain separated from pure tests where they require game assemblies.
10. The merged product is built as one primary mod assembly and one UI bundle.

## 3. Non-goals

This integration does not add the following merely because the projects are being merged:

- per-asset GPU timing when the runtime does not expose it safely;
- automatic asset modification or optimization;
- automatic disabling of assets or third-party mods;
- a single global performance score;
- a claim that static geometry/texture metadata equals runtime render cost;
- a claim that a Harmony patch owner is responsible for all time measured in a patched vanilla system;
- automatic full-city census during every Deep Capture;
- persistent storage of every Entity reference for every Prefab;
- unrelated refactors outside the code touched by integration;
- deletion or archival of either legacy repository during the implementation itself.

## 4. Approaches considered

### 4.1 Chosen: one primary assembly with modular internal subsystems

Use Runtime Profiler as the starting repository tree because it already owns the persistent monitoring/capture lifecycle and the more mature movable/resizable single-panel shell. Rename the product/assembly/root namespace to `CS2RuntimeAssetAuditor`, then port Asset Auditor into an `Assets` subsystem inside the same assembly.

Logical shape:

```text
CS2RuntimeAssetAuditor
  Mod.cs
  Settings/
  Diagnostics/
  Coordination/
  Runtime/
    Advisor/
    Attribution/
    Collectors/
    Profiling/
    RuntimeUI/
  Assets/
    Core/
    GameIntegration/
    AssetUI/
    Export/
  Export/
  Localization/
UI/
  src/
    shell/
    runtime/
    assets/
    advisor/
    diagnostics/
```

Advantages:

- one assembly and one lifecycle entry point;
- direct in-process sharing of immutable snapshots/context;
- no inter-mod API/versioning problem;
- simplest deployment and user experience;
- Runtime Profiler remains the low-overhead scheduling backbone;
- Asset Auditor keeps its layered domain/game-integration design inside a clear subtree.

Cost:

- one-time namespace/project rename and selective porting work;
- care is required to prevent `ProfilerUISystem` or other mature files from becoming oversized integration dumping grounds.

### 4.2 Rejected: host assembly plus two feature DLLs

A new host could reference separate Runtime and Asset libraries. This preserves more existing namespaces, but creates extra deployment assemblies, API boundaries, duplicated lifecycle/settings concerns, and additional packaging failure modes for little benefit at current project size.

### 4.3 Rejected: two independent mod assemblies distributed together

Shipping both existing mods in one package would not be a real functional integration. It would retain two `IMod` entry points, two settings identities, two launchers, two export schemas, and no authoritative operation coordinator.

## 5. Product identity and repository structure

The integrated product uses:

- Repository: `CS2-Runtime-Asset-Auditor`
- Solution: `CS2RuntimeAssetAuditor.sln`
- Primary project: `src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj`
- Assembly: `CS2RuntimeAssetAuditor`
- Root namespace: `CS2RuntimeAssetAuditor`
- Mod ID/settings ID: `CS2RuntimeAssetAuditor`
- Display name: `CS2 Runtime Asset Auditor`
- UI module identity: `CS2RuntimeAssetAuditor`
- Report directory: `ModsData/CS2RuntimeAssetAuditor/`

The initial import is pinned to the source SHAs named at the top of this document. Work must verify those SHAs before copying so the merge does not accidentally depend on later unrelated changes.

The new repository becomes the source of truth only after integrated verification passes. The two legacy repositories remain unchanged during implementation so they are available for comparison and rollback.

## 6. High-level architecture

```text
Cities: Skylines II
       |
       v
+----------------------------+
| Mod / Settings / Lifecycle |
+-------------+--------------+
              |
     +--------+---------+
     |                  |
     v                  v
+------------+    +----------------+
| Runtime    |    | Asset Audit    |
| Subsystem  |    | Subsystem      |
| monitoring |    | catalog/census |
| captures   |    | render metadata|
+------+-----+    +-------+--------+
       |                  |
       +---------+--------+
                 v
      +-------------------------+
      | Diagnostic Coordination |
      | session + heavy-work    |
      | arbitration + links     |
      +------------+------------+
                   |
         +---------+----------+
         |                    |
         v                    v
+------------------+   +------------------+
| Unified UI Shell |   | Unified Export   |
| runtime/assets/  |   | runtime/assets/  |
| advisor/diag     |   | links/diagnostics|
+------------------+   +------------------+
```

Runtime and Asset subsystems remain independently testable. The integration layer shares only deliberately defined domain contracts, not Unity/CS2 runtime objects.

## 7. Shared diagnostic session model

### 7.1 `DiagnosticSessionContext`

Create a small shared context representing the currently loaded city session.

Required fields:

```text
SessionId          Guid/string generated when a gameplay world is initialized
StartedAtUtc       timestamp
GameVersion        observed runtime version when available
BuildIdentity      mod build identity
```

A new `SessionId` is created when the gameplay world/session is recreated. It is not a persistent city identifier and must not claim to uniquely identify a save file across loads.

Every completed Runtime capture and Asset audit snapshot stores:

- `SessionId`
- `StartedAtUtc`
- `CompletedAtUtc`
- its own stable in-session ID (`CaptureId` or `AssetSnapshotId`)

This provides temporal/contextual linkage without inventing causality.

### 7.2 Cross-context links

A `DiagnosticEvidenceLink` may associate:

- one Runtime capture;
- zero or more Asset snapshots from the same `SessionId`;
- relative timing such as `Before`, `Overlapping`, or `After` based only on timestamps.

The link is descriptive. It must never state that an asset caused the runtime capture condition.

## 8. Heavy diagnostic work coordination

Deep Capture and heavy Asset Audit scans must not compete invisibly for the same frame budget.

Introduce `DiagnosticWorkCoordinator` with two broad classes of work:

- `RuntimeDeepCapture` — latency-sensitive measurement, highest priority.
- `AssetHeavyScan` — catalog/census/deep-inspection work that may be frame-budgeted and resumed/restarted.

Rules:

1. Deep Capture has priority over Asset heavy scans.
2. If a user requests an Asset heavy scan while Deep Capture is active, the request is queued and surfaced as `WaitingForRuntimeCapture`.
3. If Deep Capture starts while an Asset heavy scan is active, the Asset operation must reach a safe cancellation/yield boundary immediately and transition to `InterruptedByRuntimeCapture`. The UI offers restart after capture completion. Do not attempt complex transparent continuation unless the existing scanner can prove snapshot consistency.
4. Lightweight cached Asset reads and UI queries may continue during Deep Capture when they do not enumerate the world or perform deep inspection.
5. Runtime normal monitoring continues while Asset scanning runs; Asset self-telemetry records its own diagnostic overhead so runtime graphs can identify the scan interval.
6. No automatic full Asset census is triggered merely because a Deep Capture started.

This coordination is a functional benefit of the merge and protects measurement quality.

## 9. Runtime subsystem

The integrated Runtime subsystem retains the behavior of the pinned Runtime Profiler baseline, including:

- normal monitoring;
- simulation-speed efficiency;
- Deep Capture triggers and manual capture;
- marker/system timing;
- direct system ownership and patch-owner metadata;
- pathfinding/domain counters;
- bounded history/timeline;
- capture store;
- diagnostics and profiler self-overhead;
- Performance Advisor, apply/undo/conflict semantics, and comparison workflow.

Harmony remains permitted where the Runtime Profiler already requires it for justified managed-boundary observation. Integration must not remove working runtime instrumentation solely because the original Asset Auditor did not use Harmony.

Harmony is not introduced into Asset catalog/census/render-metadata collectors simply because the unified assembly already references `Lib.Harmony`. Asset access continues to prefer ECS/public Game APIs/AssetDatabase/Unity APIs as in the Asset Auditor design.

## 10. Asset subsystem

The integrated Asset subsystem retains the behavior of the pinned Asset Auditor baseline:

- Prefab catalog and stable Prefab identity;
- city census with top-level/subordinate/live/network counts;
- render graph discovery;
- geometry/submesh/topology metadata;
- LOD structure and reduction evidence;
- surface/material/texture observations;
- bounded deep inspection;
- findings and peer comparison;
- capability reporting;
- search/filter/sort/paging;
- JSON asset section and flat CSV asset summary;
- manual/cancellable/frame-budgeted heavy work;
- self-telemetry.

The asset domain types remain under `CS2RuntimeAssetAuditor.Assets.*` rather than being flattened into Runtime DTOs.

## 11. Evidence bridge between Runtime and Assets

The merge adds an explicit bridge, but the bridge is intentionally conservative.

### 11.1 Shared overview context

The main Overview shows:

- current Runtime health summary;
- latest completed Runtime capture in the current session;
- latest completed Asset snapshot in the current session;
- timestamp distance between them;
- whether an Asset scan was running near a Runtime event;
- capability/availability warnings affecting either side.

### 11.2 Runtime-to-Asset investigation handoff

From a selected Runtime capture, the UI can navigate to the Asset area while preserving the selected capture as context. If no same-session Asset snapshot exists, the UI may offer `Run Asset Audit`.

If a capture indicates rendering/GPU pressure, the Asset area may surface existing Asset findings and high-exposure records as **investigation candidates**, with an explicit note that static/census evidence does not measure per-asset runtime GPU cost.

If the runtime capture is primarily simulation/CPU/pathfinding bound, the UI must not elevate geometry/texture findings as though they explain that bottleneck.

### 11.3 Asset-to-Runtime context

Asset details may display the selected Runtime capture's city-level context and timing metadata. They must not display a fabricated per-asset frame-time value.

### 11.4 Advisor relationship

Performance Advisor remains focused on user-changeable game settings and measured runtime evidence. Integration may add a non-mutating diagnostic action such as `Inspect Assets`/`Run Asset Audit` when relevant evidence is missing. Asset findings do not become automatic game-setting changes.

## 12. Unified UI

Use the Runtime Profiler panel shell because it already supports one top-left launcher, persisted position/size, scaling, scrolling, and game `Back` input handling.

Top-level navigation becomes:

1. **Overview** — combined session/runtime/asset status and primary actions.
2. **Runtime** — subviews for Systems, Mods, Pathfinding, Timeline, and Captures.
3. **Assets** — subviews for Catalog/Assets, Census, Findings, and Compare.
4. **Advisor** — existing Performance Advisor workflow.
5. **Diagnostics** — runtime collector diagnostics, asset capability/scan diagnostics, self-overhead, export status, and integration coordinator state.

Settings remain primarily in the game's Options UI. Asset-specific in-panel settings may be retained only where they are operational controls that are materially easier to use next to scans; persistent configuration must have one authoritative settings object.

The standalone Asset Auditor launcher and standalone panel are removed after their content is available in the unified shell.

Accessibility/interaction requirements preserved from the current projects:

- Esc / gamepad B closes the panel through `InputActionConsumer`;
- panel remains movable/resizable;
- body uses the CS2 `Scrollable` control where required for Gameface behavior;
- search/paging avoids rendering an unbounded asset list;
- UI scale remains bounded to the existing supported range;
- Japanese and English labels are available for the unified product.

## 13. UI/backend binding boundaries

Do not turn the existing large `ProfilerUISystem` into a single monolithic class containing all Asset UI logic.

Use separate backend projections/binding groups under one mod identity:

- `CS2RuntimeAssetAuditor.runtime` — runtime snapshot and runtime commands;
- `CS2RuntimeAssetAuditor.assets` — asset snapshot/query/scan/export commands;
- `CS2RuntimeAssetAuditor.shell` — panel visibility/layout/shared session/coordinator status when separation is useful.

The frontend composes these sources into one panel. Shared cross-context DTOs contain stable IDs/timestamps only, not live game objects.

If CS2 binding-group constraints make three groups impractical, a single group may expose nested `runtime` and `assets` snapshots, but code must still keep the Runtime and Asset projection/builders in separate files/classes.

## 14. Settings and localization

Create one authoritative `Setting` for the merged product with logical groups:

- General/UI
- Runtime Monitoring
- Deep Capture
- Performance Advisor
- Asset Audit
- Export/Diagnostics

The Runtime Profiler defaults remain unchanged unless a conflict with an Asset setting requires an explicit merged value. Asset scan budget/page-size/finding-visibility settings are carried forward.

Legacy settings import from the two old mod IDs is **not required for the first integrated implementation** unless the game settings API provides a simple, testable read path. Do not add fragile reflection solely to migrate settings. README/release notes must state that the integrated mod uses a new settings identity if automatic migration is not implemented.

Localization is consolidated under the new product identity. Existing Japanese Runtime strings and English/Japanese Asset strings are retained. No mixed old product names should remain in visible UI except migration notes.

## 15. Unified export model

The primary JSON export is one report envelope:

```text
RuntimeAssetAuditReport
  SchemaVersion
  GeneratedAtUtc
  Product
  Session
  Runtime
  Advisor
  Assets
  EvidenceLinks
  Capabilities
  Diagnostics
  Privacy
```

Rules:

- `Runtime`, `Advisor`, or `Assets` may be absent/null when no corresponding snapshot exists; absence is not serialized as a zero-valued measurement.
- Existing Runtime measurement-quality labels remain intact.
- Existing Asset evidence status (observed/derived/estimated/unavailable semantics) remains intact.
- Existing privacy sanitization behavior is consolidated into one sanitizer and regression-tested against Windows/macOS/Linux home paths and account-name leakage.
- Asset flat CSV export remains available as a dedicated export because flattening the full unified report to CSV would destroy structure.
- Report schema versioning is independent from either legacy report schema; the initial unified schema begins at `1`.

Default JSON path:

```text
ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json
```

Default asset CSV path:

```text
ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-assets-YYYY-MM-DD_HHmmss_fff.csv
```

Collision handling retains the existing non-overwrite suffix behavior.

## 16. Capability and confidence semantics

Do not collapse distinct concepts merely to produce one enum.

- Runtime `Full / Managed / Indirect / Unavailable` describes measurement quality/attribution confidence.
- Asset `Supported / Degraded / Unsupported / Failed` describes collector/capability state.
- Asset observed/derived/estimated/unavailable evidence semantics remain explicit in asset records/findings.

The unified Diagnostics UI may present them together, but labels and tooltips must preserve their different meanings.

## 17. Performance and isolation requirements

1. Normal Runtime monitoring must not become materially heavier merely because Asset functionality is installed.
2. Asset full-world work remains manual and frame-budgeted.
3. Asset catalog/cache data may be reused within the current session when valid.
4. Long sessions must not create unbounded Runtime history, Asset snapshot history, or evidence-link growth.
5. Integration stores a bounded number of completed Asset snapshots. Initial implementation may retain the current/latest snapshot plus a small bounded history rather than every scan forever.
6. A failed Runtime collector does not disable Asset inspection.
7. A failed Asset collector does not disable Runtime monitoring/capture.
8. Exceptions crossing subsystem boundaries are converted to diagnostic state and logged; they do not crash the entire UI projection.

## 18. Build, dependency, and packaging policy

- Target framework remains `net48` to match the current mods/toolchain.
- Language version remains compatible with the current CS2 toolchain; current source baselines use C# 9.
- The unified project retains the CS2 managed references required by the union of both source projects.
- `Lib.Harmony` remains a private package/runtime dependency because Runtime Profiler currently depends on it.
- The build must verify that `0Harmony.dll` is copied when Harmony-dependent runtime code is enabled.
- One UI build is produced from `UI/` and deployed with the mod assembly.
- Pure tests must be runnable without requiring a local CS2 installation where the existing projects already support that separation.
- Adapter/integration tests that need game assemblies remain separately identifiable.

## 19. Migration strategy

Implementation proceeds incrementally so the new repository is never an unreviewable one-shot concatenation.

1. Seed from the pinned Runtime Profiler source and rename product identity while keeping Runtime behavior green.
2. Introduce shared session/operation coordination contracts.
3. Port Asset pure-core/domain code and pure tests without game integration.
4. Port Asset game adapters/systems and adapter tests.
5. Integrate settings/localization/lifecycle into the single `Mod` entry point.
6. Integrate backend UI bindings while keeping Runtime and Asset projection code separated.
7. Integrate the frontend into one launcher/panel/navigation tree.
8. Add unified export/privacy behavior.
9. Add evidence-link/cross-navigation behavior.
10. Run full automated verification, build verification, and explicit in-game validation checklist.

At every stage, tests should distinguish migration regressions from intentional product-identity changes.

## 20. Verification strategy

Automated verification must include:

- all migrated Runtime pure tests;
- all migrated Runtime adapter tests that are runnable in the environment;
- all migrated Asset pure tests;
- all migrated Asset adapter tests that are runnable in the environment;
- UI unit/regression tests for launcher uniqueness, navigation, scrolling, panel close/move/resize, asset pagination, and cross-navigation;
- export schema/privacy tests;
- coordinator tests proving Deep Capture priority and Asset scan queuing/interruption behavior;
- tests proving different sessions cannot be linked as same-session evidence;
- tests proving missing Asset data remains absent/unavailable rather than becoming zero;
- tests proving static asset evidence is not serialized/presented as measured per-asset runtime cost.

Manual in-game validation must cover at least:

1. clean game start with only the unified mod enabled;
2. one launcher, one panel, Esc/B close;
3. normal monitoring idle overhead sanity check;
4. manual Deep Capture;
5. automatic capture trigger when reproducible;
6. manual Asset catalog/census;
7. Asset scan requested during Deep Capture is queued;
8. Deep Capture starting during Asset scan interrupts Asset heavy work safely;
9. Runtime capture selection -> Asset navigation/context;
10. Asset detail -> Runtime capture context without per-asset cost fabrication;
11. Performance Advisor diagnose/apply/undo behavior;
12. JSON export and Asset CSV export;
13. privacy review of exported paths/usernames;
14. game reload and settings persistence under the new mod identity;
15. mod disable/uninstall does not leave duplicate legacy UI registrations.

## 21. Legacy repositories

During implementation:

- do not delete either legacy repository;
- do not force-push or rewrite their history;
- do not redirect their README yet;
- use the pinned commits as immutable migration references.

After the unified mod passes validation and the user explicitly decides to retire the old projects, a separate maintenance change may archive or redirect the old repositories. That action is outside this integration implementation plan.

## 22. Definition of done

The integration is done only when:

- the new repository builds as the unified mod;
- there is one `IMod` entry point and one launcher/panel;
- both prior feature sets are represented and automated regressions pass;
- heavy-work arbitration prevents Asset scanning from contaminating Deep Capture without disclosure;
- runtime and asset evidence can be linked by same-session IDs/timestamps while preserving uncertainty;
- unified JSON and Asset CSV exports work with privacy sanitization;
- documentation describes the new product rather than internal migration details;
- final verification evidence is recorded before any claim that implementation is complete.
