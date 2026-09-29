import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { PathfindingTab } from "./PathfindingTab";
import { TimelineTab } from "./TimelineTab";
import { CapturesTab } from "./CapturesTab";
import { DiagnosticsTab } from "./DiagnosticsTab";
import { useGameLocale } from "../../test/locale";

useGameLocale("ja-JP");

const snapshot: any = {
  global: { available: true, timestampSeconds: 20, selectedSpeed: 4, actualSpeed: 2.5, efficiency: 0.625, recorderMetrics: [] },
  capture: { state: "Monitoring", isDeepCapture: false, completedCount: 1 },
  systems: [], mods: [],
  pathfinding: { metrics: [
    { id: "pendingPathfindActions", value: 42, confidence: "Indirect", availability: "Available", reason: null },
    { id: "requestsPerSecond", value: null, confidence: "Unavailable", availability: "Unavailable", reason: "No verified runtime request counter is available for this game build." }
  ] },
  domainMetrics: [],
  timeline: [
    { timestampSeconds: 9, metric: "actualSpeed", value: 4, confidence: "Full" },
    { timestampSeconds: 10, metric: "actualSpeed", value: 2, confidence: "Full" },
    { timestampSeconds: 9, metric: "recorder:CPU\u001fMain Thread", value: 44_673_900, unitType: "TimeNanoseconds", confidence: "Full" },
    { timestampSeconds: 10, metric: "recorder:CPU\u001fMain Thread", value: 20_000_000, unitType: "TimeNanoseconds", confidence: "Full" },
    { timestampSeconds: 10, metric: "marker:Render\u001fBatches Count", value: 6449, confidence: "Full" },
    { timestampSeconds: 10, metric: "system:Game.Simulation.SimulationSystem", value: 10.3, unitType: "Milliseconds", confidence: "Managed" }
  ],
  captures: [
    {
      id: "capture-1", triggerKind: "Automatic", triggeredAtSeconds: 10,
      durationSeconds: 12, discoveredMarkers: 200, capturedMarkers: 180,
      batched: true, coverageRatio: 0.9, warningCount: 2, profilerOverheadShare: 0.045,
      warnings: [
        "Profiler overhead remains high; sampling stride increased to 4.",
        "Profiler memory grew by 128 MiB during this capture; marker batching reduced to 75 concurrent recorders."
      ],
      correlatedChanges: [
        { metric: "actualSpeed", before: 4, after: 2, delta: -2, relativeDelta: -0.5, confidence: "Full" }
      ]
    }
  ],
  diagnostics: {
    profilerOverheadShare: 0.02, unattributedJobsMilliseconds: 4.1, messages: ["runtime build unverified"],
    gameVersion: "1.6.2f1", profilerVersion: "0.1.0", discoveredMarkerCount: 200,
    capturedMarkerCount: 180, systemCount: 123, markerBatchSize: 75, samplingStride: 2,
    patchMapState: "Patch metadata observed"
  }
};

describe("Task 12 profiler tabs", () => {
  it("pathfinding localizes known unavailable reasons instead of exposing English runtime prose", () => {
    const html = renderToStaticMarkup(<PathfindingTab metrics={snapshot.pathfinding.metrics} />);
    expect(html).toContain("pendingPathfindActions");
    expect(html).toContain(">42<");
    expect(html).toContain("このゲーム環境では検証済みの要求カウンターを取得できません。");
    expect(html).not.toContain("No verified runtime request counter");
  });

  it("timeline renders a lightweight SVG and exposes available series toggles", () => {
    const html = renderToStaticMarkup(<TimelineTab points={snapshot.timeline} />);
    expect(html).toContain("<svg");
    expect(html).toContain("actualSpeed");
    expect(html).toContain("Main Thread");
  });

  it("timeline toggles are buttons, not Gameface text-field checkboxes, and skip single-sample series", () => {
    const html = renderToStaticMarkup(<TimelineTab points={snapshot.timeline} />);
    expect(html).not.toContain("<input");
    expect(html).toContain('aria-pressed="true"');
    expect(html).not.toContain("Batches Count");
    expect(html).not.toContain("SimulationSystem");
    expect(html).toContain("1点しかない値（1 件）");
    // Raw nanoseconds are shown with their unit in point tooltips.
    expect(html).toContain("44.67 ms");
    expect(html).not.toContain("44673900");
  });

  it("timeline point hit targets follow the same normalized Y coordinates as their line", () => {
    const html = renderToStaticMarkup(<TimelineTab points={snapshot.timeline.slice(0, 2)} />);
    expect(html).toContain('cy="28"');
    expect(html).toContain('cy="232"');
  });

  it("captures localize known profiler warnings", () => {
    const html = renderToStaticMarkup(<CapturesTab captures={snapshot.captures} />);
    expect(html).toContain("自動（低効率）");
    expect(html).toContain("12.0 秒");
    expect(html).toContain("90.0%");
    expect(html).toContain("サンプル取得率");
    expect(html).toContain("分割計測");
    expect(html).toContain("4.5%");
    expect(html).toContain("相関変化が大きい項目");
    expect(html).toContain("プロファイラー負荷が高い状態が続いているため、サンプリング間引きを 4 に増やしました。");
    expect(html).toContain("詳細キャプチャ中のプロファイラーメモリが 128 MiB 増加したため、同時記録数を 75 に抑えました。");
    expect(html).not.toContain("Profiler overhead remains high");
    expect(html).not.toContain("Profiler memory grew by");
  });

  it("captures render unavailable sampled marker rate as a dash instead of one hundred percent", () => {
    const unavailable = [{ ...snapshot.captures[0], discoveredMarkers: 0, capturedMarkers: 0, coverageRatio: null }];
    const html = renderToStaticMarkup(<CapturesTab captures={unavailable} />);
    expect(html).toContain("サンプル取得率 —");
    expect(html).not.toContain("サンプル取得率 100.0%");
  });

  it("diagnostics surfaces sampled marker rate, sampling and self-overhead state", () => {
    const html = renderToStaticMarkup(<DiagnosticsTab diagnostics={snapshot.diagnostics} captures={snapshot.captures} />);
    expect(html).toContain("1.6.2f1");
    expect(html).toContain("200");
    expect(html).toContain("75");
    expect(html).toContain("サンプル取得率");
    expect(html).toContain("runtime build unverified");
  });

  it("diagnostics describes measured capture-processing overhead instead of total profiler overhead", () => {
    const html = renderToStaticMarkup(<DiagnosticsTab diagnostics={snapshot.diagnostics} captures={snapshot.captures} />);
    expect(html).toContain("キャプチャ処理負荷");
    expect(html).toContain("最大キャプチャ処理負荷");
    expect(html).not.toContain("現在のプロファイラー負荷");
    expect(html).not.toContain("最大自己負荷");
  });
});
