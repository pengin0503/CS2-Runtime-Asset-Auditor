import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import TestRenderer, { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { OverviewTab } from "./OverviewTab";
import { SystemsTab } from "./SystemsTab";
import { ModsTab } from "./ModsTab";
import { DiagnosticsTab } from "./DiagnosticsTab";

const snapshot: any = {
  global: {
    available: true,
    timestampSeconds: 10,
    selectedSpeed: 4,
    actualSpeed: 2,
    efficiency: 0.5,
    recorderMetrics: [
      { id: "CPU\u001fMain Thread", value: 12_500_000, unitType: "TimeNanoseconds", confidence: "Full", availability: "Available", reason: null },
      { id: "GPU\u001fGPU Time", value: null, confidence: "Unavailable", availability: "Unavailable", reason: "not exposed" }
    ]
  },
  capture: { state: "Monitoring", isDeepCapture: false, completedCount: 1 },
  systems: [
    { id: "TrafficSystem", ownerAssembly: "Game", currentMilliseconds: 3.4, meanMilliseconds: null, p95Milliseconds: null, p99Milliseconds: null, maxMilliseconds: null, calls: null, confidence: "Managed", patchOwners: ["TrafficTweaks"] }
  ],
  mods: [
    { assemblyName: "TrafficTweaks", directSystemMilliseconds: 1.2, directSystemCount: 2, patchedVanillaSystemCount: 1 }
  ],
  pathfinding: { metrics: [] },
  domainMetrics: [], timeline: [], captures: [],
  diagnostics: { profilerOverheadShare: 0.02, unattributedJobsMilliseconds: 4.1, messages: [] }
};

function renderedText(node: any): string {
  if (node == null) return "";
  if (Array.isArray(node)) return node.map(renderedText).join("");
  if (typeof node === "object") return renderedText(node.children);
  return String(node);
}

describe("core profiler tabs", () => {
  it("overview shows Japanese status text and formats profiler units truthfully", () => {
    const html = renderToStaticMarkup(<OverviewTab snapshot={snapshot} onManualCapture={() => {}} onExport={() => {}} exportResult="" />);
    expect(html).toContain("4×");
    expect(html).toContain("2×");
    expect(html).toContain("50.0%");
    expect(html).toContain("監視中");
    expect(html).toContain("12.50 ms");
    expect(html).toContain("not exposed");
  });

  it("manual capture is disabled while the state machine is in post-buffer", () => {
    const postBufferSnapshot = {
      ...snapshot,
      capture: { state: "PostBuffer", isDeepCapture: false, completedCount: 1 }
    };
    const html = renderToStaticMarkup(<OverviewTab snapshot={postBufferSnapshot} onManualCapture={() => {}} onExport={() => {}} exportResult="" />);
    expect(html).toContain("手動詳細キャプチャ");
    expect(html).toContain("disabled=\"\"");
  });

  it("systems view keeps owner and patch metadata separate", () => {
    const renderer = TestRenderer.create(<SystemsTab systems={snapshot.systems} />);
    const collapsedHtml = renderedText(renderer.toJSON());
    expect(collapsedHtml).toContain("TrafficSystem");
    expect(collapsedHtml).toContain("所有元: Game");
    expect(collapsedHtml).toContain("パッチ: TrafficTweaks");
    expect(collapsedHtml).toContain("管理コード");
    expect(collapsedHtml).not.toContain("測定元:");

    const systemButton = renderer.root.find(node => node.type === "button" && typeof node.props["aria-expanded"] === "boolean");
    expect(systemButton.props["aria-expanded"]).toBe(false);
    act(() => systemButton.props.onClick());

    const expandedHtml = renderedText(renderer.toJSON());
    expect(expandedHtml).toContain("測定元: 管理システムの実行境界（管理コードの OnUpdate 同期実行時間。ジョブ/Burst のワーカー時間は含みません）");
    expect(expandedHtml).toContain("最後の呼び出し: 3.40 ms");
    expect(systemButton.props["aria-expanded"]).toBe(true);
  });

  it("systems view labels full-marker and unknown timing sources accurately", () => {
    const systems = [
      { ...snapshot.systems[0], id: "FullMarkerSystem", confidence: "Full", sourceKind: "Vanilla" },
      { ...snapshot.systems[0], id: "UnknownTimingSystem", confidence: "Unavailable", sourceKind: "Unknown" }
    ];

    for (const [index, expectedSource] of [
      [0, "Unity ECS プロファイラーマーカー"],
      [1, "不明"]
    ] as const) {
      const renderer = TestRenderer.create(<SystemsTab systems={[systems[index]]} />);
      const systemButton = renderer.root.find(node => node.type === "button" && typeof node.props["aria-expanded"] === "boolean");
      act(() => systemButton.props.onClick());
      expect(renderedText(renderer.toJSON())).toContain(`測定元: ${expectedSource}`);
      renderer.unmount();
    }
  });

  it("diagnostics displays unmeasured job time as unavailable", () => {
    const diagnostics = { ...snapshot.diagnostics, unattributedJobsMilliseconds: null };
    const html = renderToStaticMarkup(<DiagnosticsTab diagnostics={diagnostics} captures={[]} />);

    expect(html).toContain("未帰属ジョブ時間");
    expect(html).toContain("未計測");
    expect(html).not.toContain("0.00 ms");
  });

  it("systems view ranks by per-frame cost so a one-off autosave call does not lead", () => {
    // Values from an in-game report: SerializerSystem ran once for 502 ms during a 144-frame capture.
    const systems = [
      { ...snapshot.systems[0], id: "Game.Serialization.SerializerSystem", currentMilliseconds: 502.49, meanMilliseconds: 502.49, maxMilliseconds: 502.49, totalMilliseconds: 502.49, millisecondsPerFrame: 502.49 / 144, calls: 1, patchOwners: [] },
      { ...snapshot.systems[0], id: "Game.Rendering.PreRenderSystem", currentMilliseconds: 10.58, meanMilliseconds: 8.31, maxMilliseconds: 14.49, totalMilliseconds: 1196.05, millisecondsPerFrame: 1196.05 / 144, calls: 144, patchOwners: [] }
    ];
    const html = renderToStaticMarkup(<SystemsTab systems={systems} />);
    expect(html.indexOf("PreRenderSystem")).toBeLessThan(html.indexOf("SerializerSystem"));
    expect(html).toContain("8.31 ms");
    expect(html).toContain("3.49 ms");
    expect(html).toContain("502.49 ms");
    expect(html).toContain("フレーム平均");
  });

  it("systems view renders at most one page of rows and offers more", () => {
    const systems = Array.from({ length: 150 }, (_, index) => ({ ...snapshot.systems[0], id: `System${index}`, currentMilliseconds: index }));
    const html = renderToStaticMarkup(<SystemsTab systems={systems} />);
    expect(html).toContain("100 / 150 件を表示");
    expect(html).toContain("さらに 50 件表示");
    expect(html).not.toContain("<table");
  });

  it("mods view reports direct ownership and patched-system metadata without blame wording", () => {
    const html = renderToStaticMarkup(<ModsTab mods={snapshot.mods} />);
    expect(html).toContain("TrafficTweaks");
    expect(html).toContain("1.20 ms");
    expect(html).toContain("パッチ対象システム");
    expect(html).toContain("フレーム平均");
    expect(html.toLowerCase()).not.toContain("worst");
  });
});
