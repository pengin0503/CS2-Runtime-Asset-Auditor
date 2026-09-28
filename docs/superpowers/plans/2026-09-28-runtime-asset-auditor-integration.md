# CS2 Runtime Asset Auditor Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` in ChatGPT Work to implement this plan task-by-task. Do not use task-per-subagent execution unless the user explicitly changes the execution method. Every task ends with verification and a commit.

**Goal:** Merge the pinned CS2 Runtime Profiler and CS2 Asset Performance Auditor baselines into one deployable `CS2 Runtime Asset Auditor` Code Mod with one lifecycle, one UI shell, coordinated diagnostic workloads, unified evidence context, and unified export.

**Architecture:** Start from Runtime Profiler because it owns the persistent runtime/capture lifecycle and mature panel shell. Rename it to the new product, then port Asset Auditor as an `Assets` subsystem. Add a small shared coordination layer for gameplay-session identity and Deep-Capture-vs-Asset-scan arbitration; keep runtime measurements and asset observations semantically separate and connect them only through explicit IDs/timestamps/context links.

**Tech Stack:** C# 9 / .NET Framework 4.8, Cities: Skylines II official Modding Toolchain, Unity Entities/Profiler APIs, Colossal Game/AssetDatabase/UI APIs, Lib.Harmony only for existing justified Runtime Profiler instrumentation, React/TypeScript/Gameface UI, npm/Vitest, xUnit-style existing .NET test projects.

**Spec:** `docs/superpowers/specs/2026-09-28-runtime-asset-auditor-integration-design.md`

## Global Constraints

- Runtime source is pinned to `pengin0503/CS2-Runtime-Profiler@41836ded2c4cafd1eb5c7e8947ae75de737ea848`.
- Asset source is pinned to `pengin0503/CS2-Asset-Performance-Auditor@5f0650c980175d3e84451ff62227605da2447cd2`.
- Target repository is `pengin0503/CS2-Runtime-Asset-Auditor`; do not modify either source repository.
- Build one primary assembly: `CS2RuntimeAssetAuditor` targeting `net48`.
- Use one `IMod` entry point, one settings identity, one top-left launcher, one panel shell, and one UI bundle.
- Runtime Profiler behavior and Performance Advisor safety semantics must remain intact.
- Asset Auditor pure/domain behavior must be ported rather than rewritten from memory.
- Runtime Deep Capture has priority over heavy Asset work; the two must not overlap silently.
- Do not infer per-asset CPU/GPU/frame-time cost from static asset metadata or census exposure.
- Do not automatically run a full Asset census on every Deep Capture.
- Existing Runtime Harmony usage is allowed; do not add Harmony-based Asset collectors as part of this integration.
- Preserve `Unavailable != 0` and the Asset Auditor observed/derived/estimated/unavailable distinctions.
- Keep pure/core tests independent of local game assemblies; keep adapter tests separate.
- Preserve MIT licensing.
- Commit after every task. Do not defer all commits to the end.

## Review Focus

1. **Diagnostic interference:** a Deep Capture requested during Asset Census/Audit/Deep Inspection must interrupt/cancel at a safe boundary and must not publish a partial snapshot as complete.
2. **World/session rollover:** loading another city/world must create a new diagnostic session and prevent stale runtime/asset evidence from being linked across sessions.
3. **Missing capability/data:** unavailable runtime markers or asset adapters must remain unavailable/degraded rather than becoming zero/default evidence.
4. **UI scale/data volume:** large asset catalogs and many captures must remain paged/scrollable; the merged panel must not create a flat, unbounded tab/list surface.
5. **Export privacy/semantics:** unified JSON must preserve both domains' evidence meaning and sanitization, and must not introduce a fabricated per-asset runtime-cost field.

---

## Planned file structure

The final repository should converge on this structure. Exact small helper files from the source projects may remain where already well-factored, but new integration work must follow these boundaries.

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
    Profiling/
    UI/
  Assets/
    Core/
      Capabilities/
      Census/
      Diagnostics/
      Findings/
      Observations/
      Prefabs/
      Query/
      Rendering/
      Scanning/
    GameIntegration/
      Capabilities/
      Census/
      Prefabs/
      Rendering/
      AssetAuditSystem.cs
    UI/
    Export/
  Export/
  Localization/
UI/src/
  shell/
  runtime/
  assets/
  advisor/
  diagnostics/
tests/
  CS2RuntimeAssetAuditor.Tests/
  CS2RuntimeAssetAuditor.AdapterTests/
```

---

### Task 1: Bootstrap the Runtime baseline and rename the product

**Files:**
- Import from pinned Runtime source: all tracked Runtime Profiler files except `.git` metadata.
- Preserve: `docs/superpowers/specs/2026-09-28-runtime-asset-auditor-integration-design.md`
- Preserve: `docs/superpowers/plans/2026-09-28-runtime-asset-auditor-integration.md`
- Rename: `CS2RuntimeProfiler.sln` -> `CS2RuntimeAssetAuditor.sln`
- Rename: `src/CS2RuntimeProfiler/` -> `src/CS2RuntimeAssetAuditor/`
- Rename: Runtime pure/adapter test project directories to `CS2RuntimeAssetAuditor.Tests` and `CS2RuntimeAssetAuditor.AdapterTests`
- Modify: solution/project files, namespaces, assembly name, `Mod.Id`, UI module identity, package metadata, build scripts, README product references.
- Create: `src/CS2RuntimeAssetAuditor/Core/SourceBaseline.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/RepositoryIdentityTests.cs`

**Interfaces:**
- Produces: one compilable Runtime-only baseline under root namespace `CS2RuntimeAssetAuditor`.
- Produces: `SourceBaseline.RuntimeProfilerCommit == "41836ded2c4cafd1eb5c7e8947ae75de737ea848"` and `SourceBaseline.AssetAuditorCommit == "5f0650c980175d3e84451ff62227605da2447cd2"`.

- [ ] **Step 1: Verify and fetch the pinned Runtime source commit**

Use a temporary/source remote and verify `git rev-parse` resolves exactly `41836ded2c4cafd1eb5c7e8947ae75de737ea848`. Copy/checkout that tree into the new repository without deleting the integration spec/plan.

- [ ] **Step 2: Write the failing identity regression test**

`RepositoryIdentityTests` must assert the new solution/project/assembly/mod ID names and both pinned source SHA constants. Before the rename/constants exist, the test must fail.

- [ ] **Step 3: Rename the Runtime project to `CS2RuntimeAssetAuditor`**

Perform mechanical namespace/project/solution/module renames. Move Runtime feature folders under `src/CS2RuntimeAssetAuditor/Runtime/` when doing so does not require semantic rewrites; keep `Mod.cs`, `Setting.cs`, shared export/localization entry files at product root/shared folders.

Do not change runtime algorithms in this task.

- [ ] **Step 4: Add `SourceBaseline`**

```csharp
public static class SourceBaseline
{
    public const string RuntimeProfilerCommit = "41836ded2c4cafd1eb5c7e8947ae75de737ea848";
    public const string AssetAuditorCommit = "5f0650c980175d3e84451ff62227605da2447cd2";
}
```

- [ ] **Step 5: Run Runtime pure tests and UI tests**

Run the renamed Runtime pure test project and `npm ci` + the existing UI test command from `UI/`.

Expected: all imported Runtime pure/UI tests pass under the new identity. Adapter/game build may remain environment-dependent but must not fail because of stale old namespaces.

- [ ] **Step 6: Scan for stale product identity**

Search tracked non-history files for `CS2RuntimeProfiler` / `CS2 Runtime Profiler`. Remaining occurrences are allowed only in migration/source-baseline documentation or compatibility fixtures.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: bootstrap runtime asset auditor from profiler baseline"
```

---

### Task 2: Establish Runtime parity before Asset code is introduced

**Files:**
- Modify only files required to restore imported Runtime behavior after namespace/path rename.
- Test: existing Runtime pure tests, adapter tests when available, UI regression tests.
- Add/modify: `.github/workflows/*` for renamed paths only if workflows were imported.

**Interfaces:**
- Consumes: renamed Runtime baseline from Task 1.
- Produces: Runtime monitoring/capture/Advisor behavior equivalent to the pinned Runtime source before Asset integration.

- [ ] **Step 1: Run the complete imported Runtime test suite**

Run all pure .NET tests and all UI tests. Record any failures caused by path/namespace/product rename.

Expected before fixes: only migration-induced failures are acceptable.

- [ ] **Step 2: Fix migration-induced failures without changing Runtime semantics**

Preserve existing capture defaults, attribution behavior, Performance Advisor apply/undo/conflict behavior, panel layout/Back handling, privacy behavior, and Harmony packaging.

- [ ] **Step 3: Verify Harmony runtime packaging rule**

Where the full CS2 toolchain is available, build Release and verify `0Harmony.dll` is copied exactly as required by the imported Runtime project. Do not remove the package/reference.

- [ ] **Step 4: Verify Runtime tests/UI tests again**

Expected: all imported Runtime tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: restore runtime profiler parity after integration bootstrap"
```

---

### Task 3: Port Asset Auditor pure core and pure tests

**Files:**
- Create by source-port: `src/CS2RuntimeAssetAuditor/Assets/Core/**`
- Port from: `CS2-Asset-Performance-Auditor/src/CS2AssetPerformanceAuditor/Core/**`
- Create/port tests under: `tests/CS2RuntimeAssetAuditor.Tests/Assets/**`
- Modify: `tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj`

**Interfaces:**
- Produces the existing Asset domain types under `CS2RuntimeAssetAuditor.Assets.Core.*`, including `PrefabKey`, `PrefabRecord`, `CensusSnapshot`, `AssetAnalysisSnapshot`, `RenderAssetKey`, findings/query types, `ScanSession`, and capability/diagnostic models.
- No `Game.*`, Unity ECS, or AssetDatabase runtime dependency may be introduced into pure Asset core.

- [ ] **Step 1: Port one representative Asset test group first and verify RED**

Start with `GeometryMathTests`, `CensusReducerTests`, `AssetQueryServiceTests`, and `FindingEngineTests` under the new namespace. They must fail to compile before the core source is ported.

- [ ] **Step 2: Port `Assets/Core/**` mechanically from the pinned Asset SHA**

Change namespaces only as required. Do not redesign calculations, evidence classes, scan state transitions, query semantics, or diagnostics in this task.

- [ ] **Step 3: Port the remaining Asset pure tests**

Include existing export-independent pure/domain regression tests such as analysis snapshot/deep-inspection/bug regression tests where they only depend on pure core.

- [ ] **Step 4: Run the merged pure test project**

Expected: both Runtime pure tests and all ported Asset pure tests pass in the same test project without local CS2 assemblies.

- [ ] **Step 5: Verify no duplicate ambiguous core names leaked across domains**

Runtime `Core` types remain under `Runtime.Core` (or the established Runtime namespace after Task 1) and Asset types remain under `Assets.Core.*`. Resolve only true namespace collisions; do not flatten both models.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/Core tests/CS2RuntimeAssetAuditor.Tests
git commit -m "feat: port asset auditor core domain"
```

---

### Task 4: Add shared diagnostic session and heavy-work coordination

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticSessionContext.cs`
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticWorkContracts.cs`
- Create: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticWorkCoordinator.cs`
- Modify: Runtime `CaptureSession.cs`
- Modify after Asset port: `Assets/Core/Scanning/ScanSession.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Coordination/DiagnosticSessionContextTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Coordination/DiagnosticWorkCoordinatorTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Coordination/SessionMetadataTests.cs`

**Interfaces:**

```csharp
public sealed class DiagnosticSessionContext
{
    public string SessionId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public string GameVersion { get; }
    public string BuildIdentity { get; }
    public static DiagnosticSessionContext Create(
        DateTimeOffset startedAtUtc,
        string gameVersion,
        string buildIdentity);
}
```

```csharp
public enum DiagnosticWorkKind
{
    RuntimeDeepCapture,
    AssetCatalog,
    AssetCensus,
    AssetAudit,
    AssetDeepInspection
}

public enum DiagnosticWorkDisposition
{
    Started,
    WaitingForRuntimeCapture,
    InterruptedByRuntimeCapture,
    RejectedBusy
}

public sealed class DiagnosticWorkCoordinator
{
    public DiagnosticWorkDecision RequestStart(
        DiagnosticWorkKind kind,
        string operationId,
        DateTimeOffset requestedAtUtc);
    public void Complete(string operationId, DateTimeOffset completedAtUtc);
    public DiagnosticWorkSnapshot Snapshot { get; }
}
```

`DiagnosticWorkSnapshot` must expose the active operation, any waiting Asset operation, and any Asset operation interrupted by a Runtime Deep Capture. It is policy/state only; it does not directly call Unity systems.

- [ ] **Step 1: Write failing coordinator tests**

Cover at minimum:

- Asset Census starts when idle.
- Deep Capture requested while Asset Census is active returns `Started` for Runtime and records the Asset operation as interrupted.
- Asset Audit requested while Deep Capture is active returns `WaitingForRuntimeCapture`.
- Completing Deep Capture clears runtime ownership but does not magically publish/start Asset work; the Asset system must retry the queued request.
- completing the wrong operation ID does not clear the active owner.

- [ ] **Step 2: Implement the minimal pure coordinator**

No game APIs. Make state transitions deterministic and thread-agnostic for the current single-threaded game-system usage.

- [ ] **Step 3: Write failing session metadata tests**

Extend Runtime `CaptureSession` and Asset `ScanSession` with session metadata without breaking existing constructors/tests. Use explicit attach/set methods rather than adding optional constructor ambiguity.

Required additions:

```csharp
public string DiagnosticSessionId { get; private set; }
public DateTimeOffset? StartedAtUtc { get; private set; }   // CaptureSession only if absent
public DateTimeOffset? CompletedAtUtc { get; private set; }
public void AttachDiagnosticSession(string sessionId, DateTimeOffset startedAtUtc);
public void MarkCompletedAt(DateTimeOffset completedAtUtc);
```

For `ScanSession`, retain its existing `StartedAt`; add `DiagnosticSessionId` and `CompletedAtUtc` while preserving existing scan-state invariants.

- [ ] **Step 4: Implement session metadata and rerun old scan/capture tests**

Expected: old Runtime/Asset state-machine behavior remains passing.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Coordination src/CS2RuntimeAssetAuditor tests/CS2RuntimeAssetAuditor.Tests/Coordination
git commit -m "feat: coordinate diagnostic sessions and heavy work"
```

---

### Task 5: Port Asset game integration and wire lifecycle/coordinator behavior

**Files:**
- Create by port: `src/CS2RuntimeAssetAuditor/Assets/GameIntegration/**`
- Port from pinned Asset source: `GameIntegration/Capabilities/**`, `Census/**`, `Prefabs/**`, `Rendering/**`, extension files, `AssetAuditSystem.cs`
- Modify: `src/CS2RuntimeAssetAuditor/Mod.cs`
- Modify: Runtime capture lifecycle/controller/system files at the narrow point where Deep Capture starts/completes.
- Port adapter tests to: `tests/CS2RuntimeAssetAuditor.AdapterTests/Assets/**`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Coordination/AssetScanInterruptionPolicyTests.cs`

**Interfaces:**
- `AssetAuditSystem` continues to expose existing published catalog/census/analysis/capability/telemetry state.
- `AssetAuditSystem` receives/accesses the shared `DiagnosticWorkCoordinator` and current `DiagnosticSessionContext` through one integration-owned provider/service, not duplicated static state.
- Runtime capture start/completion uses the same coordinator.

- [ ] **Step 1: Port adapter tests and verify RED/compile failure before adapters exist**

Port existing Asset adapter tests with only namespace/project-reference updates.

- [ ] **Step 2: Port Asset GameIntegration code from the pinned SHA**

Preserve the existing frame budgets, cancellation behavior, `PublishedAuditState` atomicity, capability probing, catalog generation/world generation checks, and public Game/ECS/AssetDatabase access strategy.

- [ ] **Step 3: Register `AssetAuditSystem` from the single `Mod` entry point**

Keep its `MainLoop` update phase unless current source/toolchain proves a required change.

Do not port the old Asset `Mod.cs` as a second `IMod` entry point.

- [ ] **Step 4: Integrate coordinator checks at scan request/start boundaries**

Required behavior:

- if Deep Capture is active, Asset Census/Audit/Deep Inspection request becomes waiting and UI-visible;
- if Deep Capture starts during active Asset heavy work, call the existing safe cancellation path (`RequestCancellation` / managed cancellation) and do not publish a partial completed snapshot;
- Asset cached query/projection reads remain available;
- after capture completion, pending Asset work is eligible to restart only through the normal request/start path.

- [ ] **Step 5: Attach session IDs/timestamps to Runtime captures and Asset scan sessions**

A new gameplay world/session creates a new `DiagnosticSessionContext`. Never reuse the prior city's `SessionId`.

- [ ] **Step 6: Run pure + adapter tests**

Expected: pure tests all pass. Adapter tests pass where CS2 managed assemblies are configured; otherwise the existing explicit environment gate/error remains clear.

- [ ] **Step 7: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/GameIntegration src/CS2RuntimeAssetAuditor/Mod.cs src/CS2RuntimeAssetAuditor/Runtime tests
git commit -m "feat: integrate asset scanning with runtime lifecycle"
```

---

### Task 6: Merge settings and localization into one product identity

**Files:**
- Modify: `src/CS2RuntimeAssetAuditor/Setting.cs`
- Port selectively from: Asset `AuditorSetting.cs`
- Modify/create: `src/CS2RuntimeAssetAuditor/Localization/**`
- Do not retain: a second Asset settings registration system if it exists only to mirror the old standalone panel.
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Settings/UnifiedSettingTests.cs`
- Test: existing Runtime setting regression tests and ported Asset settings tests.

**Interfaces:**
- One persisted settings ID: `CS2RuntimeAssetAuditor`.
- Logical groups: General/UI, Runtime Monitoring, Deep Capture, Performance Advisor, Asset Audit, Export/Diagnostics.
- Asset scan settings must include the existing frame-budget/page-size/finding-visibility controls that are persisted today.

- [ ] **Step 1: Write failing unified-setting tests**

Assert one settings identity, preserved Runtime defaults, preserved Asset scan defaults, UI scale bounds, and no duplicate registration path.

- [ ] **Step 2: Fold Asset setting properties into the new `Setting`**

Prefer direct typed properties. Do not add reflection-based legacy setting migration.

- [ ] **Step 3: Consolidate localization sources**

Retain Japanese Runtime strings and Japanese/English Asset strings. Replace visible old product names with `CS2 Runtime Asset Auditor`.

- [ ] **Step 4: Run settings/localization/UI text regression tests**

Expected: no missing key for the merged options groups and no stale standalone Asset launcher label.

- [ ] **Step 5: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Setting.cs src/CS2RuntimeAssetAuditor/Localization tests UI
git commit -m "feat: unify settings and localization"
```

---

### Task 7: Port Asset backend UI projection without bloating Runtime UI system

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Assets/UI/**` from Asset `UI/*` projection/contracts/builders.
- Create: `src/CS2RuntimeAssetAuditor/UI/ShellUiContracts.cs` or equivalent shared shell contracts.
- Modify narrowly: existing Runtime `ProfilerUISystem` / Runtime UI projection to coexist with Asset binding group(s).
- Test: port Asset UI projection tests to `tests/CS2RuntimeAssetAuditor.Tests/Assets/UI/**`.
- Test: `tests/CS2RuntimeAssetAuditor.Tests/UI/UnifiedBindingBoundaryTests.cs`

**Interfaces:**
- Preferred binding groups:
  - `CS2RuntimeAssetAuditor.runtime`
  - `CS2RuntimeAssetAuditor.assets`
  - `CS2RuntimeAssetAuditor.shell`
- If framework constraints force one group, expose nested runtime/assets/shell DTOs but retain separate C# builder classes.

Asset commands to preserve:
- request catalog/audit/census;
- cancel current/census scan;
- request asset page/query;
- request deep inspection;
- request asset-focused export.

- [ ] **Step 1: Port Asset backend projection tests and verify RED**

- [ ] **Step 2: Port Asset UI contracts/builders/publish policy**

Keep Asset queries/pagination server-side/bounded as today. Do not move full catalog materialization into React state.

- [ ] **Step 3: Add shared shell snapshot**

At minimum include:

```text
SessionId
RuntimeCaptureState
AssetWorkState
WaitingReason
LatestRuntimeCaptureId
LatestAssetSnapshotId
```

- [ ] **Step 4: Wire Asset triggers into one product binding identity**

Do not create a second launcher visibility lifecycle.

- [ ] **Step 5: Run Runtime UI-projection and Asset UI-projection .NET tests**

Expected: both domains project independently and shared shell DTO contains no Unity/Entity live objects.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Assets/UI src/CS2RuntimeAssetAuditor/Runtime/UI src/CS2RuntimeAssetAuditor/UI tests
git commit -m "feat: add unified runtime and asset UI bindings"
```

---

### Task 8: Merge the frontend into one launcher and one hierarchical panel

**Files:**
- Modify: `UI/src/index.tsx`
- Refactor/rename: existing Runtime `UI/src/profiler/**` into `UI/src/runtime/**` where useful.
- Port: Asset frontend components/tabs/bindings into `UI/src/assets/**`.
- Create: `UI/src/shell/RuntimeAssetAuditorRoot.tsx`
- Create: shell navigation components/styles as needed.
- Test: `UI/src/shell/RuntimeAssetAuditorRoot.test.tsx`
- Port/preserve existing Runtime and Asset frontend tests.

**Interfaces:**

Top-level sections are exactly:

```text
Overview
Runtime
Assets
Advisor
Diagnostics
```

Runtime subviews:

```text
Systems
Mods
Pathfinding
Timeline
Captures
```

Asset subviews:

```text
Assets
Census
Findings
Compare
```

- [ ] **Step 1: Write failing shell tests**

Assert:

- `index.tsx` appends exactly one top-left launcher entry and one game panel root;
- top-level navigation uses the five sections above;
- Back/Escape still routes through `InputActionConsumer` and closes the entire panel;
- Asset section does not render the full asset catalog unpaged;
- waiting/interrupted Asset workload status is visible from the shell.

- [ ] **Step 2: Port Asset frontend components without the old standalone launcher/root lifecycle**

Reuse Assets/Census/Warnings-or-Findings/Compare content, query debounce, deep-inspection action, and export controls. Delete/retire only the old wrapper that owns its own open state/launcher.

- [ ] **Step 3: Build the unified shell around the Runtime panel behavior**

Preserve movable/resizable layout, persisted rect, 75–150% UI scale behavior, `Scrollable`, reset-position action, and Gameface compatibility.

- [ ] **Step 4: Add combined Overview**

Show current runtime state, latest runtime capture, latest asset snapshot, same-session/timestamp distance, and primary actions. Do not add a global score.

- [ ] **Step 5: Run all UI tests and production UI build**

Run `npm test`/Vitest command and `npm run build`.

Expected: all Runtime regression tests and ported Asset UI tests pass, one bundle builds successfully.

- [ ] **Step 6: Commit**

```bash
git add UI
git commit -m "feat: merge runtime and asset interfaces into one panel"
```

---

### Task 9: Build unified JSON export, preserve Asset CSV, and consolidate privacy

**Files:**
- Create: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReport.cs`
- Create: `src/CS2RuntimeAssetAuditor/Export/RuntimeAssetAuditReportBuilder.cs`
- Create: `src/CS2RuntimeAssetAuditor/Export/DiagnosticEvidenceLinkBuilder.cs`
- Port/select: Asset export DTOs/builders under `Assets/Export/**`
- Preserve/port: Asset `CsvSummaryExporter`
- Consolidate: one shared `PrivacySanitizer`
- Modify: Runtime export trigger/path and report filename/directory identity.
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Export/UnifiedReportTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Export/PrivacySanitizerParityTests.cs`
- Port relevant Runtime Advisor export and Asset export tests.

**Interfaces:**

```csharp
public sealed class RuntimeAssetAuditReport
{
    public string SchemaVersion { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public ProductReportSection Product { get; set; }
    public SessionReportSection Session { get; set; }
    public RuntimeReportSection Runtime { get; set; }
    public AdvisorReportSection Advisor { get; set; }
    public AssetReportSection Assets { get; set; }
    public IReadOnlyList<DiagnosticEvidenceLink> EvidenceLinks { get; set; }
    public CapabilityReportSection Capabilities { get; set; }
    public DiagnosticsReportSection Diagnostics { get; set; }
}
```

The exact existing Runtime/Asset DTOs may be wrapped/reused rather than copied if that avoids duplication.

- [ ] **Step 1: Write failing unified export tests**

Assert new schema family, both Runtime and Asset sections, session IDs, evidence-link timing relation, Advisor section, unavailable preservation, and source baseline metadata.

Add a negative assertion: serialized JSON must not contain a field named or semantically equivalent to fabricated `perAssetFrameTimeMs` / `assetGpuTimeMs`.

- [ ] **Step 2: Implement the unified envelope/builder**

Use existing domain report builders as section producers where practical. Do not rewrite all export mapping in one giant method.

- [ ] **Step 3: Consolidate privacy sanitization**

Build parity tests from both legacy sanitizer behaviors: Windows/macOS/Linux home paths, account/user names, and any Asset-specific sensitive path handling must remain redacted.

- [ ] **Step 4: Preserve explicit Asset CSV export**

CSV remains asset-focused and does not attempt to flatten Runtime captures into rows.

- [ ] **Step 5: Update export destination**

Default JSON path becomes `ModsData/CS2RuntimeAssetAuditor/` with collision-safe filenames. Preserve Runtime behavior that avoids overwriting an existing report.

- [ ] **Step 6: Run all export tests**

Expected: legacy Runtime/Advisor export assertions represented, Asset export tests represented, unified tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Export src/CS2RuntimeAssetAuditor/Assets/Export tests
git commit -m "feat: unify diagnostic export and privacy handling"
```

---

### Task 10: Add conservative runtime-to-asset evidence links and investigation handoff

**Files:**
- Create/modify: `src/CS2RuntimeAssetAuditor/Coordination/DiagnosticEvidenceLink.cs`
- Modify: `DiagnosticEvidenceLinkBuilder.cs`
- Modify: shell/backend overview projection.
- Modify: Runtime capture selection -> Asset navigation context.
- Modify: Advisor UI/backend only for non-mutating `Inspect Assets` / `Run Asset Audit` action when appropriate.
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Coordination/DiagnosticEvidenceLinkTests.cs`
- Test: `tests/CS2RuntimeAssetAuditor.Tests/Advisor/AssetInvestigationHandoffTests.cs`
- Test: UI handoff tests.

**Interfaces:**

```csharp
public enum EvidenceTimingRelation
{
    Before,
    Overlapping,
    After
}

public sealed class DiagnosticEvidenceLink
{
    public string DiagnosticSessionId { get; }
    public string RuntimeCaptureId { get; }
    public string AssetSnapshotId { get; }
    public EvidenceTimingRelation TimingRelation { get; }
    public double SeparationSeconds { get; }
}
```

Links are created only when session IDs match.

- [ ] **Step 1: Write failing link tests**

Cover same-session Before/Overlapping/After, cross-session no-link, missing timestamp no-link, deterministic ordering, and zero/positive separation semantics.

- [ ] **Step 2: Implement `DiagnosticEvidenceLinkBuilder`**

Use timestamps only. Do not inspect triangle/material/texture values when determining temporal relationship.

- [ ] **Step 3: Add Runtime -> Assets navigation context**

Selecting a Runtime capture and choosing `Inspect Assets` opens the Assets section with the capture ID retained as context. If no same-session Asset snapshot exists, offer a manual audit action.

- [ ] **Step 4: Gate Asset investigation messaging by runtime evidence**

Rendering/GPU pressure may surface existing Asset findings as investigation candidates with the static-evidence disclaimer. CPU/pathfinding/simulation-bound captures must not automatically promote geometry/texture findings as explanations.

- [ ] **Step 5: Keep Performance Advisor mutation scope unchanged**

Asset handoff is non-mutating. Do not make Asset findings directly apply game settings.

- [ ] **Step 6: Run coordination/Advisor/UI tests**

Expected: no causal language or per-asset runtime-cost field is introduced by the bridge.

- [ ] **Step 7: Commit**

```bash
git add src/CS2RuntimeAssetAuditor/Coordination src/CS2RuntimeAssetAuditor/Runtime src/CS2RuntimeAssetAuditor/Assets UI tests
git commit -m "feat: link runtime captures with asset investigation context"
```

---

### Task 11: CI, documentation, full verification, and migration readiness

**Files:**
- Modify/create: `.github/workflows/pure-tests.yml`
- Modify/create: `.github/workflows/ui-tests.yml`
- Preserve/update toolchain-dependent validation workflow only if it is already valid for the source project.
- Rewrite: `README.md` for the merged user-facing product.
- Create/update: `docs/validation/runtime-validation.md`
- Create: `docs/validation/integration-validation.md`
- Update license only if missing; retain MIT.

**Interfaces:**
- Produces a repository ready for local CS2 build/in-game validation.
- Does not archive/delete the two legacy repositories automatically.

- [ ] **Step 1: Make CI paths/project names target the merged repository**

Pure CI must not require Cities: Skylines II assemblies. UI CI must run install/test/build from `UI/`.

- [ ] **Step 2: Run the complete pure test suite**

Run all tests in `CS2RuntimeAssetAuditor.Tests`.

Expected: PASS.

- [ ] **Step 3: Run the complete UI suite and production build**

Expected: tests PASS and bundle build succeeds.

- [ ] **Step 4: Run adapter tests when CS2 managed assemblies are available**

Expected: PASS. If the Work environment lacks the game assemblies, verify the project fails/skips with the explicit existing environment message rather than a misleading compile error.

- [ ] **Step 5: Run full Release mod build when the official toolchain is available**

Verify:

- one primary mod assembly;
- one UI bundle;
- `0Harmony.dll` copied when required;
- no second `CS2AssetPerformanceAuditor.dll` or `CS2RuntimeProfiler.dll` artifact;
- no game DLLs committed/copied into repository output packaging unexpectedly.

- [ ] **Step 6: Update README**

Document combined capabilities, one-panel workflow, installation/build, evidence limitations, manual Asset scan behavior, Deep Capture priority, export locations, and new settings identity. Keep internal implementation-policy details out of user-facing sections unless they matter to installation/behavior.

- [ ] **Step 7: Create the in-game integration validation checklist**

Include at minimum:

1. startup with only merged mod enabled;
2. Normal Monitoring idle overhead;
3. manual and automatic Deep Capture;
4. Systems/Mods/Pathfinding/Timeline/Captures;
5. Advisor diagnose/apply/undo/conflict;
6. Asset catalog;
7. Census on small and large cities;
8. cancel Census;
9. start Deep Capture during Census and verify Asset cancellation/no partial publish;
10. request Census during Deep Capture and verify waiting state;
11. Asset Audit/deep inspection;
12. large asset list search/filter/page;
13. unified JSON + Asset CSV export/privacy;
14. one launcher, movable/resizable panel, Esc/B close;
15. Japanese/English UI;
16. save/load and city switching creates new session ID;
17. one Asset capability intentionally unavailable/degraded without breaking Runtime diagnostics.

- [ ] **Step 8: Search for stale standalone product artifacts**

There must be no second `IMod`, no second launcher, no standalone old assembly output, and no accidental old settings identity used as the active product.

- [ ] **Step 9: Run `git status` and verify the tree is clean after tests/build cleanup**

Do not commit generated `node_modules`, game DLLs, deployed mod output, or local user-data files.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "docs: finalize runtime asset auditor integration readiness"
```

---

## Final verification before implementation is declared complete

The Work session must run the following evidence-producing checks before claiming completion:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.Tests/CS2RuntimeAssetAuditor.Tests.csproj -c Release
cd UI
npm ci
npm test -- --run
npm run build
cd ..
```

When CS2 managed/toolchain paths are available, also run:

```bash
dotnet test tests/CS2RuntimeAssetAuditor.AdapterTests/CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
dotnet build src/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor.csproj -c Release
```

Then inspect the build/deploy output for one merged assembly/UI bundle and required private Harmony runtime dependency only.

A green local unit/UI suite is not sufficient to claim in-game compatibility. Report separately which items from `docs/validation/integration-validation.md` were actually executed in Cities: Skylines II.

## Execution handoff

Implementation is intentionally **not** started in this chat. The next step is a ChatGPT Work session using `superpowers:executing-plans`, reading both this plan and the linked design spec first, then executing Tasks 1–11 in order with RED -> GREEN -> verification -> commit discipline.
