# CS2 Runtime Profiler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Cities: Skylines II runtime profiler that keeps normal-play overhead low, automatically performs deep captures during simulation slowdowns, and visualizes measured system/mod/pathfinding/runtime costs with explicit confidence and profiler-overhead information.

**Architecture:** Keep pure profiling logic in game-independent source files so it can be unit-tested without CS2 binaries, while the mod assembly supplies CS2/Unity adapters, collectors, bindings, and runtime discovery. Normal mode samples a small metric set into bounded ring buffers; Deep Capture activates all safely discoverable collectors and profiler markers, using batching when needed. The React/TypeScript UI consumes immutable snapshots from one C# binding system, and JSON export serializes the same capture model.

**Tech Stack:** C# / .NET Framework 4.7.2 for the CS2 mod, Unity Entities 1.3.10-era APIs, Unity Profiling `ProfilerRecorder`, CS2 `GameSystemBase` / `UISystemBase`, Harmony inspection when available, React + TypeScript + SCSS using the official CS2 UI toolchain, NUnit for game-independent C# unit tests.

**Spec:** `docs/superpowers/specs/2026-09-24-cs2-runtime-profiler-design.md`

## Global Constraints

- Version 1 is diagnostic/read-mostly: do not throttle, disable, reconfigure, or otherwise alter simulation systems to recover performance.
- Never redistribute or commit Cities: Skylines II game DLLs; they are local build references only.
- `Unavailable` is a real state and must never be silently displayed or serialized as zero.
- Unattributed Burst/Job worker time must remain unattributed unless a direct runtime relationship is measurable.
- Normal monitoring targets CPU overhead under 1% and simulation-speed impact under 2% where practical; these are validation targets, not claims until measured in game.
- Deep Capture attempts every safely discoverable category and reports coverage, batching, and limitations.
- Collector failures are isolated; one broken collector cannot abort profiling or the simulation loop.
- UI language must present correlation/evidence, not unsupported causal blame.
- Runtime reflection scans are lifecycle-gated and never performed uncapped every frame.
- Private/internal CS2 access lives behind version-tolerant adapters.

## Review Focus

- A selected simulation speed of `0` or paused simulation must not divide by zero or trigger Deep Capture.
- Hundreds or thousands of profiler markers must not create unbounded recorder count or memory growth; batching and coverage reporting must remain correct.
- Reflection failures (`ReflectionTypeLoadException`, missing private members, inaccessible assemblies) must degrade to diagnostics/`Unavailable` without escaping into the simulation loop.
- A patched vanilla system must show patch owners without assigning the vanilla system's entire measured cost to those mods.
- Export must exclude absolute local paths and Windows account names even when exceptions/diagnostics contain source paths.

---

## File Structure

The implementation will use these responsibility boundaries:

```text
CS2RuntimeProfiler.sln
.gitignore
README.md
src/CS2RuntimeProfiler/
  CS2RuntimeProfiler.csproj
  Mod.cs
  Setting.cs
  Core/
    MetricConfidence.cs
    MetricAvailability.cs
    MetricSample.cs
    RollingMetricSeries.cs
    MetricStatistics.cs
    SimulationEfficiency.cs
    CaptureState.cs
    CaptureTrigger.cs
    DeepCaptureStateMachine.cs
    MarkerBatchPlanner.cs
    CaptureSession.cs
    CapabilityInfo.cs
    SystemDescriptor.cs
    PatchOwnerInfo.cs
  Profiling/
    ProfilerCatalog.cs
    RecorderManager.cs
    RecorderHandle.cs
    DeepCaptureController.cs
    ProfilerOverheadTracker.cs
  Collectors/
    IMetricCollector.cs
    GlobalMetricsCollector.cs
    SystemProfilerCollector.cs
    EntityMetricsCollector.cs
    PathfindingCollector.cs
    ReflectionMetricAccessor.cs
  Attribution/
    AssemblyAttributor.cs
    ModAttributor.cs
    HarmonyPatchInspector.cs
  Export/
    PerformanceReport.cs
    ReportExporter.cs
    PrivacySanitizer.cs
  UI/
    ProfilerUISystem.cs
    UiSnapshot.cs
    UiSnapshotBuilder.cs
UI/
  UI.esproj
  mod.json
  package.json
  tsconfig.json
  src/
    index.tsx
    profiler/
      ProfilerRoot.tsx
      profiler.module.scss
      bindings.ts
      format.ts
      components/ProfilerHud.tsx
      components/MetricBadge.tsx
      tabs/OverviewTab.tsx
      tabs/SystemsTab.tsx
      tabs/ModsTab.tsx
      tabs/PathfindingTab.tsx
      tabs/TimelineTab.tsx
      tabs/CapturesTab.tsx
      tabs/DiagnosticsTab.tsx
tests/CS2RuntimeProfiler.Tests/
  CS2RuntimeProfiler.Tests.csproj
  RollingMetricSeriesTests.cs
  MetricStatisticsTests.cs
  SimulationEfficiencyTests.cs
  DeepCaptureStateMachineTests.cs
  MarkerBatchPlannerTests.cs
  ReflectionMetricAccessorTests.cs
  AttributionTests.cs
  PrivacySanitizerTests.cs
```

The test project compiles only the game-independent `Core`, selected reflection/attribution helpers, and export sanitization sources by linked source files. It does not require proprietary CS2 DLLs.

---

### Task 1: Repository bootstrap and testable core boundary

**Files:**
- Create: `.gitignore`
- Create: `CS2RuntimeProfiler.sln`
- Create: `src/CS2RuntimeProfiler/CS2RuntimeProfiler.csproj`
- Create: `src/CS2RuntimeProfiler/Mod.cs`
- Create: `src/CS2RuntimeProfiler/Setting.cs`
- Create: `src/CS2RuntimeProfiler/Core/MetricConfidence.cs`
- Create: `src/CS2RuntimeProfiler/Core/MetricAvailability.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj`
- Create: `tests/CS2RuntimeProfiler.Tests/CoreSmokeTests.cs`

**Interfaces:**
- Produces: `MetricConfidence`, `MetricAvailability`, a loadable CS2 `IMod` entry point, and a test project that can compile game-independent sources without game DLLs.
- Consumes: official CS2 `Mod.props` / `Mod.targets` through `CSII_TOOLPATH`; local game binaries are never committed.

- [ ] **Step 1: Write the failing core smoke test**

```csharp
using NUnit.Framework;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Tests;

public class CoreSmokeTests
{
    [Test]
    public void Unavailable_is_not_equivalent_to_zero_measurement()
    {
        Assert.That(MetricAvailability.Unavailable, Is.Not.EqualTo(MetricAvailability.Available));
        Assert.That(MetricConfidence.Unavailable, Is.Not.EqualTo(MetricConfidence.Full));
    }
}
```

- [ ] **Step 2: Create the test project with linked pure sources and verify the test initially fails**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="../../src/CS2RuntimeProfiler/Core/**/*.cs" LinkBase="Core" />
  </ItemGroup>
</Project>
```

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: FAIL because `MetricAvailability` and `MetricConfidence` do not exist.

- [ ] **Step 3: Add the minimal core enums**

```csharp
namespace CS2RuntimeProfiler.Core
{
    public enum MetricAvailability
    {
        Available,
        Unavailable
    }

    public enum MetricConfidence
    {
        Full,
        Managed,
        Indirect,
        Unavailable
    }
}
```

Place each enum in its corresponding file.

- [ ] **Step 4: Add CS2 project scaffolding without committing game DLLs**

`src/CS2RuntimeProfiler/CS2RuntimeProfiler.csproj` must target `net472`, import the official `Mod.props` and `Mod.targets`, reference `Game`, Colossal UI/logging assemblies, `Unity.Entities`, `Unity.Collections`, `Unity.Burst`, `Unity.Mathematics`, `UnityEngine.CoreModule`, and `Unity.Profiling.Core` with `<Private>false</Private>`, and run `npm run build` in `UI` after the C# build.

`Mod.cs` must implement `IMod`, create the settings object, register it with the options UI, and schedule no expensive profiling systems yet.

`.gitignore` must include at minimum:

```gitignore
bin/
obj/
.vs/
.idea/
node_modules/
*.user
*.suo
*.dll
!docs/**/*.dll.md
local-game-references/
```

- [ ] **Step 5: Run the pure tests**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add .gitignore CS2RuntimeProfiler.sln src tests
git commit -m "build: bootstrap runtime profiler project"
```

---

### Task 2: Bounded metric history, statistics, and simulation efficiency

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/MetricSample.cs`
- Create: `src/CS2RuntimeProfiler/Core/RollingMetricSeries.cs`
- Create: `src/CS2RuntimeProfiler/Core/MetricStatistics.cs`
- Create: `src/CS2RuntimeProfiler/Core/SimulationEfficiency.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/RollingMetricSeriesTests.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/MetricStatisticsTests.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/SimulationEfficiencyTests.cs`

**Interfaces:**
- Produces: `MetricSample`, `RollingMetricSeries.Add`, `RollingMetricSeries.Snapshot`, `MetricStatistics.From`, and `SimulationEfficiency.Calculate`.
- Consumes: `MetricConfidence` and `MetricAvailability` from Task 1.

- [ ] **Step 1: Write failing bounded-history and statistics tests**

```csharp
[Test]
public void Rolling_series_discards_oldest_sample_at_capacity()
{
    var series = new RollingMetricSeries(3);
    series.Add(new MetricSample(1, 10, MetricConfidence.Full));
    series.Add(new MetricSample(2, 20, MetricConfidence.Full));
    series.Add(new MetricSample(3, 30, MetricConfidence.Full));
    series.Add(new MetricSample(4, 40, MetricConfidence.Full));

    Assert.That(series.Snapshot().Select(x => x.Value), Is.EqualTo(new[] { 20d, 30d, 40d }));
}

[Test]
public void Statistics_include_median_p95_p99_and_max()
{
    var values = Enumerable.Range(1, 100).Select(x => (double)x).ToArray();
    var stats = MetricStatistics.From(values);

    Assert.That(stats.Mean, Is.EqualTo(50.5).Within(0.001));
    Assert.That(stats.Median, Is.EqualTo(50.5).Within(0.001));
    Assert.That(stats.P95, Is.EqualTo(95).Within(0.001));
    Assert.That(stats.P99, Is.EqualTo(99).Within(0.001));
    Assert.That(stats.Max, Is.EqualTo(100));
}
```

- [ ] **Step 2: Write the failing paused-speed safety test**

```csharp
[TestCase(0, 0, 0)]
[TestCase(4, 2.5, 0.625)]
public void Efficiency_is_safe_for_paused_and_running_simulation(double selected, double actual, double expected)
{
    Assert.That(SimulationEfficiency.Calculate(selected, actual), Is.EqualTo(expected).Within(0.0001));
}
```

This pins the Review Focus requirement that paused simulation does not divide by zero or trigger invalid ratios.

- [ ] **Step 3: Run the tests and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: FAIL because the new types are undefined.

- [ ] **Step 4: Implement bounded history and statistics**

`MetricSample` is an immutable struct with `double TimestampSeconds`, `double Value`, and `MetricConfidence Confidence`.

`RollingMetricSeries` uses a fixed-size array/ring with O(1) append and returns snapshots in chronological order. It must reject capacities below `1` with `ArgumentOutOfRangeException`.

`MetricStatistics.From(IReadOnlyCollection<double>)` returns a value object containing `Current`, `Mean`, `Median`, `P95`, `P99`, `Max`, `Total`, and `Count`. Percentiles use nearest-rank semantics: `ceil(p * n)` clamped to `[1,n]`.

`SimulationEfficiency.Calculate(double selected, double actual)` returns `0` for `selected <= 0`; otherwise it returns `Math.Max(0, actual / selected)` without silently clamping values above `1`.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CS2RuntimeProfiler/Core tests/CS2RuntimeProfiler.Tests
git commit -m "feat: add bounded metric history and statistics"
```

---

### Task 3: Deep-capture trigger and state machine

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/CaptureState.cs`
- Create: `src/CS2RuntimeProfiler/Core/CaptureTrigger.cs`
- Create: `src/CS2RuntimeProfiler/Core/DeepCaptureStateMachine.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/DeepCaptureStateMachineTests.cs`

**Interfaces:**
- Consumes: `SimulationEfficiency.Calculate` from Task 2.
- Produces: `DeepCaptureStateMachine.Observe`, `RequestManualCapture`, `CaptureState`, and transition timestamps used by the runtime controller in Task 6.

- [ ] **Step 1: Write failing trigger/state tests**

```csharp
[Test]
public void Sustained_low_efficiency_enters_deep_capture_after_two_seconds()
{
    var machine = new DeepCaptureStateMachine(
        efficiencyThreshold: 0.80,
        sustainSeconds: 2,
        deepSeconds: 10,
        postSeconds: 5,
        cooldownSeconds: 30);

    machine.Observe(0, selectedSpeed: 4, actualSpeed: 2.5);
    machine.Observe(1.9, 4, 2.5);
    Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));

    machine.Observe(2.0, 4, 2.5);
    Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
}

[Test]
public void Paused_simulation_never_auto_triggers()
{
    var machine = DeepCaptureStateMachine.CreateDefault();
    machine.Observe(0, 0, 0);
    machine.Observe(10, 0, 0);
    Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
}

[Test]
public void Manual_request_enters_deep_capture_immediately()
{
    var machine = DeepCaptureStateMachine.CreateDefault();
    machine.RequestManualCapture(5);
    Assert.That(machine.State, Is.EqualTo(CaptureState.DeepCapture));
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter DeepCaptureStateMachineTests -v minimal`

Expected: FAIL with missing types.

- [ ] **Step 3: Implement deterministic state transitions**

States:

```csharp
public enum CaptureState
{
    Monitoring,
    DeepCapture,
    PostBuffer,
    Cooldown
}
```

`Observe(now, selectedSpeed, actualSpeed)` must:

1. ignore automatic triggering when `selectedSpeed <= 0`;
2. start the sustained-low timer only while efficiency is strictly below the threshold;
3. reset the sustained-low timer when efficiency recovers;
4. transition `DeepCapture -> PostBuffer -> Cooldown -> Monitoring` by elapsed wall-clock seconds;
5. prevent new automatic captures during PostBuffer/Cooldown;
6. permit `RequestManualCapture` from Monitoring and Cooldown, resetting deep-capture timing.

- [ ] **Step 4: Add hysteresis regression test**

```csharp
[Test]
public void Brief_recovery_resets_auto_trigger_sustain_window()
{
    var machine = DeepCaptureStateMachine.CreateDefault();
    machine.Observe(0, 4, 2.5);
    machine.Observe(1.5, 4, 4);
    machine.Observe(2, 4, 2.5);
    machine.Observe(3.9, 4, 2.5);
    Assert.That(machine.State, Is.EqualTo(CaptureState.Monitoring));
}
```

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: PASS.

```bash
git add src/CS2RuntimeProfiler/Core tests/CS2RuntimeProfiler.Tests
git commit -m "feat: add deep capture state machine"
```

---

### Task 4: System catalog, reflection safety, and assembly/mod attribution

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/SystemDescriptor.cs`
- Create: `src/CS2RuntimeProfiler/Core/PatchOwnerInfo.cs`
- Create: `src/CS2RuntimeProfiler/Profiling/ProfilerCatalog.cs`
- Create: `src/CS2RuntimeProfiler/Attribution/AssemblyAttributor.cs`
- Create: `src/CS2RuntimeProfiler/Attribution/ModAttributor.cs`
- Create: `src/CS2RuntimeProfiler/Attribution/HarmonyPatchInspector.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/AttributionTests.cs`

**Interfaces:**
- Produces: `ProfilerCatalog.Discover()`, immutable `SystemDescriptor` records, `AssemblyAttributor.Classify`, `ModAttributor.Resolve`, and `HarmonyPatchInspector.GetPatchOwners(MethodBase)`.
- Consumes: loaded runtime assemblies; Harmony access is optional and feature-detected.

- [ ] **Step 1: Write failing ownership tests**

```csharp
[TestCase("Game", "Vanilla")]
[TestCase("Unity.Entities", "Runtime")]
[TestCase("TourismOverhaul", "Mod")]
public void Assembly_classification_is_deterministic(string assemblyName, string expected)
{
    Assert.That(AssemblyAttributor.ClassifyName(assemblyName).ToString(), Is.EqualTo(expected));
}

[Test]
public void Patch_owners_do_not_reassign_vanilla_system_ownership()
{
    var descriptor = new SystemDescriptor(
        fullTypeName: "Game.Simulation.TrafficLightSystem",
        assemblyName: "Game",
        sourceKind: SystemSourceKind.Vanilla,
        modName: null,
        confidence: MetricConfidence.Full,
        patchOwners: new[] { new PatchOwnerInfo("TrafficLightsEnhancement", "Traffic Lights Enhancement") });

    Assert.That(descriptor.SourceKind, Is.EqualTo(SystemSourceKind.Vanilla));
    Assert.That(descriptor.PatchOwners.Single().OwnerId, Is.EqualTo("TrafficLightsEnhancement"));
}
```

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter AttributionTests -v minimal`

Expected: FAIL because attribution types are missing.

- [ ] **Step 3: Implement safe reflection discovery**

`ProfilerCatalog.Discover()` must iterate `AppDomain.CurrentDomain.GetAssemblies()`. For every assembly, use a helper equivalent to:

```csharp
private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
{
    try { return assembly.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    catch { return Array.Empty<Type>(); }
}
```

A type is a candidate ECS system when it derives from `Unity.Entities.ComponentSystemBase` or matches the runtime system base by full name when direct type access is unavailable. Discovery runs only at initialization/manual refresh.

- [ ] **Step 4: Implement assembly and patch metadata without cost reallocation**

`AssemblyAttributor` classifies at minimum `Game` as Vanilla, `Unity.*`/`Colossal.*` as Runtime, the profiler assembly as Profiler, and remaining non-framework assemblies as Mod/Unknown based on the runtime mod registry.

`HarmonyPatchInspector` must feature-detect Harmony by assembly/type lookup and return an empty owner list when Harmony is absent. It may expose patch owner IDs/names but never mutate patches or attribute total vanilla system timing to a patch owner.

- [ ] **Step 5: Add reflection-failure regression coverage**

Create a test-only helper path that passes a type provider throwing `ReflectionTypeLoadException`/`InvalidOperationException` and assert catalog discovery returns partial/empty results instead of throwing. This pins the Review Focus reflection-failure requirement.

- [ ] **Step 6: Run tests and commit**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: PASS.

```bash
git add src/CS2RuntimeProfiler/Core src/CS2RuntimeProfiler/Profiling src/CS2RuntimeProfiler/Attribution tests
git commit -m "feat: discover and attribute runtime systems"
```

---

### Task 5: Recorder catalog and low-overhead global metrics

**Files:**
- Create: `src/CS2RuntimeProfiler/Profiling/RecorderHandle.cs`
- Create: `src/CS2RuntimeProfiler/Profiling/RecorderManager.cs`
- Create: `src/CS2RuntimeProfiler/Collectors/IMetricCollector.cs`
- Create: `src/CS2RuntimeProfiler/Collectors/GlobalMetricsCollector.cs`
- Create: `src/CS2RuntimeProfiler/Profiling/ProfilerOverheadTracker.cs`
- Modify: `src/CS2RuntimeProfiler/Mod.cs`

**Interfaces:**
- Consumes: Unity `ProfilerRecorderHandle.GetAvailable`, `ProfilerRecorder`, current `Game.Simulation.SimulationSystem`, and core rolling series.
- Produces: `RecorderManager.DiscoverAvailableMarkers`, `RecorderManager.SampleActive`, `GlobalMetricsCollector.Sample`, and profiler-overhead samples.

- [ ] **Step 1: Add an adapter seam before touching Unity recorder APIs**

Define internal interfaces:

```csharp
internal interface IRecorderBackend
{
    IReadOnlyList<RecorderDescriptor> Discover();
    IActiveRecorder Start(RecorderDescriptor descriptor, int capacity);
}

internal interface ISimulationSpeedSource
{
    double SelectedSpeed { get; }
    double ActualSpeed { get; }
}
```

`RecorderManager` depends on `IRecorderBackend`; the production backend wraps Unity APIs. This keeps discovery policy testable even though Unity itself is not unit-tested outside the game.

- [ ] **Step 2: Implement runtime discovery with availability filtering**

Use `ProfilerRecorderHandle.GetAvailable(...)` to enumerate runtime markers. Persist marker category/name and runtime availability metadata. Recorder creation failures are caught per marker and recorded as `CapabilityInfo` failures.

Do not start every discovered recorder in Normal mode.

- [ ] **Step 3: Implement GlobalMetricsCollector with fixed Normal-mode cadence**

At minimum collect:

- selected speed from `SimulationSystem.selectedSpeed`;
- actual speed from `SimulationSystem.smoothSpeed`;
- derived efficiency using `SimulationEfficiency.Calculate`;
- FPS/frame time from Unity timing where exposed;
- main-thread/render-thread/GPU metrics only when a matching available marker exists;
- GC/memory metrics only when available;
- profiler self-overhead.

Every metric carries `MetricAvailability` and `MetricConfidence`.

- [ ] **Step 4: Wire only Normal-mode lightweight systems into `Mod.OnLoad`**

Schedule a lightweight profiler coordinator/UI feed at an appropriate non-mutating phase. No full catalog reflection or full recorder set runs each frame.

- [ ] **Step 5: Build against the user's local CS2 references**

Run from a Windows CS2 development environment:

```powershell
dotnet build .\src\CS2RuntimeProfiler\CS2RuntimeProfiler.csproj -c Debug
```

Expected: C# project builds with no copied proprietary game DLLs in Git status.

- [ ] **Step 6: Run pure tests and commit**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal`

Expected: PASS.

```bash
git add src/CS2RuntimeProfiler
git commit -m "feat: add runtime recorder discovery and global metrics"
```

---

### Task 6: Marker batching, Deep Capture controller, and overhead backoff

**Files:**
- Create: `src/CS2RuntimeProfiler/Core/MarkerBatchPlanner.cs`
- Create: `src/CS2RuntimeProfiler/Core/CapabilityInfo.cs`
- Create: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Create: `src/CS2RuntimeProfiler/Profiling/DeepCaptureController.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/MarkerBatchPlannerTests.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/RecorderManager.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/ProfilerOverheadTracker.cs`

**Interfaces:**
- Consumes: `DeepCaptureStateMachine`, discovered recorder descriptors, overhead measurements.
- Produces: bounded `CaptureSession`, batching/coverage metadata, and Deep Capture activation/deactivation of recorders.

- [ ] **Step 1: Write failing large-marker batching tests**

```csharp
[Test]
public void Six_hundred_markers_are_covered_in_four_batches_of_150()
{
    var ids = Enumerable.Range(1, 600).Select(i => $"m{i}").ToArray();
    var plan = MarkerBatchPlanner.Create(ids, maxConcurrent: 150);

    Assert.That(plan.Batches.Count, Is.EqualTo(4));
    Assert.That(plan.Batches.SelectMany(x => x).Distinct().Count(), Is.EqualTo(600));
    Assert.That(plan.CoverageRatio, Is.EqualTo(1.0));
}

[Test]
public void Empty_marker_set_reports_complete_empty_coverage_without_divide_by_zero()
{
    var plan = MarkerBatchPlanner.Create(Array.Empty<string>(), 150);
    Assert.That(plan.CoverageRatio, Is.EqualTo(1.0));
    Assert.That(plan.Batches, Is.Empty);
}
```

This pins the Review Focus large-marker requirement.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter MarkerBatchPlannerTests -v minimal`

Expected: FAIL because `MarkerBatchPlanner` is undefined.

- [ ] **Step 3: Implement stable batching and capture metadata**

`MarkerBatchPlanner.Create` must preserve deterministic input order, never duplicate a marker across batches, and report discovered/captured counts plus simultaneous-vs-batched mode.

`CaptureSession` stores trigger metadata, pre/deep/post samples, capability map, system samples, marker samples, domain/pathfinding samples, patch map, overhead samples, and warnings. Collections are bounded by configured sample/capture limits.

- [ ] **Step 4: Implement DeepCaptureController**

On transition to Deep Capture:

1. freeze/copy the last 5 seconds of Normal-mode ring-buffer history into the new session;
2. activate all safe collector categories;
3. build marker batches from every discovered safe marker;
4. rotate batches by elapsed capture time;
5. record coverage and same-frame-correlation limitation;
6. stop heavy recorders at the end of Deep Capture;
7. collect 5 seconds of post-buffer lightweight history;
8. finalize the session and enter cooldown.

- [ ] **Step 5: Add overhead backoff**

When measured profiler overhead approaches the configured ceiling (initial target 8%), first reduce simultaneous recorder count by selecting a smaller batch size; if still high, reduce sampling frequency; if still high, attach a distortion warning to the capture. Do not silently drop entire discovered categories.

- [ ] **Step 6: Run tests/build and commit**

Run:

```bash
dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal
```

Expected: PASS.

Then run the CS2 build on the Windows development environment.

```bash
git add src tests
git commit -m "feat: add deep capture orchestration and marker batching"
```

---

### Task 7: Fail-open Pathfinding and entity/domain collectors

**Files:**
- Create: `src/CS2RuntimeProfiler/Collectors/ReflectionMetricAccessor.cs`
- Create: `src/CS2RuntimeProfiler/Collectors/PathfindingCollector.cs`
- Create: `src/CS2RuntimeProfiler/Collectors/EntityMetricsCollector.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/ReflectionMetricAccessorTests.cs`

**Interfaces:**
- Consumes: current-game systems such as `Game.Pathfind.PathfindQueueSystem`, `Game.Simulation.PathfindSetupSystem`, `Game.Pathfind.PathfindResultSystem`, and ECS queries.
- Produces: named domain metrics with explicit availability/confidence and diagnostic failure reasons.

- [ ] **Step 1: Write failing missing-member tests**

```csharp
private sealed class FakeTarget { public int Present = 12; }

[Test]
public void Missing_member_returns_unavailable_instead_of_throwing()
{
    var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "DoesNotExist");
    var result = accessor.TryRead(new FakeTarget());

    Assert.That(result.Availability, Is.EqualTo(MetricAvailability.Unavailable));
    Assert.That(result.Value, Is.Null);
    Assert.That(result.Reason, Does.Contain("DoesNotExist"));
}

[Test]
public void Existing_numeric_member_is_read_without_mutation()
{
    var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "Present");
    var target = new FakeTarget();
    var result = accessor.TryRead(target);

    Assert.That(result.Value, Is.EqualTo(12));
    Assert.That(target.Present, Is.EqualTo(12));
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter ReflectionMetricAccessorTests -v minimal`

Expected: FAIL because the accessor is undefined.

- [ ] **Step 3: Implement cached read-only reflection access**

Resolve fields/properties once at collector initialization and cache delegates/member handles. Catch read exceptions and return `Unavailable` with a reason. Never write to the reflected member.

- [ ] **Step 4: Implement the current 1.6.x Pathfinding adapter conservatively**

For each candidate counter, first verify the member/type at runtime. Expose only values whose semantics can be established from the supplied `Game.dll`/runtime behavior. Candidate outputs include pending/queued/completed counts, requests/sec, results/sec, and queue delta/sec. If semantics cannot be verified, expose the item in Diagnostics as unsupported rather than inventing a value.

- [ ] **Step 5: Implement inexpensive entity/domain counts**

Use cached `EntityQuery` instances and throttled updates for citizen/household/tourist/vehicle/public-transport/cargo/service domains where types are available. Treat counts as `Indirect` supporting metrics, not causal CPU attribution.

- [ ] **Step 6: Run tests/build and commit**

Run pure tests and a Windows CS2 build. Expected: all tests pass; missing reflected members do not throw.

```bash
git add src tests
git commit -m "feat: add fail-open pathfinding and entity collectors"
```

---

### Task 8: System timing aggregation and confidence-aware attribution

**Files:**
- Create: `src/CS2RuntimeProfiler/Collectors/SystemProfilerCollector.cs`
- Modify: `src/CS2RuntimeProfiler/Core/SystemDescriptor.cs`
- Modify: `src/CS2RuntimeProfiler/Core/CaptureSession.cs`
- Modify: `src/CS2RuntimeProfiler/Profiling/ProfilerCatalog.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/SystemAggregationTests.cs`

**Interfaces:**
- Consumes: discovered system profiler markers, fallback managed instrumentation when safe, attribution metadata, and `MetricStatistics`.
- Produces: per-system `Current/Mean/Median/P95/P99/Max/Total/Calls`, measurement confidence, mod grouping data, and `Unattributed Jobs/Burst` bucket.

- [ ] **Step 1: Write failing aggregation/confidence tests**

```csharp
[Test]
public void Managed_only_system_does_not_claim_full_job_cost()
{
    var result = SystemMetricAggregate.FromManagedSamples("ExampleSystem", new[] { 0.3, 0.5, 0.4 });
    Assert.That(result.Confidence, Is.EqualTo(MetricConfidence.Managed));
    Assert.That(result.MeanMilliseconds, Is.EqualTo(0.4).Within(0.001));
}

[Test]
public void Unattributed_worker_time_remains_separate()
{
    var capture = new SystemTimingSnapshot();
    capture.AddSystem("ExampleSystem", 1.0, MetricConfidence.Managed);
    capture.SetUnattributedJobsMilliseconds(7.8);

    Assert.That(capture.UnattributedJobsMilliseconds, Is.EqualTo(7.8));
    Assert.That(capture.Systems.Single().Milliseconds, Is.EqualTo(1.0));
}
```

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter SystemAggregationTests -v minimal`

Expected: FAIL with missing aggregate types.

- [ ] **Step 3: Implement profiler-marker-first system timing**

For every discovered system, try the Unity/Entities system marker first. Only when no usable marker exists may Deep Capture attach managed-boundary timing, and that result must be marked `Managed`.

Never call `Complete()` or otherwise synchronize jobs solely for profiling.

- [ ] **Step 4: Implement mod grouping without false cost transfer**

Direct mod totals include only systems defined by the mod's assembly. Patched vanilla systems are linked to patch owners in metadata but remain charged to the vanilla system. Shared libraries stay separate unless a direct system belongs to that assembly.

- [ ] **Step 5: Run tests/build and commit**

Run pure tests and a Windows CS2 build.

```bash
git add src tests
git commit -m "feat: aggregate system timings with honest attribution"
```

---

### Task 9: Capture/report model and privacy-safe JSON export

**Files:**
- Create: `src/CS2RuntimeProfiler/Export/PerformanceReport.cs`
- Create: `src/CS2RuntimeProfiler/Export/PrivacySanitizer.cs`
- Create: `src/CS2RuntimeProfiler/Export/ReportExporter.cs`
- Create: `tests/CS2RuntimeProfiler.Tests/PrivacySanitizerTests.cs`
- Modify: `tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj` to link safe export sources.

**Interfaces:**
- Consumes: finalized `CaptureSession`, runtime version/mod metadata, capability diagnostics.
- Produces: `ReportExporter.Export(CaptureSession, path)` and schema-versioned JSON matching the UI data model.

- [ ] **Step 1: Write failing privacy tests**

```csharp
[Test]
public void Sanitizer_removes_windows_user_paths_and_user_name()
{
    var text = @"Failure at C:\Users\Alice\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\Profiler";
    var sanitized = PrivacySanitizer.Sanitize(text);

    Assert.That(sanitized, Does.Not.Contain("Alice"));
    Assert.That(sanitized, Does.Not.Contain(@"C:\Users\"));
    Assert.That(sanitized, Does.Contain("<user-path>"));
}

[Test]
public void Export_model_does_not_include_city_name_by_default()
{
    var report = PerformanceReport.CreateForTest();
    Assert.That(report.CityName, Is.Null);
}
```

This pins the Review Focus privacy requirement.

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj --filter PrivacySanitizerTests -v minimal`

Expected: FAIL because export types are missing.

- [ ] **Step 3: Implement schema-versioned report serialization**

The report contains at minimum:

```text
schemaVersion
gameVersion
profilerVersion
hardwareSummary
enabledMods
captureConfig
capabilities
globalMetrics
systems
modAttribution
pathfinding
domainMetrics
timeline
profilerOverhead
warnings
```

Use a stable serializer supported by the CS2 runtime/toolchain. Before serialization, sanitize diagnostic/error strings and strip absolute local paths. City name remains null/not emitted by default.

- [ ] **Step 4: Write export to the mod data directory safely**

Use `EnvPath.kUserDataPath/ModsData/CS2RuntimeProfiler/` and a filename like `CS2Profiler-report-2026-09-24_114231.json`. I/O errors return a UI-visible failure result and log the exception; they do not propagate into simulation systems.

- [ ] **Step 5: Run tests/build and commit**

Run pure tests and a Windows CS2 build.

```bash
git add src tests
git commit -m "feat: export privacy-safe profiling reports"
```

---

### Task 10: C# UI snapshot/binding layer

**Files:**
- Create: `src/CS2RuntimeProfiler/UI/UiSnapshot.cs`
- Create: `src/CS2RuntimeProfiler/UI/UiSnapshotBuilder.cs`
- Create: `src/CS2RuntimeProfiler/UI/ProfilerUISystem.cs`
- Modify: `src/CS2RuntimeProfiler/Mod.cs`

**Interfaces:**
- Consumes: current Normal-mode metrics, current/finalized captures, system catalog, diagnostics, report exporter.
- Produces: one immutable UI snapshot binding plus trigger bindings for open/close, manual capture, capture selection, and report export.

- [ ] **Step 1: Define the UI DTO as a stable contract**

`UiSnapshot` must include:

```csharp
public sealed class UiSnapshot
{
    public GlobalUiMetrics Global { get; init; }
    public CaptureUiState Capture { get; init; }
    public IReadOnlyList<SystemUiRow> Systems { get; init; }
    public IReadOnlyList<ModUiRow> Mods { get; init; }
    public PathfindingUiMetrics Pathfinding { get; init; }
    public IReadOnlyList<TimelinePoint> Timeline { get; init; }
    public IReadOnlyList<CaptureSummaryUi> Captures { get; init; }
    public DiagnosticsUi Diagnostics { get; init; }
}
```

Use fields/properties supported by the chosen CS2 binding serializer; if init-only properties are unsupported by the runtime serializer, use constructor/read-only properties without changing semantics.

- [ ] **Step 2: Implement snapshot building away from per-frame hot paths**

`UiSnapshotBuilder` reads already-collected data only. It does not start reflection scans or heavy profiler operations. Build/update the UI snapshot on a throttled cadence (initially about 2 Hz) and when the user explicitly opens/clicks detailed views.

- [ ] **Step 3: Implement `ProfilerUISystem` bindings**

Expose:

- current snapshot;
- panel visibility;
- manual capture trigger;
- selected capture/system/mod identifiers;
- export report trigger/result.

Use `UISystemBase` and `Colossal.UI.Binding`; keep `OnUpdate` cheap.

- [ ] **Step 4: Build and commit**

Run the Windows CS2 build. Expected: C# binding layer compiles with no simulation mutation.

```bash
git add src/CS2RuntimeProfiler/UI src/CS2RuntimeProfiler/Mod.cs
git commit -m "feat: expose profiler snapshots to the UI"
```

---

### Task 11: React UI shell, compact HUD, and Overview/System/Mods views

**Files:**
- Create: `UI/UI.esproj`
- Create: `UI/mod.json`
- Create: `UI/package.json`
- Create: `UI/tsconfig.json`
- Create: `UI/src/index.tsx`
- Create: `UI/src/profiler/bindings.ts`
- Create: `UI/src/profiler/format.ts`
- Create: `UI/src/profiler/profiler.module.scss`
- Create: `UI/src/profiler/ProfilerRoot.tsx`
- Create: `UI/src/profiler/components/ProfilerHud.tsx`
- Create: `UI/src/profiler/components/MetricBadge.tsx`
- Create: `UI/src/profiler/tabs/OverviewTab.tsx`
- Create: `UI/src/profiler/tabs/SystemsTab.tsx`
- Create: `UI/src/profiler/tabs/ModsTab.tsx`

**Interfaces:**
- Consumes: `ProfilerUISystem` bindings from Task 10.
- Produces: game HUD entry and dedicated profiler panel with Overview, Systems, and Mods tabs.

- [ ] **Step 1: Bootstrap the official CS2 UI project**

Use the same dependency/tooling family as a current working CS2 UI mod: `cs2/modding`, React/TypeScript, SCSS modules, and the official UI build scripts. `npm run build` must produce the bundle consumed by the mod project.

- [ ] **Step 2: Register a compact HUD/panel entry using `moduleRegistry`**

`index.tsx` uses `ModRegistrar` and extends/appends an appropriate stable vanilla UI slot. Keep the integration narrow: one HUD entry that opens a dedicated profiler panel rather than replacing major vanilla UI components.

- [ ] **Step 3: Implement metric formatting that preserves unavailable/confidence state**

`format.ts` must render unavailable values as `—`/`Unavailable`, never `0`. `MetricBadge` renders the confidence indicators `Full`, `Managed`, `Indirect`, or `Unavailable` with a tooltip explaining inclusion/exclusion.

- [ ] **Step 4: Implement Overview**

Display selected vs actual simulation speed, efficiency, FPS/frame timing, CPU main/render, GPU, pathfinding summary, capture state, and profiler overhead. Show an explicit high-load/deep-capture indicator and manual capture/export actions.

- [ ] **Step 5: Implement Systems and Mods**

Systems: sortable hierarchy/table with Current/Mean/P95/P99/Max/Calls/source/confidence and expandable patch/marker/cadence details.

Mods: direct assembly-owned system totals only, plus links/badges for patched vanilla systems; no "worst mod" wording or blame ranking.

- [ ] **Step 6: Build UI and C# project**

Run:

```bash
cd UI
npm ci
npm run build
```

Then on Windows:

```powershell
dotnet build .\src\CS2RuntimeProfiler\CS2RuntimeProfiler.csproj -c Debug
```

Expected: UI bundle and mod compile successfully.

- [ ] **Step 7: Commit**

```bash
git add UI src/CS2RuntimeProfiler/CS2RuntimeProfiler.csproj
git commit -m "feat: add profiler HUD and core runtime views"
```

---

### Task 12: Pathfinding, Timeline, Captures, Diagnostics, and Spike Inspector UI

**Files:**
- Create: `UI/src/profiler/tabs/PathfindingTab.tsx`
- Create: `UI/src/profiler/tabs/TimelineTab.tsx`
- Create: `UI/src/profiler/tabs/CapturesTab.tsx`
- Create: `UI/src/profiler/tabs/DiagnosticsTab.tsx`
- Modify: `UI/src/profiler/ProfilerRoot.tsx`
- Modify: `UI/src/profiler/profiler.module.scss`

**Interfaces:**
- Consumes: timeline/capture/diagnostic data in the Task 10 snapshot.
- Produces: the remaining diagnostic workflow, including pre-vs-spike comparison and coverage/overhead visibility.

- [ ] **Step 1: Implement Pathfinding view**

Display available queue/pending/throughput/delta metrics with history and confidence. Unsupported values show `Unavailable` plus the collector reason in a details/tooltip surface.

- [ ] **Step 2: Implement Timeline without introducing a heavy chart dependency**

Use a lightweight SVG/canvas implementation driven by bounded samples. Support toggling series for actual simulation speed, FPS, main thread, GPU, pathfinding, domain counts, GC, and selected system timing. Clicking/hovering a timestamp surfaces the matching snapshot/capture context.

- [ ] **Step 3: Implement Captures and Spike Inspector**

For each capture show trigger, duration, coverage, simultaneous/batched mode, profiler overhead, and strongest correlated changes. Comparison headings use evidence language such as `Strongest correlated changes`, never `Causes`.

- [ ] **Step 4: Implement Diagnostics**

Show game/profiler version, discovered/captured marker counts, system count, Harmony patch-map availability, collector states/failure reasons, batch size, coverage, and profiler self-overhead.

- [ ] **Step 5: Build and commit**

Run `npm run build`, then the Windows mod build.

```bash
git add UI
git commit -m "feat: add timeline capture and diagnostics views"
```

---

### Task 13: End-to-end runtime validation, performance guardrails, and documentation

**Files:**
- Create: `README.md`
- Create: `docs/validation/runtime-validation.md`
- Modify: any implementation files only when a validation result demonstrates a concrete defect.

**Interfaces:**
- Consumes: complete mod and the user's real modded city benchmark.
- Produces: documented, repeatable runtime validation evidence and installation/use instructions.

- [ ] **Step 1: Document the validation matrix before running it**

`docs/validation/runtime-validation.md` must contain these exact scenarios and columns for observed results:

1. Profiler disabled.
2. Normal monitoring in a stable vanilla/new city.
3. Normal monitoring in the user's approximately 40k modded city.
4. Manual Deep Capture.
5. Automatic Deep Capture during selected-4x / reduced-actual-speed condition.
6. Large marker set / batched coverage.
7. Collector failure or deliberately unavailable member.
8. Mod-owned ECS system classification.
9. Patched vanilla system metadata.
10. JSON export/privacy inspection.

Record selected speed, actual speed, FPS, profiler overhead, capture coverage, errors/warnings, and pass/fail for each case.

- [ ] **Step 2: Establish Normal-mode baseline**

Measure the same camera position/save for at least 60 seconds with profiler disabled and then Normal mode enabled. The target is under 1% CPU overhead and under 2% simulation-speed impact where practical. If the target is missed, profile the profiler before changing game-facing features.

- [ ] **Step 3: Validate Deep Capture truthfulness**

Trigger a manual capture and confirm:

- every discovered safe category is attempted;
- marker batching/coverage matches Diagnostics;
- profiler overhead is visible;
- managed-only systems are not shown as Full;
- unavailable counters are not rendered as zero;
- no forced Job completion or gameplay-state mutation occurs.

- [ ] **Step 4: Validate the user's slowdown case**

On the approximately 40k modded city, reproduce the intermittent reduced actual simulation speed if possible. Confirm the capture records pre-buffer, Deep Capture, and post-buffer and surfaces strongest correlated changes without automatically naming a cause.

- [ ] **Step 5: Validate privacy/export manually**

Search the generated JSON for `C:\\Users\\`, the Windows account name, and absolute `ModsData` paths. Expected: none are present. Confirm UI values for the selected capture match serialized values.

- [ ] **Step 6: Write README**

README must explain:

- purpose and non-goals;
- Normal vs Deep Capture;
- confidence labels;
- how to interpret Mods/System timing;
- why Burst/Job cost can remain unattributed;
- automatic trigger defaults;
- export/report location;
- privacy behavior;
- known limitations;
- build requirement that users/developers supply their own CS2 installation/toolchain and that game DLLs are not in the repository.

- [ ] **Step 7: Run final verification**

Run pure tests:

```bash
dotnet test tests/CS2RuntimeProfiler.Tests/CS2RuntimeProfiler.Tests.csproj -v minimal
```

Run UI build:

```bash
cd UI && npm ci && npm run build
```

Run Windows CS2 mod build:

```powershell
dotnet build .\src\CS2RuntimeProfiler\CS2RuntimeProfiler.csproj -c Release
```

Expected: all pure tests pass, UI build succeeds, mod release build succeeds, and runtime validation document contains no unexplained failures.

- [ ] **Step 8: Commit**

```bash
git add README.md docs src UI tests
git commit -m "docs: add runtime profiler validation and usage guide"
```

---

## Plan self-review results

- **Spec coverage:** Every major design section is assigned: low-overhead monitoring (Tasks 2/5), automatic/manual Deep Capture (Tasks 3/6), maximum safe coverage and batching (Task 6), ECS discovery/attribution/patch metadata (Tasks 4/8), Pathfinding/domain collectors (Task 7), confidence and unattributed jobs (Tasks 2/8), self-overhead (Tasks 5/6), JSON/privacy export (Task 9), full UI workflow (Tasks 10–12), fail-open compatibility and real runtime validation (Tasks 7/13).
- **Placeholder scan:** No implementation step relies on `TBD`, `TODO`, "similar to", or unspecified generic error handling. Runtime-only unknowns are explicitly represented as capability discovery / `Unavailable`, matching the design rather than postponing work.
- **Type consistency:** Core naming used by later tasks is established before consumption: `MetricConfidence`, `MetricAvailability`, `MetricSample`, `SimulationEfficiency`, `DeepCaptureStateMachine`, `SystemDescriptor`, `CaptureSession`, and UI snapshot types are introduced in dependency order.
- **Review Focus coverage:** paused simulation (Task 2/3), huge marker sets (Task 6), reflection/missing members (Task 4/7), patched vanilla attribution (Task 4/8), and path/privacy leakage (Task 9/13) all have explicit tests or validation steps.
