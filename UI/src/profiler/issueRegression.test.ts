import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("repository issue regressions", () => {
  it("preserves Unity recorder handles until rediscovery succeeds", () => {
    const source = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Profiling/UnityRecorderBackend.cs"), "utf8");
    const discoveryIndex = source.indexOf("ProfilerRecorderHandle.GetAvailable");
    const clearIndex = source.indexOf("_handles.Clear()");

    expect(discoveryIndex).toBeGreaterThanOrEqual(0);
    expect(clearIndex).toBeGreaterThan(discoveryIndex);
  });

  it("uses the current runtime clock for manual capture requests instead of the latest sampled timestamp", () => {
    const globalSource = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Collectors/GlobalMetricsCollector.cs"), "utf8");
    const captureSource = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs"), "utf8");

    expect(globalSource).toContain("CurrentTimestampSeconds => _clock.Elapsed.TotalSeconds");
    expect(captureSource).toContain("_global?.CurrentTimestampSeconds");
    expect(captureSource).not.toContain("var now = latest?.TimestampSeconds");
  });

  it("closes the profiler panel through the game's Back input action instead of a DOM keydown listener", () => {
    const source = readFileSync(new URL("../shell/RuntimeAssetAuditorRoot.tsx", import.meta.url), "utf8");
    const bindings = readFileSync(new URL("./bindings.ts", import.meta.url), "utf8");
    const system = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs"), "utf8");

    // Escape arrives through the game's input system as the "Back" action; the vanilla pause menu
    // consumed it before any DOM listener ran, so a keydown listener never closed the panel.
    expect(source).toContain('from "cs2/input"');
    expect(source).toMatch(/<InputActionConsumer actions=\{BACK_ACTIONS\} ignoreFocusState>/);
    expect(source).toContain("Back: closePanel");
    expect(source).not.toContain('addEventListener("keydown"');
    // Close must be idempotent so a repeated Back delivery can never toggle the panel open again.
    expect(bindings).toContain('trigger(GROUP, "setPanelVisible", false)');
    expect(system).toContain('new TriggerBinding<bool>(Group, "setPanelVisible", SetPanelVisible)');
    expect(system).toMatch(/if \(_panelVisible == visible\)\s*return;/);
  });

  it("keeps review hardening wiring and export documentation aligned", () => {
    const reportBuilder = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Export/ProfilerReportBuilder.cs"), "utf8");
    const domains = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Collectors/DomainMetricsSystem.cs"), "utf8");
    const timing = readFileSync(path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Core/CaptureSystemTimingFinalizer.cs"), "utf8");
    const readme = readFileSync(path.resolve(process.cwd(), "../README.md"), "utf8");

    expect(reportBuilder).toContain("HasAvailableMetric(report.Pathfinding)");
    expect(reportBuilder).toContain("HasAvailableMetric(report.DomainMetrics)");
    expect(domains).toContain('"Game.Vehicles.ServiceVehicleUnion"');
    expect(domains).toContain("ComponentType.ReadOnly<Ambulance>()");
    expect(domains).toContain("ComponentType.ReadOnly<PrisonerTransport>()");
    expect(timing).toContain("timeMarkers={diagnostics.TimeMarkerCount}");
    expect(timing).toContain("ambiguousMatches={diagnostics.AmbiguousMatchCount}");
    expect(readme).toContain("CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json");
    expect(readme).toContain("`-1`、`-2`");
  });

});
