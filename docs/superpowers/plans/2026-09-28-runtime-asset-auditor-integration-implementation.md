# CS2 Runtime Asset Auditor Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task in ChatGPT Work. Do not start implementation until the user explicitly approves this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge CS2 Runtime Profiler and CS2 Asset Performance Auditor into one `CS2-Runtime-Asset-Auditor` Code Mod with one assembly, one launcher/panel, coordinated diagnostic work, shared session context, and unified export while preserving the evidence semantics and tested behavior of both source projects.

**Architecture:** Seed the new repository from the pinned Runtime Profiler baseline, rename it to the unified product identity, then port Asset Auditor as an isolated `Assets` subsystem. Add a small shared coordination layer for city-session identity and heavy-work arbitration. Compose Runtime and Asset backend bindings into one frontend shell and one report envelope without claiming static asset metadata is measured runtime cost.

**Tech Stack:** C# 9 / .NET Framework 4.8, Cities: Skylines II Modding Toolchain, Unity Entities/Profiler APIs, Colossal AssetDatabase/UI APIs, Lib.Harmony only for existing justified Runtime Profiler instrumentation, React 18, TypeScript, Webpack, Vitest, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-28-runtime-asset-auditor-integration-design.md`

## Global Constraints

- Target repository is `pengin0503/CS2-Runtime-Asset-Auditor`; implement on `main` unless the Work environment requires an isolated temporary worktree. Do not create a long-lived feature branch without a separate user request.
- Runtime source is pinned to `pengin0503/CS2-Runtime-Profiler@41836ded2c4cafd1eb5c7e8947ae75de737ea848`.
- Asset source is pinned to `pengin0503/CS2-Asset-Performance-Auditor@5f0650c980175d3e84451ff62227605da2447cd2`.
- Do not modify, archive, delete, force-push, or redirect either legacy repository during this implementation.
- Produce one primary assembly: `CS2RuntimeAssetAuditor.dll`, one `IMod` entry point, and one UI bundle.
- Product/mod/settings ID is `CS2RuntimeAssetAuditor`; display name is `CS2 Runtime Asset Auditor`.
- Target framework remains `net48`; code remains compatible with C# 9 and the current CS2 toolchain.
- Preserve existing Runtime Profiler behavior unless the integration explicitly changes product identity/navigation/export structure.
- Preserve existing Asset Auditor functionality and evidence semantics; Asset collectors remain ECS/public API/AssetDatabase based and do not gain Harmony dependencies merely because the unified assembly includes Harmony.
- Runtime `Full/Managed/Indirect/Unavailable`, Asset capability state, and Asset observed/derived/estimated/unavailable semantics remain distinct concepts.
- Never present static asset geometry/material/texture/census metadata as measured per-asset runtime GPU/frame cost.
- Deep Capture has priority over heavy Asset scans. Asset heavy work requested during Deep Capture is queued; Deep Capture beginning during Asset heavy work interrupts that work at a safe boundary.
- Never start an automatic full Asset census merely because Deep Capture started.
- Pure tests must remain runnable without a game installation where the source projects already support that separation.
- Keep user-facing README content focused on the product, not internal migration notes such as source phase history.

## Review Focus

1. **Deep Capture begins while a census/deep inspection is active:** Asset heavy work must stop at a safe boundary, publish an interrupted state rather than a partial completed snapshot, and allow a later restart. Covered in Task 3 and Task 6 tests.
2. **Asset scan is requested during Deep Capture:** the request must become visibly queued, not run concurrently and not disappear. Covered in Task 3 and Task 7 tests.
3. **Runtime capture and Asset snapshot belong to different city sessions:** they must not be linked as same-session evidence. Covered in Task 3 and Task 10 tests.
4. **Asset data is missing or a collector is unsupported/failed:** unified UI/export must preserve absence/availability semantics rather than emit zero values that look measured. Covered in Tasks 7, 9, and 10 tests.
5. **Rendering/GPU runtime pressure exists but no per-asset runtime marker exists:** Asset records may be shown only as investigation candidates with an explicit non-causal/static-evidence notice; no per-asset time may be fabricated. Covered in Tasks 8 and 10 tests.

---

## Target File Structure

The implementation should converge on this structure. Existing focused files from both projects may remain separate inside these directories; do not concatenate large files only to reduce file count.

```text
CS2RuntimeAssetAuditor.sln
src/CS2RuntimeAssetAuditor/
  CS2RuntimeAssetAuditor.csproj
  Mod.cs
  Setting.cs
  Coordination/
    DiagnosticSessionContext.cs
    DiagnosticWorkCoordinator.cs
    DiagnosticWorkContracts.cs
    DiagnosticEvidenceLink.cs
  Runtime/
    Advisor/
    Attribution/
    Collectors/
    Core/
    Export/
    Profiling/
    UI/
  Assets/
    Core/
    Export/
    GameIntegration/
    Localization/
    UI/
  Export/
    RuntimeAssetAuditReport.cs
    RuntimeAssetAuditReportBuilder.cs
    RuntimeAssetAuditReportSerializer.cs
    PrivacySanitizer.cs
  Localization/
UI/
  src/
    index.tsx
    shell/
      RuntimeAssetAuditorRoot.tsx
      navigation.ts
    runtime/
    assets/
    shared/
tests/
  CS2RuntimeAssetAuditor.Tests/
  CS2RuntimeAssetAuditor.AdapterTests/
```

`Runtime/` is primarily a namespace/directory relocation of the Runtime Profiler baseline. `Assets/` is a namespace/directory relocation of Asset Auditor. Shared coordination/export code must stay outside either feature subtree.

---

### Task 1: Seed the unified repository from the pinned Runtime Profiler baseline

**Files:**
- Import from source commit: all tracked files from `pengin0503/CS2-Runtime-Profiler@41836ded2c4cafd1eb5c7e8947ae75de737ea848`
- Preserve: `docs/superpowers/specs/2026-09-28-runtime-asset-auditor-integration-design.md`
- Preserve: `docs/superpowers/plans/2026-09-28-runtime-asset-auditor-integration-implementation.md`
- Create: `docs/migration/source-baselines.md`

**Interfaces:**
- Consumes: pinned Runtime Profiler repository state.
- Produces: a green Runtime Profiler baseline inside the new repository before behavior-changing integration work begins.

- [ ] **Step 1: Verify source SHAs before importing**

Run:

```bash
git ls-remote https://github.com/pengin0503/CS2-Runtime-Profiler.git refs/heads/main
git ls-remote https://github.com/pengin0503/CS2-Asset-Performance-Auditor.git refs/heads/main
```

Expected at planning baseline: Runtime resolves to `41836ded2c4cafd1eb5c7e8947ae75de737ea848`; Asset resolves to `5f0650c980175d3e84451ff62227605da2447cd2`. If either main has moved, still import the pinned SHA unless the user explicitly asks to rebase the integration plan onto newer source.

- [ ] **Step 2: Import the pinned Runtime tree without overwriting the integration spec/plan**

Use a temporary source remote/fetch, then checkout the pinned Runtime tree into the new repository working tree. Preserve the two integration documents if Git reports path conflicts.

One valid sequence is:

```bash
git remote add runtime-source https://github.com/pengin0503/CS2-Runtime-Profiler.git
git fetch runtime-source 41836ded2c4cafd1eb5c7e8947ae75de737ea848
git checkout 41836ded2c4cafd1eb5c7e8947ae75de737ea848 -- .
```

Then restore the integration spec/plan from current `HEAD` if necessary.

- [ ] **Step 3: Record source provenance**

Create `docs/migration/source-baselines.md` containing the two pinned repository names/SHAs, the import date `2026-09-28`, and the rule that the legacy repositories remain unchanged during integration.

- [ ] **Step 4: Run the imported Runtime pure test suite before renaming anything**

Run:

```bash
dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -c Release
```

Expected: PASS. If the local environment cannot resolve a dependency that the original CI resolves, record the exact environment failure before changing source code; do not “fix” unrelated Runtime behavior in this task.

- [ ] **Step 5: Run Runtime UI tests**

Run:

```bash
cd UI
npm ci
npm test
npm run build
cd ..
```

Expected: Vitest PASS and Webpack build succeeds.

- [ ] **Step 6: Commit the imported baseline**

```bash
git add .
git commit -m "chore: seed unified repo from runtime profiler"
```

---

### Task 2: Rename the product and Runtime code to the unified identity

**Files:**
- Rename: `CS2RuntimeProfiler.sln` -> `CS2RuntimeAssetAuditor.sln`
- Rename directory: `src/CS2RuntimeProfiler/` -> `src/CS2RuntimeAssetAuditor/`
- Rename project: `src/CS2RuntimeAssetAuditor/CS2RuntimeProfiler.csproj` -> `src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj`
- Rename test directories/projects: `tests/CS2RuntimeProfiler.Tests/` -> `tests/CS2RuntimeAssetAuditor.Tests/`; `tests/CS2RuntimeProfiler.AdapterTests/` -> `tests/CS2RuntimeAssetAuditor.AdapterTests/`
- Modify: namespaces/usings under `src/CS2RuntimeAssetAuditor/**` and migrated tests
- Modify: `src/CS2RuntimeAssetAuditor/Mod.cs`
- Modify: `UI/mod.json`, `UI/package.json`, `UI/src/index.tsx`, Runtime UI bindings/tests
- Modify: report/export path constants in Runtime export code
- Test: `tests/CS2RuntimeAssetAuditor.Tests/ProductIdentityTests.cs`
- Test: `UI/src/productIdentity.test.ts`

**Interfaces:**
- Consumes: green Runtime baseline from Task 1.
- Produces: `CS2RuntimeAssetAuditor` assembly/root namespace/mod ID while retaining Runtime functionality.

- [ ] **Step 1: Write failing C# product identity tests**

Create `ProductIdentityTests.cs` with assertions that:

```text
Mod.Id == "CS2RuntimeAssetAuditor"
assembly name == "CS2RuntimeAssetAuditor"
default report root contains "ModsData/CS2RuntimeAssetAuditor"
```

Also assert that user-facing product metadata does not expose `CS2 Runtime Profiler` as the current product name.

- [ ] **Step 2: Run the product identity test and verify RED**

Run:

```bash
dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -c Release --filter ProductIdentityTests
```

Expected: FAIL because the baseline still uses the old identity.

- [ ] **Step 3: Rename solution/project/test paths and root namespace**

Apply the exact target names listed in **Files**. In `CS2RuntimeAssetAuditor.csproj`, set:

```xml
<RootNamespace>CS2RuntimeAssetAuditor</RootNamespace>
<AssemblyName>CS2RuntimeAssetAuditor</AssemblyName>
```

Retain `net48`, C# 9, existing CS2 references, `Lib.Harmony`, and the `0Harmony.dll` verification target.

- [ ] **Step 4: Update `Mod` identity and Runtime namespace references**

`Mod.cs` must expose:

```csharp
public const string Id = "CS2RuntimeAssetAuditor";
```

Update Runtime namespaces from `CS2RuntimeProfiler.*` to `CS2RuntimeAssetAuditor.Runtime.*` or the nearest focused target namespace. Do not mix product rename with unrelated behavioral refactors.

- [ ] **Step 5: Write failing UI identity test before changing frontend identity**

`UI/src/productIdentity.test.ts` must assert the module metadata/visible shell uses `CS2 Runtime Asset Auditor` and only one `GameTopLeft` registration exists.

Run:

```bash
cd UI && npm test -- productIdentity.test.ts
```

Expected: FAIL on old Runtime Profiler identity.

- [ ] **Step 6: Update frontend/package/mod identity**

Set npm package name to `cs2-runtime-asset-auditor-ui`, update `UI/mod.json`, launcher aria/title text, and binding group constants to the unified identity while retaining the existing one-launcher Runtime panel behavior.

- [ ] **Step 7: Run renamed Runtime tests and UI tests**

Run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
cd UI && npm test && npm run build && cd ..
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add .
git commit -m "refactor: rename runtime profiler to runtime asset auditor"
```

---

### Task 3: Add shared diagnostic session identity and heavy-work arbitration

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticSessionContext.cs`
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticWorkContracts.cs`
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticWorkCoordinator.cs`
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticEvidenceLink.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/DiagnosticSessionContextTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/DiagnosticWorkCoordinatorTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/DiagnosticEvidenceLinkTests.cs`

**Interfaces:**
- Consumes: none beyond BCL types.
- Produces:
  - `DiagnosticSessionContext.Create(string gameVersion, string buildIdentity, DateTimeOffset startedAtUtc)`
  - `DiagnosticWorkCoordinator.Request(DiagnosticWorkKind kind) -> DiagnosticWorkDecision`
  - `DiagnosticWorkCoordinator.Complete(DiagnosticWorkKind kind)`
  - `DiagnosticWorkCoordinator.ConsumeAssetInterruptionRequest() -> bool`
  - `DiagnosticWorkCoordinator.HasQueuedAssetWork`
  - `DiagnosticEvidenceLink.TryCreate(...)`

- [ ] **Step 1: Write RED tests for session identity**

Tests must assert:

- each `Create(...)` call creates a non-empty session ID;
- separate session creation produces different IDs;
- supplied start time/game version/build identity are retained;
- session ID is plain serializable data and has no Unity/CS2 type dependency.

- [ ] **Step 2: Implement `DiagnosticSessionContext`**

Signature:

```csharp
public sealed class DiagnosticSessionContext
{
    public string SessionId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public string GameVersion { get; }
    public string BuildIdentity { get; }
    public static DiagnosticSessionContext Create(string gameVersion, string buildIdentity, DateTimeOffset startedAtUtc);
}
```

Use a GUID string for `SessionId`.

- [ ] **Step 3: Write RED tests for work priority/queueing**

Cover these exact transitions:

```text
Idle + AssetHeavyScan request -> Started
Idle + RuntimeDeepCapture request -> Started
RuntimeDeepCapture active + AssetHeavyScan request -> Queued; HasQueuedAssetWork=true
AssetHeavyScan active + RuntimeDeepCapture request -> Started; ConsumeAssetInterruptionRequest() returns true once
RuntimeDeepCapture complete + queued AssetHeavyScan -> next Asset request can start
repeated identical active request -> AlreadyActive
```

- [ ] **Step 4: Implement the coordinator contract**

Use:

```csharp
public enum DiagnosticWorkKind { RuntimeDeepCapture, AssetHeavyScan }
public enum DiagnosticWorkDecision { Started, Queued, AlreadyActive }

public sealed class DiagnosticWorkCoordinator
{
    public DiagnosticWorkDecision Request(DiagnosticWorkKind kind);
    public void Complete(DiagnosticWorkKind kind);
    public bool ConsumeAssetInterruptionRequest();
    public bool HasQueuedAssetWork { get; }
    public bool IsActive(DiagnosticWorkKind kind);
}
```

Do not add transparent scan-resume logic here.

- [ ] **Step 5: Write RED tests for evidence-link session safety**

`DiagnosticEvidenceLink.TryCreate(...)` must reject/return no link for different session IDs and classify same-session timing only from timestamps as `Before`, `Overlapping`, or `After`.

- [ ] **Step 6: Implement evidence-link DTO/factory**

The DTO stores IDs, timestamps, and relative timing only. It contains no “cause”, “responsible”, score, or per-asset runtime cost property.

- [ ] **Step 7: Run pure tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter "DiagnosticSessionContextTests|DiagnosticWorkCoordinatorTests|DiagnosticEvidenceLinkTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Coordination tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: add shared diagnostic coordination"
```

---

### Task 4: Port the Asset Auditor pure domain/core and preserve its tests

**Files:**
- Port source: `CS2-Asset-Performance-Auditor/src/CS2AssetPerformanceAuditor/Core/**` -> `src/CS2RuntimeAssetAuditor/Assets/Core/**`
- Port relevant pure tests from `CS2-Asset-Performance-Auditor/tests/CS2AssetPerformanceAuditor.Tests/**` into `tests/CS2RuntimeAssetAuditor.Tests/Assets/**`
- Modify: `tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj`

**Interfaces:**
- Consumes: no game runtime types beyond what the original pure core already avoided.
- Produces: Prefab/census/render observation/finding/query/scanning/capability domain types under `CS2RuntimeAssetAuditor.Assets.Core.*`.

- [ ] **Step 1: Import the pinned Asset source commit into a temporary remote namespace/work area**

```bash
git remote add asset-source https://github.com/pengin0503/CS2-Asset-Performance-Auditor.git
git fetch asset-source 5f0650c980175d3e84451ff62227605da2447cd2
```

Do not merge histories or add the Asset repository as a runtime dependency.

- [ ] **Step 2: Port Asset pure tests first and verify RED due to missing namespaces/types**

At minimum port the pure tests covering:

- Prefab/catalog identity and snapshot behavior;
- census reducer;
- geometry math/topology;
- render observations;
- findings/peer comparison;
- query/filter/sort/paging;
- scan coordinator/state;
- deep-inspection snapshot/core behavior;
- bug regressions that do not require game assemblies.

Run the unified pure test project and confirm failures are missing Asset types/namespaces rather than unrelated Runtime regressions.

- [ ] **Step 3: Port `Core/**` with namespace-only adaptation**

Map:

```text
CS2AssetPerformanceAuditor.Core.*
-> CS2RuntimeAssetAuditor.Assets.Core.*
```

Do not rewrite algorithms that already pass their source tests. Keep files focused and preserve observed/derived/estimated/unavailable semantics.

- [ ] **Step 4: Resolve only integration-level type/name conflicts**

Examples include duplicate `ProjectInfo`, shared enum names, or serializer helpers. Prefer namespacing over semantic merging when two types represent different concepts.

- [ ] **Step 5: Run all unified pure tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: migrated Runtime tests and Asset pure tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/Core tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: port asset auditor core"
```

---

### Task 5: Port Asset game integration and adapter coverage

**Files:**
- Port: `CS2-Asset-Performance-Auditor/src/CS2AssetPerformanceAuditor/GameIntegration/**` -> `src/CS2RuntimeAssetAuditor/Assets/GameIntegration/**`
- Port: game-facing Asset adapter tests -> `tests/CS2RuntimeAssetAuditor.AdapterTests/Assets/**`
- Modify: `src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj` with the union of required managed references
- Modify: `tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj`

**Interfaces:**
- Consumes: Asset core records from Task 4.
- Produces: `AssetAuditSystem` and existing Prefab/Census/Rendering/Capability adapters under the unified assembly.

- [ ] **Step 1: Port adapter tests first**

Bring over the Asset adapter tests that validate current game-facing assumptions. Update namespaces/project references only.

- [ ] **Step 2: Run adapter tests and verify RED because adapters are not present**

When the CS2 managed path is available:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

Expected initially: compile/test failure for missing Asset integration classes. If no CS2 managed path exists, still compile the pure project and record adapter tests as environment-blocked rather than inventing results.

- [ ] **Step 3: Port `GameIntegration/**` with namespace adaptation**

Map all original GameIntegration source into `CS2RuntimeAssetAuditor.Assets.GameIntegration.*`, including:

- `AssetAuditSystem.cs`;
- `AssetAuditDeepInspectionExtensions.cs`;
- `AssetAuditGeometryExtensions.cs`;
- `AssetAuditSurfaceTextureExtensions.cs`;
- `Capabilities/**`;
- `Census/**`;
- `Prefabs/**`;
- `Rendering/**`.

Keep existing frame budgeting, cancellation boundaries, atomic snapshot publication, and graceful capability failure behavior.

- [ ] **Step 4: Merge project references conservatively**

Add only managed references required by either existing source project. Keep `<Private>false</Private>` for game-managed assemblies. Preserve the Runtime Harmony package and runtime-copy verification.

- [ ] **Step 5: Run pure + adapter verification**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

Expected: PASS when the adapter environment is available.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/GameIntegration src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj tests/CS2RuntimeAssetAuditor.AdapterTests
git commit -m "feat: port asset game integration"
```

---

### Task 6: Unify lifecycle, settings, localization, and coordinator wiring

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Mod.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Setting.cs`
- Port/merge from source: `AuditorSetting.cs` properties into unified `Setting.cs`
- Port: Asset localization resources into `src/CS2RuntimeAssetAuditor/Localization/` or `Assets/Localization/` as appropriate
- Modify: Runtime localization strings to unified product name
- Modify: `src/CS2RuntimeAssetAuditor/Runtime/Profiling/CaptureRuntimeSystem.cs` or the exact current Deep Capture lifecycle owner
- Modify: `src/CS2RuntimeAssetAuditor/Assets/GameIntegration/AssetAuditSystem.cs`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/UnifiedLifecyclePolicyTests.cs`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/UnifiedSettingsTests.cs`

**Interfaces:**
- Consumes: `DiagnosticWorkCoordinator`, Runtime capture lifecycle, Asset scan lifecycle.
- Produces: one `IMod` entry point and one settings object registering Runtime + Asset systems.

- [ ] **Step 1: Write RED lifecycle tests**

Tests must assert source/text/domain policy that the unified product has exactly one `IMod` implementation and that registration includes:

```text
GlobalMetricsCollector
DomainMetricsSystem
CaptureRuntimeSystem
AdvisorSystem
AssetAuditSystem
Runtime/Profiler UI system
Asset Audit UI system (after Task 7, or placeholder registration added then)
```

Do not keep the old Asset `Mod` class.

- [ ] **Step 2: Write RED settings tests**

Assert one settings ID `CS2RuntimeAssetAuditor` exposes existing Runtime configuration plus migrated Asset scan budget/page size/finding visibility/UI options, grouped without duplicate conflicting properties.

- [ ] **Step 3: Merge Asset settings into `Setting`**

Use one authoritative persistent settings object. Preserve Runtime default values and Asset default values unless exact property collisions require a documented mapping.

Do not add reflection-based legacy settings migration in this task.

- [ ] **Step 4: Register Asset systems from the unified `Mod`**

`Mod.OnLoad` becomes the only lifecycle entry point. Register `AssetAuditSystem` at its required phase and keep Runtime systems at their existing phases.

- [ ] **Step 5: Wire Deep Capture priority into the Runtime capture owner**

Before Deep Capture begins, call:

```csharp
coordinator.Request(DiagnosticWorkKind.RuntimeDeepCapture)
```

If Asset heavy work is active, the coordinator must request Asset interruption; Runtime capture proceeds.

On capture completion/abort, call:

```csharp
coordinator.Complete(DiagnosticWorkKind.RuntimeDeepCapture)
```

- [ ] **Step 6: Wire Asset heavy scan lifecycle into `AssetAuditSystem`**

Before starting a heavy catalog/census/deep-inspection operation:

```csharp
var decision = coordinator.Request(DiagnosticWorkKind.AssetHeavyScan);
```

Behavior:

```text
Started -> begin scan
Queued -> publish WaitingForRuntimeCapture; do not enumerate the world
AlreadyActive -> keep current operation; do not duplicate
```

During frame-budgeted work, consume the interruption request. If true, cancel at the next safe boundary and publish `InterruptedByRuntimeCapture`; do not publish partial data as a completed snapshot.

Call `Complete(AssetHeavyScan)` on normal completion/cancel/interruption.

- [ ] **Step 7: Consolidate localization**

Provide `ja-JP` and `en-US` under the new product identity. Replace visible old product names in active UI/settings strings. Keep legacy names only in migration documentation.

- [ ] **Step 8: Run lifecycle/settings/coordinator tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter "UnifiedLifecyclePolicyTests|UnifiedSettingsTests|DiagnosticWorkCoordinatorTests"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/CS2RuntimeAssetAuditor tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: unify mod lifecycle and diagnostic scheduling"
```

---

### Task 7: Port Asset backend UI contracts without creating a monolithic Runtime UI system

**Files:**
- Port: `CS2-Asset-Performance-Auditor/src/CS2AssetPerformanceAuditor/UI/**` -> `src/CS2RuntimeAssetAuditor/Assets/UI/**`
- Modify: `src/CS2RuntimeAssetAuditor/Assets/UI/AssetAuditUISystem.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Runtime/UI/ProfilerUISystem.cs` only for shared session/shell integration points
- Create: `src/CS2RuntimeAssetAuditor/Assets/UI/AssetAuditBindingNames.cs`
- Port/adapt tests: Asset UI projection/publish policy tests into `tests/CS2RuntimeAssetAuditor.Tests/Assets/UI/**`
- Create: `tests/CS2RuntimeAssetAuditor.Tests/UnifiedUiBindingPolicyTests.cs`

**Interfaces:**
- Consumes: Asset snapshots, unified Setting, DiagnosticSessionContext, DiagnosticWorkCoordinator.
- Produces: separate Asset binding surface under one mod identity, plus shell/runtime binding surface used by the integrated React panel.

- [ ] **Step 1: Port Asset UI projection tests and add RED binding-policy tests**

Assert:

- Asset UI has no independent panel visibility state or top-left launcher concept;
- Asset binding group is namespaced under the unified Mod ID, e.g. `CS2RuntimeAssetAuditor.assets`;
- queued scan state is projectable to the frontend;
- unsupported/unavailable values remain explicit;
- Asset UI commands do not scan entities directly; they delegate to `AssetAuditSystem`.

- [ ] **Step 2: Port Asset UI contracts/projection classes**

Map the source files including:

```text
AssetAuditUISystem.cs
AssetAuditSettingsSyncSystem.cs
UiAnalysisProjection.cs
UiContracts.cs
UiExportContracts.cs
UiPublishPolicy.cs
UiSnapshotBuilder.cs
```

into `Assets/UI/`, changing namespaces and binding names only where possible.

- [ ] **Step 3: Remove standalone Asset open/close ownership from backend contracts**

Asset bindings expose data/actions for the unified shell; panel visibility/layout remains owned by the unified shell/Runtime side.

- [ ] **Step 4: Add shared session/coordinator status to backend projections**

Expose stable serializable fields needed by the frontend:

```text
SessionId
AssetScanState
QueuedBecauseRuntimeCapture
InterruptedByRuntimeCapture
LatestAssetSnapshotId
LatestAssetSnapshotStartedAtUtc
LatestAssetSnapshotCompletedAtUtc
```

Do not pass live Unity/Entity objects.

- [ ] **Step 5: Run pure UI projection tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter "Ui|Binding|Asset"
```

Expected: PASS for relevant test classes.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/UI src/CS2RuntimeAssetAuditor/Runtime/UI tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: integrate asset backend UI bindings"
```

---

### Task 8: Build one frontend shell and migrate Asset UI into it

**Files:**
- Modify: `UI/package.json`, `UI/package-lock.json`
- Modify: `UI/src/index.tsx`
- Rename/refactor: existing Runtime `UI/src/profiler/**` -> `UI/src/runtime/**` as practical; do not do a rename only if it creates unnecessary risk, but visible product naming must be unified
- Create: `UI/src/shell/RuntimeAssetAuditorRoot.tsx`
- Create: `UI/src/shell/navigation.ts`
- Port: Asset `components/**`, `details/**`, `tabs/**`, `types.ts`, and bindings into `UI/src/assets/**`
- Merge styles into focused Runtime/Asset/shell modules; do not inject a second global launcher stylesheet
- Create: `UI/src/shell/unifiedNavigation.test.tsx`
- Create: `UI/src/shell/launcherRegression.test.tsx`
- Port/adapt Asset UI tests under `UI/src/assets/**`

**Interfaces:**
- Consumes: Runtime bindings, Asset bindings, unified panel visibility/layout.
- Produces: one `ModRegistrar`, one `GameTopLeft` launcher, one `Game` panel root, and the top-level navigation defined in the spec.

- [ ] **Step 1: Normalize frontend dependency versions before porting components**

Keep one `package.json` and one lockfile. Use the Runtime baseline dependency/tooling set as the base and add only dependencies required by Asset tests/components (for example `jsdom` if tests require it). Do not retain two React, TypeScript, Webpack, or Vitest installations/configurations.

- [ ] **Step 2: Write RED tests for unified registration/navigation**

Tests assert:

```text
exactly one moduleRegistry.append("GameTopLeft", ...)
exactly one panel root registration
five top-level sections: Overview, Runtime, Assets, Advisor, Diagnostics
Runtime section exposes Systems/Mods/Pathfinding/Timeline/Captures
Assets section exposes Catalog(or Assets)/Census/Findings/Compare
```

Also assert Esc/B behavior still uses `InputActionConsumer` rather than relying on DOM keydown.

- [ ] **Step 3: Create the unified shell using the current Runtime panel mechanics**

`RuntimeAssetAuditorRoot.tsx` owns:

- visible/close behavior;
- drag/resize/persisted layout;
- top-level navigation;
- CS2 `Scrollable` panel body;
- composition of Runtime, Asset, Advisor, and Diagnostics views.

Do not duplicate these mechanics inside Asset components.

- [ ] **Step 4: Port Asset UI components as content views, not as a second application root**

Move/adapt Asset tabs/components/details/types/bindings under `UI/src/assets/`. Remove the old Asset launcher/open-state/header shell from `AssetAuditorRoot.tsx`; either delete that root after migration or reduce it to an internal Asset section component with no panel/launcher ownership.

- [ ] **Step 5: Preserve Asset list scalability and settings behavior**

Keep search debounce/paging/query behavior and page-size setting. Asset lists must not render the full catalog without pagination/virtualization safeguards.

- [ ] **Step 6: Add queued/interrupted scan UI states**

Display:

```text
Waiting for Runtime capture to finish
Interrupted by Runtime capture — restart scan
```

or localized equivalents. Do not silently spin or report completion.

- [ ] **Step 7: Add non-causal evidence notice in Asset investigation context**

When Runtime rendering/GPU context is shown next to Asset candidates, render a persistent explanation equivalent to:

```text
Asset geometry, texture, and instance exposure are investigation evidence, not measured per-asset frame/GPU cost.
```

Test that no UI field/label named `perAssetGpuMs`, `assetFrameTime`, or equivalent fabricated runtime cost is introduced.

- [ ] **Step 8: Run UI tests/build**

```bash
cd UI
npm ci
npm test
npm run build
cd ..
```

Expected: PASS/build succeeds.

- [ ] **Step 9: Commit**

```bash
git add UI
git commit -m "feat: unify runtime and asset UI"
```

---

### Task 9: Replace separate JSON reports with one unified report envelope and privacy pipeline

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReport.cs`
- Create: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReportBuilder.cs`
- Create: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReportSerializer.cs`
- Consolidate: `src/CS2RuntimeAssetAuditor/Export/PrivacySanitizer.cs`
- Retain/adapt Runtime report DTO/builders under `Runtime/Export/` as section builders
- Port/adapt Asset export DTO/builders under `Assets/Export/` as Asset section/CSV builders
- Retain: Asset CSV summary export as a dedicated export
- Test: `tests/CS2RuntimeAssetAuditor.Tests/UnifiedExportTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/UnifiedPrivacySanitizerTests.cs`

**Interfaces:**
- Consumes: latest/selected Runtime capture, Advisor state, Asset snapshot, evidence links, capability/diagnostic state.
- Produces:
  - `RuntimeAssetAuditReportBuilder.Build(...) -> RuntimeAssetAuditReport`
  - JSON serializer/file writer rooted at `ModsData/CS2RuntimeAssetAuditor/`
  - Asset CSV writer rooted at the same product directory.

- [ ] **Step 1: Write RED unified export schema tests**

Assert top-level fields:

```text
SchemaVersion == 1
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

Assert `Runtime`, `Advisor`, or `Assets` can be absent/null without synthetic zero-valued measurements.

- [ ] **Step 2: Write RED privacy tests**

Cover Windows, macOS, and Linux home paths plus current-account-name replacement. Include both Runtime-originated strings and Asset-originated strings in the same report object.

- [ ] **Step 3: Implement the unified envelope and section-builder composition**

Do not flatten Runtime and Asset internal DTOs into one giant record. Adapt existing builders to produce sections consumed by `RuntimeAssetAuditReportBuilder`.

- [ ] **Step 4: Consolidate privacy sanitization**

Choose the stricter behavior from the two existing sanitizers and preserve all tested replacements. Run sanitizer over the final serialized report path/content boundary as defense in depth.

- [ ] **Step 5: Preserve non-overwrite file naming**

JSON:

```text
ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json
```

Asset CSV:

```text
ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-assets-YYYY-MM-DD_HHmmss_fff.csv
```

Retain `-1`, `-2`, ... suffix behavior on collisions.

- [ ] **Step 6: Update UI export actions/status to target unified JSON + Asset CSV**

Runtime Overview/Diagnostics and Asset views should no longer expose two competing full JSON report concepts.

- [ ] **Step 7: Run export/privacy tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter "UnifiedExportTests|UnifiedPrivacySanitizerTests|Export"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Export src/CS2RuntimeAssetAuditor/Runtime/Export src/CS2RuntimeAssetAuditor/Assets/Export tests/CS2RuntimeAssetAuditor.Tests UI
git commit -m "feat: add unified diagnostic export"
```

---

### Task 10: Add conservative Runtime↔Asset evidence linking and cross-navigation

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticEvidenceBridge.cs`
- Modify: Runtime capture DTO/store to stamp `SessionId`, `StartedAtUtc`, `CompletedAtUtc`
- Modify: Asset completed snapshot DTO/store to stamp `SessionId`, `AssetSnapshotId`, `StartedAtUtc`, `CompletedAtUtc`
- Modify: unified overview/backend projections
- Modify: `UI/src/shell/RuntimeAssetAuditorRoot.tsx`
- Create/modify: `UI/src/assets/RuntimeContextCard.tsx`
- Create/modify: Runtime capture action to navigate to Assets with selected capture context
- Test: `tests/CS2RuntimeAssetAuditor.Tests/DiagnosticEvidenceBridgeTests.cs`
- Test: `UI/src/shell/crossNavigation.test.tsx`

**Interfaces:**
- Consumes: Runtime capture summaries, Asset snapshot summaries, DiagnosticSessionContext.
- Produces:
  - same-session temporal evidence links;
  - combined Overview summary;
  - selected Runtime capture context in the Asset section;
  - optional `Run Asset Audit` diagnostic action when no same-session snapshot exists.

- [ ] **Step 1: Write RED bridge tests**

Cover:

```text
same session + Asset before Runtime -> link RelativeTiming.Before
same session + overlapping windows -> Overlapping
same session + Asset after Runtime -> After
different sessions -> no link
missing timestamps -> no fabricated relative timing
```

- [ ] **Step 2: Stamp Runtime captures with session/timing metadata**

Reuse existing capture start/end timestamps when available. Do not derive a persistent city identity from save names/paths.

- [ ] **Step 3: Stamp Asset snapshots with session/timing metadata**

A completed Asset snapshot gets an in-session ID only after successful atomic publication. Interrupted/failed partial work must not masquerade as a completed snapshot.

- [ ] **Step 4: Implement `DiagnosticEvidenceBridge`**

The bridge returns descriptive links and overview context only. It must not calculate causality scores or per-asset runtime cost.

- [ ] **Step 5: Write RED frontend cross-navigation tests**

Assert selecting a Runtime capture and choosing Asset investigation:

- switches to Assets;
- preserves selected capture ID/context;
- shows the same-session Asset snapshot/link when available;
- otherwise exposes `Run Asset Audit`;
- shows the non-causal/static-evidence notice for rendering/GPU investigation context.

- [ ] **Step 6: Implement cross-navigation/context cards**

For CPU/simulation/pathfinding-dominant captures, do not automatically promote geometry/texture findings as explanation. Existing Asset findings remain available normally.

- [ ] **Step 7: Run C# + UI tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release --filter "DiagnosticEvidence"
cd UI && npm test -- crossNavigation.test.tsx && cd ..
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/CS2RuntimeAssetAuditor UI tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: link runtime captures with asset evidence"
```

---

### Task 11: Consolidate CI, documentation, packaging, and final verification

**Files:**
- Modify/create: `.github/workflows/pure-tests.yml`
- Modify/create: `.github/workflows/ui-tests.yml`
- Modify build workflow(s) if present
- Rewrite: `README.md` for `CS2 Runtime Asset Auditor`
- Preserve/update: `LICENSE` (MIT)
- Create: `docs/validation/2026-09-28-integration-validation.md`
- Remove obsolete duplicate project identity files only after all tests are green

**Interfaces:**
- Consumes: fully integrated product from Tasks 1–10.
- Produces: reproducible verification evidence and user-facing documentation for the unified repository.

- [ ] **Step 1: Write/adjust CI expectations before deleting old project paths**

Pure CI must run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

UI CI must run:

```bash
cd UI
npm ci
npm test
npm run build
```

Adapter/build CI may remain environment-specific if CS2 managed assemblies/toolchain are not legally/distributably available to GitHub Actions; document that limitation rather than weakening pure/UI checks.

- [ ] **Step 2: Rewrite README as user-facing product documentation**

Cover:

- what Runtime + Asset diagnosis does;
- one launcher/panel;
- Runtime/Assets/Advisor/Diagnostics workflows;
- evidence/confidence caveats;
- heavy-scan vs Deep Capture coordination;
- installation/build requirements;
- unified JSON and Asset CSV locations;
- privacy warning before sharing reports;
- current game-version baseline;
- new settings identity and any legacy-setting reset limitation.

Do not include internal statements such as “Phase 1–4 use no Harmony” or migration-only implementation notes unless a user needs them to operate the mod.

- [ ] **Step 3: Search for stale user-facing product names and duplicate entry points**

Run searches equivalent to:

```bash
git grep -n "CS2 Runtime Profiler"
git grep -n "CS2 Asset Performance Auditor"
git grep -n "CS2RuntimeProfiler"
git grep -n "CS2AssetPerformanceAuditor"
git grep -n "IMod"
```

Expected: legacy names remain only in migration/source-history docs or compatibility tests where intentional; exactly one active `IMod` implementation exists.

- [ ] **Step 4: Run all pure tests**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Run all UI tests and production build**

```bash
cd UI
npm ci
npm test
npm run build
cd ..
```

Expected: PASS/build succeeds.

- [ ] **Step 6: Run adapter tests when game assemblies are available**

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

Expected: PASS. If blocked by unavailable local game assemblies, record the exact prerequisite and do not claim adapter verification succeeded.

- [ ] **Step 7: Build the unified mod when the official toolchain is available**

```bash
dotnet build src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj -c Release
```

Expected:

- build succeeds;
- `CS2RuntimeAssetAuditor.dll` exists;
- `0Harmony.dll` exists when required by Runtime instrumentation;
- one UI bundle is deployed/produced;
- no second Asset Auditor or Runtime Profiler assembly is produced as an installable mod entry point.

- [ ] **Step 8: Record in-game validation checklist/status**

In `docs/validation/2026-09-28-integration-validation.md`, record PASS/FAIL/NOT RUN for the 15 manual scenarios in the design spec. Never convert NOT RUN into PASS merely because automated tests succeeded.

- [ ] **Step 9: Verify git status and commit final integration documentation/CI**

```bash
git status --short
git add .github README.md LICENSE docs/validation
git commit -m "docs: finalize unified auditor verification"
```

- [ ] **Step 10: Run verification-before-completion before reporting success**

Required final evidence:

```text
pure tests: PASS
UI tests: PASS
UI production build: PASS
adapter tests: PASS or explicitly environment-blocked
mod Release build: PASS or explicitly environment-blocked
in-game validation: individual PASS/FAIL/NOT RUN recorded
working tree: clean
```

Do not archive or edit the two legacy repositories in this task.

---

## Plan Self-Review Results

### Spec coverage

- Single mod/assembly/launcher/panel: Tasks 2, 6, 8, 11.
- Runtime feature preservation: Tasks 1–2 plus regression verification throughout.
- Asset feature preservation: Tasks 4–5, 7–9.
- Shared city-session identity: Tasks 3, 10.
- Deep Capture vs Asset heavy-work arbitration: Tasks 3, 6–8.
- Conservative evidence linking/no false causality: Tasks 3, 8, 10.
- Unified settings/localization: Task 6.
- Unified JSON + Asset CSV/privacy: Task 9.
- Test separation/build/CI/manual validation: Task 11.
- Legacy repositories unchanged: Global Constraints and Task 11.

### Type/interface consistency

The plan uses one shared coordination API throughout:

```csharp
DiagnosticWorkCoordinator.Request(DiagnosticWorkKind)
DiagnosticWorkCoordinator.Complete(DiagnosticWorkKind)
DiagnosticWorkCoordinator.ConsumeAssetInterruptionRequest()
DiagnosticSessionContext.SessionId
DiagnosticEvidenceLink.TryCreate(...)
```

Later tasks depend on these exact names rather than redefining equivalent coordination types.

### Scope decision

The work is large but still one coherent integration project: every task contributes to replacing two independently installed diagnostic mods with one product. New runtime-render interception, per-asset GPU timing, automatic optimization, legacy-repository retirement, and unrelated refactors remain outside this plan.

## Execution Handoff

Implementation is intentionally **not started by this planning session**. After the user reviews and approves this plan, continue in **ChatGPT Work** and use `superpowers:executing-plans` to execute Tasks 1–11 in order, with each task ending in verification and a commit. Do not reinterpret the old repositories' current `main` branches as the source baseline unless the user explicitly requests updating the pinned SHAs.
