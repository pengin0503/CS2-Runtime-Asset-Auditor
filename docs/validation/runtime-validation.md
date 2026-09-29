# Runtime validation

Date: 2026-09-26  
Branch: `main`

This document separates automated evidence from checks that require a real Cities: Skylines II session. Unit tests, CI and static inspection do **not** convert an in-game scenario into a pass. If a scenario was not actually executed, its status remains **NOT RUN / UNVERIFIED**.

## Automated evidence recorded in this implementation session

| Check | Evidence | Result | Scope / limitation |
| --- | --- | --- | --- |
| Capture timing finalizer TDD RED | commit `50cee256924376934f976093c7a7da6b814d6246`, Actions run `35971528377` | Expected failure | Failed on missing `CaptureSystemTimingFinalizer` (`CS0103`), establishing the missing behavior before implementation. |
| Capture timing finalizer GREEN | commit `578104eb84789bf22ed6da0a46754c36c2d0c85a`, Actions run `35971630905` | PASS | Pure-layer projection/finalization test only. |
| Completed-capture one-shot processor TDD RED | commit `247043b0518daf7f011169ac83f7876ef92c9dfe`, Actions run `35971836820` | Expected failure | Failed on missing `CaptureCompletionTimingProcessor` (`CS0246`). |
| Completed-capture one-shot processor GREEN | commit `52352d03236a478d281b188767e2a7d0a87dfd78`, Actions run `35971922138` | PASS | Pure-layer lifecycle test only. |
| Runtime wiring commit Pure Core Tests | commit `5441378444f32bb25faf6971b40a6b77b9ea4ea6`, Actions run `35972029165` | PASS | Confirms the pure test suite remained green. The pure test project does not compile the Game-dependent runtime system, so this is not a full CS2 build result. |
| Latest UI-specific CI after Timeline/Captures/Diagnostics work | commit `0d933d55648c306cebe94f0aeb3d12a80347c010`, UI Tests run `35968390716` | PASS | Historical UI evidence from the original implementation session. |
| Full official Windows CS2 mod build | — | NOT RUN / UNVERIFIED | Not executed in this validation environment. A complete official Windows CS2 modding environment is required. |
| In-game runtime validation | — | NOT RUN / UNVERIFIED | No Cities: Skylines II process was available in this validation environment. |

## 2026-09-25 hardening evidence

| Check | Evidence | Result | Scope / limitation |
| --- | --- | --- | --- |
| Skipped-state capture finalization RED | commit `6c3f44f2bc5f4e17605d5ba52559c323894f1ec6`, Actions run `36017908590` | Expected failure | Reproduced `DeepCapture -> Cooldown` time jump leaving `CurrentSession` alive and no completed capture. |
| Skipped-state capture finalization GREEN | commit `e660ed38f90c9457a8bd5650e9116b837f025b01`, Actions run `36018048295` | PASS | Pure controller regression test verifies the session is finalized exactly once after the jump. |
| Monitoring lifecycle gate RED | commit `8dfd1abcc41ca4f5f35a410206e8b892970a231d`, Actions run `36018253472` | Expected failure | Missing lifecycle gate/types established the one-shot transition behavior before implementation. |
| Monitoring lifecycle GREEN | commits `f5eb9be7afb3e6d971138af0d65d287b1dc4fb4e`, `ef5c7fa54556981702306f9b8b6e9176bde9ab1e`; Actions run `36018414141` | PASS | Pure transition logic is covered. Game-system wiring is statically inspected but still requires in-game validation. |
| Narrow marker lookup RED | commit `a8f60a57a83abc42fa1653054e4b7c1b728bd3f8`, Actions run `36018556742` | Expected failure | Failed because `TryGetMarkerSamples` did not exist. |
| Narrow marker lookup / timing projection GREEN | commits `1e5972342db4691b1a68edfb763de7981eeb2ae2`, `6f70b002879d60aa795713d20a5c5d264a61f0e3`; Actions run `36018865062` | PASS | System timing projection no longer materializes the whole marker dictionary for each lookup. |
| Adaptive-overhead hysteresis RED | commit `e695ca848899e5d9c6e9eaa1161d8bb7360fe9b9`, Actions run `36019012978` | Expected failure | Demonstrated degradation outside capture, degradation after one spike, excessive reduction after three spikes, and persistence into the next capture. |
| Adaptive-overhead hysteresis GREEN | commit `b7386dd9dbefd74d948ce0b15ffddf5c799646bd`, Actions run `36019152958` | PASS | Degradation requires three consecutive over-ceiling observations during an active capture and resets for a new capture. |
| Capture-history eviction processor RED | commit `69bd8bde8448ec6d1288a7680ffb5de7f7c2e8d4`, Actions run `36019314362` | Expected failure | Established that index-only processing cannot represent front eviction safely. |
| Bounded completed history RED | commit `4ed753e58a8c0113970b0706429b0ba7ce9ce872`, Actions run `36019518819` | Expected failure | Controller did not yet expose a completed-history retention limit. |
| Bounded completed history GREEN | commits `f5313ee80f09b14c1ee8bf25b0bdd0551a7e180b`, `0be7cd16d2ff3dea429d51fd348823d4409cc277`, `f473a48bbc28dfc38bfd64eec02b6f20c4b5a225`; Actions run `36019647891` | PASS | Default completed capture retention is bounded to 20; timing processing tracks capture identity and remains correct after front eviction. |
| Lightweight HUD binding initial regression run | commit `9d9d52cfb28f0085686dd48da81ccb7b5c3b0f10`, UI run `36019966835` | Harness failure | The initial test could not resolve `cs2/api`; this run is not counted as a behavioral RED result. A dedicated `cs2/api` Vitest stub was added afterward. |
| Lightweight HUD / closed-panel refresh GREEN | commit `2f1ad4a7de474704613844ef0304acdd408ff28a`, UI run `36020955605`, Pure Core run `36020956448` | PASS | UI tests and webpack production build pass. Source-level regression checks lock the closed-panel HUD-only refresh and immediate full refresh on panel open. |
| Managed-reference API inspection | supplied `CS2-managed-reference-fix(1).zip` | PASS for referenced type presence | `Colossal.UI.Binding.dll` contains `RawValueBinding` and `IJsonWriter`; the hardening change introduces no new game-facing API family beyond APIs already used by the mod. This is not a substitute for a full Windows CS2 build. |

## 2026-09-25 correctness-hardening evidence

| Check | Evidence | Result | Scope / limitation |
| --- | --- | --- | --- |
| Recorder call-count / zero-read semantics RED | commit `d3ba42be46a51ba129a259db6d3a5ec705c63fa4`, Pure Core run `36085389204` | Expected failure | The test required explicit recorder call-count evidence before `MetricSample.CallCount` existed. Zero-count reads are also covered by controller regression tests. |
| Recorder call-count / zero-read semantics GREEN | commit `dd65db0b596ff2b0c9984a32dcfdf46729ac49b0`, Pure Core run `36085604215` | PASS | Deep Capture skips `RecorderReading.Count <= 0`; per-system `Calls` is derived from recorded call counts only and remains unavailable for sources without that evidence. |
| Monitoring OFF during active capture GREEN | commits `d5c98b510a9308d59af0536864113fb94adee7a7`, `78df4e833904996364d1f162037d86096d5c8fba`, `9177ce519cd48468b7a507b3639eed139573265f`; Pure Core run `36085897349` | PASS | Pure controller tests verify early partial finalization, warning, recorder deactivation and Monitoring reset. Game-system transition still requires in-game validation. |
| Vanilla/MOD source-kind attribution RED | commit `d993662d857932debe26744376a954e19f866cd5`, Pure Core run `36085954663` | Expected failure | `sourceKind` was not yet carried through timing/UI projection. |
| Vanilla/MOD source-kind attribution GREEN | commit `4e09a7f938011a1a5b963732cd6da3adcc87a1af`, Pure Core run `36086121921` | PASS | Vanilla systems remain visible in Systems but are excluded from direct MOD cost; patch-owner metadata remains separate. |
| Export fidelity RED | commit `9ad7a57726f9cf072c03beb53a6953f1ccd73e93`, Pure Core run `36086170499` | Expected failure | Export DTOs lacked median/P99/max/total/calls and metric-unit preservation. |
| Export fidelity GREEN | commit `e33070f7a6273a990c78ec2a968b639bcf5f7464`, Pure Core run `36086351898` | PASS | Report DTO/builder preserve supported statistics and units through privacy-sanitized copies. |
| Current/completed/selected capture separation RED | commits `1362ce21db4ecb188e4eb61531213cd5f8bf9824`, `0015fe361f7964d2f533046c7d502283521d04dd` | Expected failure | Pure tests failed on missing `CurrentCapture`/`SelectedCaptureId`; UI test showed PostBuffer manual capture was still enabled. |
| Current/completed/selected capture separation GREEN | commits `4aa16993a2bcc87637b752f2ab155d3150d5ac6e`, `f5f8dc29f4f18450f93ef3a38291a16cf0ad8c56`, `bd3929367c4f442a5efeb682ffe872a59c4e3424`, `689376951cc54583fdbeabccd149405836e939f6`; UI run `36088488554`, Pure Core run `36088562657` | PASS | Completed summaries no longer include the active capture. Systems/timeline use one selected/current/latest capture. PostBuffer manual capture is disabled. Raw UI serialization now preserves source kind, median and total fields. |
| Historical export scope RED | commits `ac3fb8a84aabd45ad5a84866154784e6f5d35e88`, `473bf17562b1277c29e3ffd76767ce53e848f3f9`; Pure Core run `36089481878` | Expected failure | Final review found that selected historical Systems/Timeline could still be exported beside current Global/Pathfinding/Domain data. RED failed on missing export-specific projection/scope fields. |
| Historical export scope GREEN | commit `d79733defe882c29de28474cdeacbaa547f55067`; Pure Core run `36089778625`, UI run `36089778609` | PASS | Historical export uses the selected/latest completed capture's stored Global sample and retained Pathfinding/Domain snapshots when present, and records `detailCaptureId`, `detailScope`, and `globalTimestampSeconds`. Live UI projection remains unchanged. |
| Capture-processing overhead wording RED | commit `0be803fbac09880919d1e57233823e3fd6143f9c`, UI run `36088653568` | Expected failure | Regression test rejected ambiguous whole-profiler-overhead wording. |
| Capture-processing overhead wording GREEN | commit `f3a068eb0c01cb0112ad35a453f84490d64a27f3`, UI run `36088777589`, Pure Core run `36088777556` | PASS | UI labels explicitly describe the measured capture-processing share and state that it is not total game-wide profiler overhead. |
| Official toolchain variable/deploy inspection | supplied `CS2-managed-reference-fix(1).zip`, README commit `6566dc845f968f74ea854e02d579ff02123f9f7a` | PASS for documentation/source agreement | Supplied `Mod.props` uses `CSII_MANAGEDPATH` and User-scope toolchain variables; supplied `Mod.targets` deploys to `CSII_LOCALMODSPATH\$(TargetName)`. This validates the README against the supplied toolchain files, not against an actual Windows build. |
| UI dependency audit | UI run `36250034356` | PASS | `npm ci` reported `found 0 vulnerabilities`. The previous 5-vulnerability maintenance note is resolved for the current lockfile used by CI. |

## 2026-09-26 review-hardening evidence

| Check | Evidence | Result | Scope / limitation |
| --- | --- | --- | --- |
| Capability/queue/service-vehicle/timing diagnostics RED | commit `7b94494c576021af12a9bd99e32eb85654f9c6c9`, Pure Core run `36250926271` | Expected failure | Four targeted regressions failed while the other 95 tests passed: unavailable-only capability groups were reported available, Length-only queue containers were rejected, the service-vehicle aggregate stayed unavailable, and zero-result system timing lacked projection-stage diagnostics. |
| Capability/queue/service-vehicle/timing diagnostics GREEN | commit `15bcb6f1b60aa079d7927d37f711e4f3c75b715f`, Pure Core run `36251219666` | PASS | 99/99 Pure Core tests passed after the focused changes. The pure project still does not compile `DomainMetricsSystem` against the full official game toolchain. |
| Service-vehicle managed-reference inspection | supplied `CS2-managed-reference-fix(1).zip` | PASS for static type evidence | The supplied `Game.dll` exposes `Game.Vehicles` components for Ambulance, Hearse, MaintenanceVehicle, FireEngine, GarbageTruck, PoliceCar, PostVan and PrisonerTransport, with generated ECS ComponentTypeHandle/ComponentLookup names. This supports the read-only union query, but actual runtime counts remain an in-game validation item. |
| Pathfinding request/result rates | supplied `Game.dll` + source review | INTENTIONALLY UNAVAILABLE | `GetPathfindCompleted` and related names exist, but binary-name presence does not establish counter lifetime/reset semantics. `requestsPerSecond` and `resultsPerSecond` therefore remain unavailable rather than inventing a rate. |

## In-game validation matrix

Record concrete measurements and evidence in this table when the scenarios are executed. Do not replace missing measurements with inferred values.

| # | Scenario | Status | Selected speed | Actual speed | FPS | Profiler overhead | Capture coverage | Errors / warnings | Pass criteria / notes |
| ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Profiler disabled baseline | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Set `EnableMonitoring` false, allow at least one system update for the disable transition, and independently confirm recorder activity stops before collecting the baseline. |
| 2 | Stable Normal Monitoring on a new/vanilla city | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Global/domain metrics remain stable and truthful; no unexplained errors; measure profiler overhead rather than assuming it. |
| 3 | Normal Monitoring on the representative ~40k modded city | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Same-save comparison against disabled baseline; observe stability and overhead under the intended real workload. |
| 4 | Manual Deep Capture | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Manual request starts a bounded capture, records the intended Deep window and post-buffer, attempts safe marker categories, reports coverage/batching, and does not force Job completion or mutate gameplay state. |
| 5 | Automatic Deep Capture during selected 4x / reduced actual speed | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Verify the trigger from observed data: efficiency below 0.80 continuously for at least 2 s. Confirm pre/deep/post context without treating correlation as a proven cause. |
| 6 | Large marker set / batched coverage | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Confirm batching remains bounded (configured concurrent recorder ceiling: 150), adaptive reduction occurs only after sustained profiler overhead, every safe category is attempted across batches as designed, and Diagnostics coverage matches what was actually sampled. |
| 7 | Collector failure / deliberately unavailable member | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Failure remains isolated; monitoring continues; warning/diagnostic is visible; unavailable data is not rendered as zero or fabricated. |
| 8 | Mod-owned ECS system classification | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | A uniquely matched direct system marker produces timing for the correct full type; direct owner assembly/mod metadata is retained and Mods totals use direct ownership only. |
| 9 | Patched vanilla system metadata | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Vanilla system remains the timing owner; patch owner(s) appear as metadata and do not inherit the vanilla timing as direct mod cost. |
| 10 | JSON export and privacy inspection | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Selected-capture values in JSON match the UI/source capture. Historical export must not mix current-only values into the retained capture scope. Inspect for `C:\Users\`, account name, absolute `ModsData`/home paths and other identifying path leakage before sharing. |
| 11 | Monitoring OFF during an active Deep Capture | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Verify normal and dedicated Deep Capture recorders stop on the disable transition, no capture sampling continues while disabled, and re-enabling returns to a coherent normal/deep lifecycle without errors. |
| 12 | Profiler panel closed for an extended session | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | HUD speed/capture state continues updating while the full Systems/Mods/Timeline/Captures snapshot is not periodically rebuilt; opening the panel immediately refreshes full detail. |
| 13 | Service-vehicle domain aggregate | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | Compare `serviceVehicles` against a controlled city state and verify Ambulance/Hearse/MaintenanceVehicle/FireEngine/GarbageTruck/PoliceCar/PostVan/PrisonerTransport entities are included while `Deleted`/`Temp` entities are excluded. |
| 14 | Profiler panel input and layout | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | With the panel open, Escape closes it without opening the pause menu (and Escape with the panel closed still opens the pause menu). Dragging the header moves the panel, dragging the lower-right grip resizes it, the geometry survives a game restart, and "位置をリセット" restores the default. The launcher icon renders and aligns with other GameTopLeft launchers. Timeline series toggles are buttons that accept no text input. |
| 15 | Frame-normalized system/mod cost | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | In a capture that contains an autosave, `SerializerSystem` keeps its raw per-call max but ranks by its small per-frame cost; Mods totals show `フレーム平均`, and patch owners are grouped under the patching assembly instead of Harmony IDs. |
| 16 | Diagnostic log | **NOT RUN / UNVERIFIED** | — | — | — | — | — | — | With `EnableDiagnosticLog` on, one CSV row per second is written while a city is open and the file closes on returning to the menu or loading another city. Check that `frameMs*` agrees with the in-game FPS, whether `cpuMainMs*`/`gpuMs*` are filled (`frameTimingFeature` in the mod log), that `simStepsPerSecond` is about 60 × selected speed when the city is light, and that the markers file lists the recorder names normal monitoring and `frame.p95.ms` expect. |

## Measurement protocol

1. Use the same save, camera position and simulation state where practical.
2. For the disabled baseline, set `EnableMonitoring` false and allow the monitoring lifecycle transition to execute. The current implementation deactivates the normal-monitoring recorder manager in `GlobalMetricsCollector`; `CaptureRuntimeSystem` also deactivates its dedicated Deep Capture recorder manager and resets an active capture. Independently verify recorder inactivity in-game before treating the run as a zero-sampling baseline.
3. Record selected speed, actual speed, FPS, profiler self-overhead, capture coverage, errors and warnings.
4. Exercise both manual and automatic capture paths.
5. For the representative slowdown case, preserve the pre/deep/post timeline and describe only observed correlations unless an independent causal test supports a stronger statement.
6. Inspect the exported JSON itself; UI appearance alone is not sufficient for privacy/export validation. For a completed historical capture, verify `captureConfig.detailCaptureId`, `detailScope=historical-capture`, and the exported global timestamp against the selected capture. When retained Pathfinding/Domain snapshots exist, verify those sections also come from the selected capture; otherwise keep them unavailable rather than substituting current live values.
7. For long-session memory validation, create more than 20 captures and confirm the UI retains only the newest 20 completed sessions while timing projection continues for newly completed captures.
8. With the panel closed, compare profiler self-overhead against the panel-open state; the closed state should perform only the lightweight HUD binding update at the periodic UI refresh cadence.

The design targets for Normal Monitoring are **<1% CPU overhead** and **<2% simulation-impact overhead**. These are acceptance targets only. They must not be reported as achieved until measured in-game with an appropriate baseline.

## System timing acceptance checks

For a completed capture that contains a uniquely matching ECS profiler marker:

- `TimeNanoseconds` is converted to milliseconds.
- Current, mean, median, P95, P99, max, total and calls are preserved where samples exist.
- Full type name matching is conservative; ambiguous matches are skipped rather than guessed.
- If zero systems can be projected, the warning exposes catalog-system count, profiler-marker count, TimeNanoseconds marker count, unique and ambiguous matches, unique matches without captured samples, and captured marker coverage.
- The completed `CaptureSession.SystemTiming` is populated exactly once while it remains in retained history and remains available to Systems/Mods UI projection.
- Completed capture history is bounded; eviction of older sessions must not cause new sessions to be skipped by timing finalization.
- Direct mod ownership and patch-owner metadata remain separate concepts.
- Missing or unsafe-to-measure work remains unavailable/unattributed; no forced Job completion is introduced.

The pure-layer tests cover projection/finalization, skipped-state lifecycle behavior, monitoring edge tracking, overhead adaptation, bounded-history behavior, historical-export scoping, truthful capability reporting and supported Pathfinding/domain fallback behavior. UI CI covers the compact HUD binding, closed-panel refresh policy and current binding/build contract. Actual Unity/CS2 profiler marker names, game lifecycle behavior, full Windows mod compilation and UI presentation with real captures still require the in-game matrix above.

## Release-candidate evidence policy

For each runtime row, attach or reference enough evidence to reproduce the result: save/scenario description, game/mod version, logs, screenshots or capture/export artifacts, and the measured values. If evidence is incomplete, leave the row **UNVERIFIED** rather than inferring a pass.
