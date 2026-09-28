# Profiler Correctness Hardening Design

Date: 2026-09-25
Branch: `main`

## Goal

Fix the remaining correctness, lifecycle, attribution, export, UI-state and installation-documentation issues identified in the 2026-09-25 audit without weakening the profiler's read-only / evidence-first design.

## Constraints

- Work directly on `main`; do not create another branch.
- Game/API verification uses the supplied Cities: Skylines II game/toolchain files.
- Missing profiler samples must remain unavailable; never convert missing evidence into a measured zero.
- Do not attribute vanilla system time to a patch owner or list vanilla assemblies as directly-owned mods.
- Monitoring OFF must stop profiler recorders and leave capture lifecycle coherent when monitoring is enabled again.
- Keep normal monitoring and UI overhead bounded.
- Preserve backward compatibility where practical; avoid broad unrelated refactors.

## Design

### 1. Recorder samples retain validity and call counts

`RecorderReading.Count <= 0` means no usable profiler sample was present for that read. Deep Capture must not add that marker to coverage or timing samples. `MetricSample` will carry an optional call-count value so Unity `ProfilerRecorder` count information survives through capture storage and system aggregation.

System timing statistics continue to aggregate timing values, while `Calls` becomes the sum of retained recorder sample counts rather than the number of profiler polling observations. If call counts are unavailable, `Calls` remains unavailable instead of being fabricated.

### 2. Monitoring OFF interrupts an active capture coherently

Disabling monitoring during Deep Capture deactivates recorders and explicitly interrupts/finalizes the partial capture with a warning. The capture state machine returns to Monitoring. Re-enabling monitoring restores only the normal recorder set; a new Deep Capture starts cleanly.

### 3. Preserve source kind into UI attribution

`SystemSourceKind` is carried from `SystemDescriptor` through `SystemTimingEntry` into `SystemUiRow`. The Mods projection includes direct system cost only for `SystemSourceKind.Mod`. Patch-owner metadata can still create a patch-only row, but vanilla/runtime/profiler direct cost is not shown as mod cost.

### 4. Export preserves visible measurement metadata

JSON export preserves metric units and the visible system statistics (current, mean, median, P95, P99, max, total and calls). Export continues to sanitize strings before writing.

### 5. Completed vs current capture is explicit

Completed capture summaries contain completed sessions only. The active capture may participate in live timeline/diagnostics but must not increment `completedCount` or appear in the completed-capture list.

Manual capture UI is enabled only in states where the state machine accepts the request: Monitoring and Cooldown.

### 6. Selection drives detail projection and limits timeline work

The selected capture ID is retained by the C# UI system. Systems/timeline projection prefers the selected completed capture; otherwise it uses the active capture where meaningful or the newest completed capture. Timeline projection is limited to the relevant capture rather than rebuilding all retained capture timelines every refresh.

### 7. Overhead diagnostics describe what is actually measured

Do not label CaptureRuntimeSystem timing as total profiler CPU overhead. Expose a more precise capture-orchestration overhead label/field unless a component-wide aggregate is available. Runtime validation remains authoritative for true end-to-end impact.

### 8. Toolchain documentation uses official variable names

README PowerShell instructions must use the variable names expected by the supplied `Mod.props` / `Mod.targets`, especially `CSII_MANAGEDPATH`. Documentation must not imply that a partial set of environment variables replaces installation/configuration of the official Modding Toolchain.

## Acceptance

- Empty recorder reads do not produce Full-confidence zero timing or increase captured marker coverage.
- System `Calls` represents recorder call counts when available.
- Monitoring OFF during Deep Capture cannot resume into a mixed normal/deep recorder state.
- Vanilla `Game` system timing does not appear as direct MOD cost.
- Export retains units and all supported system statistics.
- `completedCount` and Captures tab exclude the active capture.
- Capture selection affects systems/timeline detail and timeline projection no longer combines all 20 retained captures by default.
- Manual capture button matches state-machine acceptance.
- README uses official toolchain environment variable names.
- Pure Core Tests and UI Tests/build remain green; real in-game validation remains separately marked UNVERIFIED until run.