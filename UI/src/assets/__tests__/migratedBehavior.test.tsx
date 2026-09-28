import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { normalizeUiSettings } from "../bindings";
import { ExportControls } from "../components/ExportControls";
import { AssetsTab } from "../tabs/AssetsTab";
import { OverviewTab } from "../tabs/OverviewTab";
import { SettingsTab } from "../tabs/SettingsTab";
import { DEFAULT_ASSET_QUERY_STATE, DEFAULT_SCAN_OPTIONS, EMPTY_UI_SNAPSHOT, type AssetAuditorBindings, type AssetRow } from "../types";

const bindings: AssetAuditorBindings = {
  requestCensus() {}, requestAssetAudit() {}, requestDeepInspection() {}, cancelCensus() {},
  requestAssetsPage() {}, requestExport() {}, updateSettings() {},
};

describe("migrated Asset UI behavior", () => {
  it("offers separate Census and Asset Audit actions", () => {
    const html = renderToStaticMarkup(<OverviewTab snapshot={EMPTY_UI_SNAPSHOT} bindings={bindings} />);
    expect(html).toContain("Run Census");
    expect(html).toContain("Run Asset Audit");
  });

  it("clamps unsafe settings and describes disabled collection as Not scanned", () => {
    const settings = normalizeUiSettings({
      ...DEFAULT_SCAN_OPTIONS, frameBudgetMs: -100, progressUpdateMs: 999999,
      pageSize: 10000, deepInspectionLimit: 0, uiScale: 9, collectNetworkEdges: false,
    });
    expect([settings.frameBudgetMs, settings.progressUpdateMs, settings.pageSize, settings.deepInspectionLimit, settings.uiScale])
      .toEqual([0.25, 2000, 200, 1, 1.5]);
    const html = renderToStaticMarkup(<SettingsTab settings={settings} onChange={() => {}} />);
    expect(html).toContain("Collect network edges");
    expect(html).toContain("Not scanned");
    expect(html).toContain("Heuristic findings");
    expect(html).toContain("Peer-outlier analysis");
  });

  it("exports a selected Asset CSV with its stable Prefab key", () => {
    const onExport = vi.fn();
    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(<ExportControls selectedAsset={{ prefabId: "House.A", prefabType: "Building" }} onExport={onExport} />); });
    const click = (label: string) => {
      const button = renderer.root.findAllByType("button").find(item => item.children.join("") === label);
      if (!button) throw new Error("Missing button: " + label);
      act(() => button.props.onClick());
    };
    click("CSV");
    click("Selected asset");
    click("Save Asset CSV report");
    expect(onExport).toHaveBeenCalledWith({
      format: "Csv", scope: "Selected", selectedKeys: [{ prefabId: "House.A", prefabType: "Building" }],
    });
    act(() => renderer.unmount());
  });

  it("routes a selected render relation to Deep Inspection using its complete key", () => {
    const onDeepInspect = vi.fn();
    const asset: AssetRow = {
      prefabId: "House.A", prefabType: "Building", displayName: "House A", sourceLabel: "Built-in",
      traits: ["Building"], countKind: "TopLevelObjects", instances: { availability: "NotScanned" },
      presence: "Unknown", renderCoverage: "Available",
      renderRelations: [{ kind: "DirectMesh", from: "Building:House.A", to: "Game.Prefabs.RenderPrefab:Render.House.A", lodLevel: null }],
    };
    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(<AssetsTab page={{ offset: 0, limit: 100, totalCount: 1, items: [asset] }}
      query={DEFAULT_ASSET_QUERY_STATE} onQueryChange={() => {}} onDeepInspect={onDeepInspect} />); });
    act(() => renderer.root.findByProps({ className: "apa__asset-row" }).findByType("button").props.onClick());
    const inspect = renderer.root.findAllByType("button").find(item => item.children.join("") === "Deep inspect");
    expect(inspect).toBeDefined();
    act(() => inspect!.props.onClick());
    expect(onDeepInspect).toHaveBeenCalledWith("Game.Prefabs.RenderPrefab:Render.House.A");
    act(() => renderer.unmount());
  });
});
