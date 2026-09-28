# Runtime Capture Diagnostics Hardening Design

## Context

The 2026-09-27 runtime report shows repeated Deep Capture sessions with hundreds of discovered `TimeNanoseconds` markers but zero system-marker matches. The same report also exposes ambiguous coverage semantics, insufficient trigger audit data, and profiler-memory growth that is not represented by the current CPU-only overhead guard.

## Goals

- Restore reliable per-system timing attribution without guessing marker names.
- Preserve conservative attribution: ambiguous or unverified mappings remain unattributed.
- Split marker discovery/attempt/sample coverage so exported ratios describe what they actually measure.
- Make automatic-capture triggers auditable after export by retaining selected speed, actual speed, and efficiency at trigger time.
- Track profiler-memory growth during capture and warn/degrade when it becomes excessive.
- Export effective runtime capture settings and emit concise completion diagnostics to the mod log.

## Non-goals

- Do not change gameplay systems or simulation state.
- Do not add heuristic short-name matching between profiler markers and ECS systems.
- Do not redesign unrelated UI or profiling features.
- Do not assign worker/job time to a system without direct evidence.

## System marker identity

`SystemDescriptor` will carry an optional exact profiler marker name in addition to the CLR full type name. Runtime discovery should derive that exact name from the live Unity Entities world/system identity when possible. `SystemMarkerTimingProjector` must prefer this exact runtime marker identity. Legacy descriptors without an exact marker name may retain the existing exact/full-name behavior for tests and compatibility, but no new fuzzy/short-name matching is introduced.

The runtime catalog must only attach a marker name when it can be obtained from a live ECS system. If a type exists in loaded assemblies but has no live system handle, it remains catalogued for ownership metadata but is not projected into timing by inference.

## Coverage model

A capture distinguishes:

- `Discovered`: marker catalog size at capture start.
- `Attempted`: unique marker IDs for which recorder activation was attempted.
- `Activated`: unique marker IDs successfully activated.
- `Sampled`: unique marker IDs that produced at least one usable sample.

The historical `Captured` value remains a compatibility alias for `Sampled`. Existing `coverageRatio` remains the sampled/discovered ratio for schema compatibility, while exports add explicit attempted/activated/sampled ratios so consumers no longer need to infer its meaning.

## Trigger snapshot

Each `CaptureSession` retains a trigger snapshot containing selected speed, actual speed, and efficiency when capture begins. Manual capture may omit this snapshot when no current global sample exists. The timeline also includes `selectedSpeed` alongside `actualSpeed` and `efficiency`.

## Profiler-memory guard

During Deep Capture, the runtime reads the existing Unity profiler-memory recorder when available. The session records baseline and peak profiler-used bytes. A configurable fixed safety threshold is intentionally avoided in this change; instead, the controller warns when profiler-used memory grows by at least 128 MiB during one capture and applies the same degradation mechanism used for repeated CPU-overhead breaches. If the metric is unavailable, capture behavior is unchanged.

## Export and logging

Report capture configuration includes effective monitoring/capture settings: sampling period, automatic capture flag, efficiency threshold, sustain/pre/deep/post/cooldown durations, max concurrent markers, CPU overhead limit, and max completed captures.

Each completed capture emits one concise informational log line containing capture id, trigger, discovered/attempted/activated/sampled counts, maximum CPU overhead share, profiler-memory delta when known, system timing count, and warning count. No per-sample logging is added.

## Compatibility

- Preserve schema version 2 and all existing report fields.
- Additive fields are allowed.
- Existing constructors/tests that do not supply runtime marker names continue to work.
- Existing consumers of `Captured`/`coverageRatio` keep sampled semantics.

## Verification

Add regression tests for exact runtime marker identity, coverage counters, trigger snapshot/timeline export, profiler-memory growth warnings/degradation, effective configuration export, and capture completion log formatting. Run the full C# and UI test suites through the repository's existing GitHub Actions workflows.