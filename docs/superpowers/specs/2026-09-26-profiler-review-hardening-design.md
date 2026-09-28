# Profiler Review Hardening Design

Date: 2026-09-26
Branch: `main`

## Intent

Improve the current profiler without changing its read-only evidence policy. The goal is to reduce avoidable `Unavailable` results, make remaining unavailability diagnostically useful, and avoid reporting capability availability when all rows in a metric group are unavailable.

## Evidence and constraints

- The supplied current managed-reference archive contains `Game.dll` plus the Unity/Colossal assemblies used by the mod.
- `Game.dll` exposes the existing `PathfindQueueSystem` members already probed by the collector. Native/container-shaped fields can expose `Length` without implementing `ICollection`, so collection-count probing must accept the already verified `Length` pattern used for `m_PathfindActions.m_Items`.
- The supplied `Game.dll` contains these ECS component types: `Game.Vehicles.Ambulance`, `Hearse`, `MaintenanceVehicle`, `FireEngine`, `GarbageTruck`, `PoliceCar`, `PostVan`, and `PrisonerTransport`. They can form a read-only `Any` query for the existing `serviceVehicles` metric.
- `Game.dll` also contains pathfinding names such as `GetPathfindCompleted`, but the supplied binary evidence alone does not establish counter lifetime/reset semantics. Do not derive `requestsPerSecond` or `resultsPerSecond` from it until those semantics are verified in runtime/source evidence.
- Never represent unavailability as numeric zero.
- Do not create another branch; all work is committed directly to `main` as requested.

## Changes

### 1. Capability truthfulness

`ProfilerReportBuilder` must mark `pathfinding` and `domainMetrics` available only when at least one exported metric row has `Availability == "Available"`. A non-empty list made entirely of unavailable rows is not an available capability. `systemTiming` remains available only when at least one system timing row exists.

### 2. Pathfinding collection coverage

`PathfindingCollector.TryReadCollectionCount` must use the existing count/length probe rather than requiring `ICollection`. This preserves current collection behavior and additionally supports runtime containers that expose an integer `Length` property.

### 3. Service vehicle domain metric

Replace the hard-coded unavailable `serviceVehicles` row with a verified read-only aggregate query. `DomainMetricsSystem` registers one query that matches any of the verified service vehicle component types and excludes `Deleted` and `Temp`, exactly like the existing domain queries. `EntityMetricsCollector` reads it through the existing count-source abstraction and keeps `Indirect` confidence.

This is an aggregate entity count, not a sum of independent per-type counts, so an entity matching more than one component is counted once.

### 4. System timing failure diagnostics

When a capture produces zero system timing rows, the capture warning must identify the main projection stages rather than only giving total catalog/marker counts. At minimum report:

- catalog system count;
- total profiler marker count;
- `TimeNanoseconds` marker count;
- systems with exactly one full-name marker match;
- systems with ambiguous full-name matches;
- unique matches that had no captured samples;
- captured marker coverage.

Matching rules remain conservative: exact full type name or `<world> <full type name>`, never short-name guessing.

### 5. Documentation consistency

Update the README export filename example for millisecond timestamps/collision suffix behavior. Update runtime-validation notes that still describe the old shared-recorder lifecycle or the now-resolved npm audit state.

## Testing

Use TDD for each behavioral change:

- report capability test with unavailable-only metric rows;
- pathfinding test with a fake field exposing `Length` but not `ICollection`;
- entity collector test expecting `serviceVehicles` from the verified aggregate source key;
- timing finalizer test expecting detailed zero-result diagnostics;
- full Pure Core suite and UI suite/build after implementation.

Actual CS2 runtime behavior remains subject to in-game validation; static managed-reference inspection does not replace that step.
