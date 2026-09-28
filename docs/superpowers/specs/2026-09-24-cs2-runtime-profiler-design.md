# CS2 Runtime Profiler — Design Specification

Date: 2026-09-24
Status: Approved design, pending written-spec review
Target game: Cities: Skylines II 1.6.2-era runtime, with compatibility-oriented discovery for later versions
Repository: `pengin0503/CS2-Runtime-Profiler`

## 1. Purpose

CS2 Runtime Profiler is a read-mostly diagnostic mod for Cities: Skylines II. Its purpose is not to automatically optimize or alter the simulation. It makes runtime cost visible so the player can determine where simulation time is being spent and decide what action to take.

The mod should answer questions such as:

- Why is selected simulation speed 4.0x while actual speed is only 2.5x?
- Is the slowdown primarily simulation, rendering, pathfinding, memory/GC, or another runtime subsystem?
- Which ECS systems become expensive during a slowdown?
- Which loaded mod assembly owns a measured system?
- Is a heavy vanilla system patched by one or more mods?
- Did a pathfinding queue, entity population, or another counter rise before the slowdown?
- How trustworthy is each displayed timing value?
- How much overhead is the profiler itself adding?

The UI must emphasize observation and evidence. It may show correlated changes, but must not label a mod or subsystem as the cause when the captured data does not prove causation.

## 2. Non-goals

Version 1 will not:

- automatically throttle citizen AI, tourism, traffic, pathfinding, or other simulation systems;
- change simulation settings to recover performance;
- disable or reconfigure third-party mods;
- assign unattributable Burst/Job worker time to a mod by guesswork;
- treat higher CPU time as evidence that a mod is defective;
- commit or redistribute Cities: Skylines II game DLLs.

Automatic optimization can be considered separately in the future only after the profiler provides reliable evidence about real bottlenecks.

## 3. Success criteria

The first usable release is successful when it can:

1. Show selected and actual simulation speed together with an efficiency ratio.
2. Continuously collect low-overhead global health metrics during ordinary play.
3. Detect sustained high-load periods and automatically start a deeper capture.
4. Allow manual deep capture at any time.
5. Discover and profile as much of the current runtime as the game exposes, rather than relying only on a hard-coded list.
6. Enumerate vanilla and mod-added ECS systems and attribute them to assemblies/mods where possible.
7. Distinguish direct measurement, managed-only measurement, indirect observation, and unavailable metrics.
8. Provide timeline and spike views that show what changed before and during a slowdown.
9. Display profiler self-overhead so the player can judge measurement distortion.
10. Export a machine-readable report containing the same evidence shown in the UI.
11. Fail open when a collector no longer works after a game update: one failed metric must not disable the entire profiler.

## 4. Core design principles

### 4.1 Visibility over intervention

The profiler reads and records runtime information. It does not modify gameplay state as part of normal profiling.

### 4.2 Evidence over diagnosis labels

The UI says, for example, "pending pathfinding requests increased from 14 to 317 while simulation efficiency fell from 98% to 63%." It does not say "Tourism Overhaul caused the slowdown" unless a future measurement mechanism can establish that directly.

### 4.3 Honest attribution

Unattributed worker-thread/Burst cost remains unattributed. A plausible number is worse than a missing number.

### 4.4 Low idle overhead, maximum useful coverage under load

Normal monitoring is deliberately shallow and inexpensive. When a high-load event occurs, deep capture expands to every runtime metric, marker, ECS system, queue, and counter that can safely be observed in the current game build.

### 4.5 Runtime capability discovery

The profiler discovers available profiler markers, ECS systems, loaded assemblies, patch metadata, and collector capabilities at runtime. Hard-coded adapters are used only where CS2-specific internal counters require them.

### 4.6 Collector isolation

Collectors fail independently. An unavailable private field in a future game version should produce an `Unavailable` metric with an explanation, not an exception that breaks the profiler.

## 5. Runtime architecture

The runtime is divided into six logical layers:

1. **Capability Discovery** — discovers systems, markers, assemblies, patches, and supported counters.
2. **Collectors** — acquire global, ECS, pathfinding, entity, memory, render, and other metrics.
3. **Profiler Engine** — schedules recorders, handles sampling, aggregation, deep-capture multiplexing, and profiler-overhead measurement.
4. **Attribution** — maps systems to assemblies/mods and records Harmony patch owners when visible.
5. **Capture Store** — keeps bounded time-series history and completed high-load captures.
6. **UI / Export** — presents and exports the same captured model.

The implementation should keep these layers independent so a game update that breaks one collector does not force broad rewrites.

## 6. Operating modes

### 6.1 Normal monitoring

Normal mode should be active during ordinary play and target very low overhead.

Initial cadence targets:

- selected/actual simulation speed: 2–4 Hz;
- global CPU/GPU/frame metrics: approximately 2 Hz;
- domain/entity/pathfinding counters: approximately 1–2 Hz where safe;
- full system timing: disabled;
- full reflection scans: disabled;
- full marker recording: disabled.

History is stored in bounded ring buffers. The initial target is 120 seconds of lightweight history. Long play sessions must not cause unbounded memory growth.

Target overhead for Normal mode:

- CPU overhead: under 1% where practical;
- simulation-speed impact: under 2% where practical.

These are engineering targets, not claims to be shown until verified in-game.

### 6.2 Deep capture

Deep capture activates either manually or when a high-load trigger remains true for a configured duration.

Default initial trigger:

`actualSimulationSpeed / selectedSimulationSpeed < 0.80` for 2 seconds.

The trigger system may later support additional conditions such as main-thread spikes, pathfinding-queue growth, GC spikes, and frame-time spikes.

A capture has three time regions:

- pre-buffer: 5 seconds from Normal-mode history;
- deep capture: initial target 10 seconds;
- post-buffer: 5 seconds of follow-up lightweight history.

A cooldown, initially around 30 seconds, prevents repeated captures from continuously profiling an already-slow city.

### 6.3 Maximum observable coverage during Deep capture

When Deep capture starts, the profiler attempts to observe every category available in the current environment:

- Unity profiler markers/counters;
- discovered ECS systems;
- mod-added systems;
- selected/actual simulation speed;
- main-thread and render-thread timing;
- GPU timing where exposed;
- memory and GC metrics;
- pathfinding queues and throughput where exposed;
- citizen, household, tourist, vehicle, transport, cargo, traffic, and other useful entity/domain counters where available;
- Harmony patch ownership where inspectable;
- profiler self-overhead.

Coverage must be reported explicitly. A Deep capture can have 100% discovered-marker coverage even when all markers cannot be measured in the same frame.

## 7. Measurement sources and confidence model

The profiler uses the highest-confidence source available for each metric.

### 7.1 Preferred source order

1. **Unity `ProfilerRecorder` / runtime profiler markers**
2. **ECS system profiler markers provided by Unity Entities / the game**
3. **Managed system instrumentation for otherwise unmeasured C# execution**
4. **Indirect queue/entity/counter observations**

Harmony instrumentation is a fallback for managed boundaries, not the default measurement strategy.

### 7.2 Confidence labels

Every timing or derived cost that can be misunderstood must carry a measurement-quality label:

- **Full** — the relevant execution is directly represented by the captured marker/counter with high confidence.
- **Managed** — the managed system body is measured, but scheduled Job/Burst work may execute elsewhere and is not fully included.
- **Indirect** — related load is visible through queues, entity counts, throughput, or other supporting counters rather than direct execution timing.
- **Unavailable** — the current game build does not expose or safely provide the metric.

The UI must never silently render `Unavailable` as zero.

## 8. ECS System Catalog

A System Catalog is built from loaded assemblies during initialization and refreshed only on explicit or relevant lifecycle events, never every frame.

Each catalog entry should contain, where discoverable:

- full type name;
- namespace;
- assembly name;
- source classification: vanilla, mod, shared library, unknown;
- mod identity/version when resolvable;
- enabled state;
- system update phase/group;
- update interval and offset where available;
- matching profiler marker(s);
- measurement capabilities;
- runtime patch owners;
- collector/category tags such as Pathfinding, Tourism, Traffic, UI, Rendering, Economy.

Reflection scans must guard per-assembly type enumeration failures and must not run uncapped on the simulation thread during ordinary play.

## 9. System and mod attribution

### 9.1 Direct system ownership

A system type is attributed first to its defining assembly. If the assembly can be mapped to a loaded CS2 mod, the UI shows that mod as the direct owner.

Example:

`TourismOverhaul.SomeSystem -> TourismOverhaul.dll -> Tourism Overhaul`

### 9.2 Vanilla systems patched by mods

A heavy vanilla system may include Harmony patches from third-party mods. In this case, the total system time is still displayed under the vanilla system, accompanied by patch-owner metadata.

Example:

- System: `TrafficLightSystem`
- Source: `Game.dll` / Vanilla
- Runtime patches: `Traffic Lights Enhancement`, another patch owner
- Warning: exact patch contribution cannot be separated from the total unless a direct marker exists.

The profiler must not assign the entire vanilla-system time to a patch owner.

### 9.3 Shared libraries

Shared libraries such as ExtraLib are shown as their own assembly/source. Their direct measured systems may be reported, but their CPU cost is not arbitrarily divided among dependent mods.

## 10. Jobs and Burst

The design assumes that managed system timing does not necessarily represent total system cost.

A common execution pattern is:

`System.OnUpdate -> Schedule Job -> managed method returns -> Burst job executes on worker thread`

If the job can be associated through profiler markers, both managed and job time may be shown. If not, the system receives a `Managed` confidence label and worker cost remains in an `Unattributed Jobs/Burst` category.

The profiler must not force completion of jobs merely to make attribution easier; doing so would change scheduling behavior and distort the workload being measured.

## 11. Deep-capture marker multiplexing

If the environment exposes more profiler markers than can be recorded simultaneously at acceptable overhead, Deep capture uses batched marker multiplexing.

Example:

- second 0–1: markers 1–150;
- second 1–2: markers 151–300;
- second 2–3: markers 301–450;
- second 3–4: markers 451–600.

The result must report:

- discovered marker count;
- captured marker count;
- overall coverage percentage;
- whether collection was simultaneous or batched;
- limitations on same-frame correlation.

Coverage is reduced only after sampling/multiplexing controls are exhausted or a marker is genuinely unavailable.

## 12. Profiler self-overhead

The profiler records its own work separately.

At minimum the UI shows:

- Normal-mode profiler time;
- Deep-capture profiler time;
- profiler share of the observed frame/update budget;
- current collection mode.

Deep capture should target an initial profiler-overhead ceiling near 8%. This is a tuning target to validate in-game, not a guaranteed threshold.

When overhead approaches the configured ceiling, the system should degrade in this order:

1. retain all categories but batch markers;
2. reduce sample frequency;
3. warn that the measurement itself is materially affecting the workload.

The profiler must not hide its own overhead from exported reports.

## 13. CS2-specific collectors

### 13.1 Global Metrics Collector

Collects, where available:

- selected simulation speed;
- actual/smoothed simulation speed;
- simulation efficiency ratio;
- FPS/frame time;
- CPU main-thread time;
- render-thread time;
- GPU time;
- process/game memory;
- GC activity;
- profiler self-overhead.

### 13.2 System Profiler Collector

Records discovered ECS-system timings and call/update frequency. Aggregates include:

- current;
- mean;
- median;
- P95;
- P99;
- maximum;
- total sampled time;
- calls/update count;
- share of measured CPU when meaningful;
- confidence label.

### 13.3 Pathfinding Collector

The supplied current `Game.dll` exposes relevant types including `Game.Pathfind.PathfindQueueSystem`, `Game.Simulation.PathfindSetupSystem`, and `Game.Pathfind.PathfindResultSystem`.

The collector should expose only counters that can be safely validated in the current build, such as where available:

- pending requests;
- queued requests;
- processed/completed requests;
- requests/sec;
- results/sec;
- queue delta/sec;
- wait/latency information if safely exposed.

Private/internal access is version-adapted and optional. Missing members produce `Unavailable`, not zero and not a fatal exception.

### 13.4 Entity/Domain Collectors

Where inexpensive and safe, collect counts/trends for useful domains such as:

- citizens;
- households;
- tourists;
- vehicles;
- public transport;
- cargo;
- service vehicles;
- relevant simulation entities.

These counters are supporting context. Entity growth is not automatically treated as a cause of CPU growth.

## 14. User interface

The profiler uses the standard CS2 C# binding plus React/TypeScript UI pattern and provides a compact HUD plus a dedicated large profiler panel.

### 14.1 Compact HUD

Example:

- `SIM 2.53x / 4.00x`
- `FPS 47`
- `CPU 24ms`
- `PF 317`
- capture state indicator

The HUD is intended for continuous awareness without keeping the profiler panel open.

### 14.2 Main tabs

#### Overview

Shows current runtime health:

- selected vs actual simulation speed;
- simulation efficiency;
- FPS;
- main/render/GPU timing;
- profiler overhead;
- high-level pressure categories;
- current monitoring/capture state;
- recent performance events.

#### Systems

Hierarchical and sortable view of discovered systems, with timing distribution, calls, source, category, and confidence. System rows can expand to show marker details, update phase/cadence, patch metadata, and related counters.

#### Mods

Groups directly owned systems by assembly/mod. Shows measured direct system cost and peak/percentile information without presenting a "bad mod" ranking. Patched vanilla systems are linked rather than fully charged to the mod.

#### Pathfinding

Dedicated queue/throughput/trend page for pathfinding-related information because pathfinding is a major CS2 diagnostic domain.

#### Timeline

Plots synchronized histories such as:

- actual simulation speed;
- FPS;
- main-thread time;
- GPU time;
- pathfinding pending/throughput;
- selected entity counts;
- GC/allocation activity;
- selected system timings.

Selecting a point on the timeline opens the corresponding snapshot/event.

#### Captures

Lists automatic and manual capture sessions, their duration, trigger, coverage, overhead, and strongest observed changes.

#### Diagnostics

Developer-oriented visibility into:

- game/mod profiler version;
- discovered markers;
- marker availability;
- detected systems;
- Harmony patch map;
- collector capability/failure state;
- unsupported/internal fields;
- current sampling/multiplexing mode;
- profiler self-overhead.

## 15. Spike Inspector

A completed performance event compares the pre-event baseline to the slowdown interval and displays the largest observed changes.

Example evidence:

- simulation: `4.00x -> 2.47x`;
- pending pathfinding: `14 -> 317`;
- tourist entities: `8,421 -> 11,903`;
- Pathfinding group CPU: `3.2ms -> 9.7ms`;
- a mod-owned managed system: `1.8ms -> 4.6ms (Managed)`;
- main thread: `14.1ms -> 25.8ms`.

The heading is "Strongest correlated changes" or equivalent, not "Causes".

## 16. Data model

A capture is represented as an independent `CaptureSession` containing logically separate datasets:

- metadata;
- trigger information;
- capability map;
- global samples;
- system samples;
- mod/assembly attribution;
- profiler-marker samples;
- entity/domain samples;
- pathfinding samples;
- patch map;
- profiler-overhead samples;
- warnings and diagnostics.

The UI and exporter consume the same model so exported values cannot silently diverge from on-screen values.

Normal-mode ring buffers and capture storage must be bounded. Capture retention limits should be configurable later; version 1 may use a conservative fixed cap if necessary.

## 17. Export format and privacy

Version 1 uses JSON as the primary export format.

Suggested naming:

`CS2Profiler-report-YYYY-MM-DD_HHMMSS.json`

Schema includes:

- `schemaVersion`;
- game version;
- profiler version;
- non-sensitive hardware summary when available;
- enabled mods and versions where resolvable;
- capture configuration;
- capability map;
- global metrics;
- systems;
- mod/assembly attribution;
- pathfinding/domain metrics;
- timeline samples;
- profiler overhead;
- warnings.

Exports must exclude unnecessary personal data such as Windows account names and absolute user paths. City name is excluded by default unless a later explicit setting is added.

## 18. Compatibility and game updates

CS2-specific private/internal access is isolated behind adapters/collectors rather than spread throughout the project.

Example conceptual layout:

- generic pathfinding metrics interface;
- `PathfindingCollector` generic layer;
- current 1.6.x adapter for verified internal members.

If a game update removes or renames a field:

- that metric becomes unavailable;
- the Diagnostics page records the reason;
- the rest of the profiler continues running.

Runtime discovery should reduce unnecessary hard-coded version checks, but private/internal collectors must remain conservative.

## 19. Error handling

Each collector and recorder operation must be failure-isolated.

Rules:

- no uncaught collector exception may escape into the simulation loop;
- reflection type enumeration handles per-assembly failures;
- missing private members are normal compatibility failures, not fatal errors;
- file/export I/O errors are reported to the user and logs without affecting simulation;
- recorder creation failures mark that metric unavailable and continue;
- Deep capture may reduce cadence/batching if profiling overhead becomes excessive.

## 20. Testing strategy

### 20.1 Unit/logic tests

Where game-runtime dependencies can be abstracted, test:

- percentile/aggregation calculations;
- ring-buffer bounds;
- trigger hysteresis/cooldown;
- capture state transitions;
- capability-state merging;
- assembly/system attribution rules;
- report serialization;
- marker batching/multiplexing;
- confidence-label propagation.

### 20.2 Integration/runtime tests

Required scenarios:

1. **Profiler disabled** — no gameplay intervention and no scheduled expensive work.
2. **Normal mode** — low overhead under a stable city.
3. **Manual Deep capture** — all discovered safe categories are attempted.
4. **Automatic Deep capture** — sustained efficiency loss triggers one capture and observes cooldown.
5. **Large marker set** — automatically switches to batched/multiplexed capture while reporting coverage.
6. **Collector failure** — one broken collector does not stop the others.
7. **Missing internal member** — produces `Unavailable` and diagnostic reason.
8. **Vanilla city** — vanilla systems classify correctly.
9. **Modded city** — mod-defined systems classify by assembly/mod where possible.
10. **Patched vanilla system** — patch-owner metadata is visible without falsely reallocating total runtime.
11. **Export** — exported capture values match the UI model.
12. **Profiler overhead** — Normal and Deep modes both report their own measurement cost.

The user's existing approximately 40k-population modded city is a useful real-world benchmark for later in-game validation, especially because it exhibits intermittent selected-4x/actual-approximately-2.5x behavior.

## 21. Repository and build policy

Cities: Skylines II game binaries supplied for development are local build references only. They must not be committed to this repository.

The project should reference the installed game's Managed directory through the official CS2 toolchain/build properties where practical. Local development references include the supplied current-version `Game.dll`, `Unity.Entities.dll`, Unity/Colossal dependencies, and `Mod.props`/`Mod.targets`, but the repository contains only source, configuration, documentation, and build scripts that do not redistribute proprietary game files.

Expected source layout:

```text
CS2RuntimeProfiler/
  Mod.cs
  Setting.cs
  Profiling/
    ProfilerCatalog.cs
    RecorderManager.cs
    SystemProfiler.cs
    DeepCaptureController.cs
  Collectors/
    GlobalMetricsCollector.cs
    EntityMetricsCollector.cs
    PathfindingCollector.cs
    MemoryCollector.cs
  Attribution/
    AssemblyAttributor.cs
    ModAttributor.cs
    HarmonyPatchInspector.cs
  Data/
    CaptureSession.cs
    MetricSample.cs
    SystemMetrics.cs
    CapabilityInfo.cs
  Export/
    ReportExporter.cs
  UI/
    ProfilerUISystem.cs
UI/
  src/
    Overview/
    Systems/
    Mods/
    Pathfinding/
    Timeline/
    Captures/
    Diagnostics/
```

Exact filenames may be refined during implementation if the responsibility boundaries remain intact.

## 22. Version 1 boundary

Version 1 prioritizes trustworthy runtime visibility over automatic remediation. The implementation order should establish reliable capability discovery, low-overhead global monitoring, capture infrastructure, and system/assembly attribution before adding domain-specific private-field collectors.

A feature is not considered complete merely because it renders a value. For every metric, the implementation must know:

- where the value came from;
- how often it was sampled;
- what execution it includes or excludes;
- its confidence level;
- whether profiler overhead could materially distort it.

That requirement is the defining quality bar for CS2 Runtime Profiler.
