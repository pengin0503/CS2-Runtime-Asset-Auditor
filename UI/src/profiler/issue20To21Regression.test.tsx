import React from "react";
import TestRenderer from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { TimelineTab, seriesVisualIdentity } from "./tabs/TimelineTab";
import { PerformanceAdvisorTab } from "./tabs/PerformanceAdvisorTab";
import { EMPTY_ADVISOR } from "./bindings";
import { useGameLocale } from "../test/locale";

useGameLocale("ja-JP");

function text(node: any): string {
  if (node == null) return "";
  if (Array.isArray(node)) return node.map(text).join("");
  return typeof node === "object" ? text(node.children) : String(node);
}

describe("Issue #20 timeline identity", () => {
  it("disambiguates different metric sources that share the same short name", () => {
    const points = [
      { timestampSeconds: 1, metric: "recorder:Render\u001fGPU Frame Time", value: 10, confidence: "Full", unitType: "TimeNanoseconds" },
      { timestampSeconds: 2, metric: "recorder:Render\u001fGPU Frame Time", value: 11, confidence: "Full", unitType: "TimeNanoseconds" },
      { timestampSeconds: 1, metric: "marker:GPU\u001fGPU Frame Time", value: 12, confidence: "Full", unitType: "TimeNanoseconds" },
      { timestampSeconds: 2, metric: "marker:GPU\u001fGPU Frame Time", value: 13, confidence: "Full", unitType: "TimeNanoseconds" }
    ] as any;

    const rendered = text(TestRenderer.create(<TimelineTab points={points} />).toJSON());
    expect(rendered).toContain("Render / GPU Frame Time");
    expect(rendered).toContain("GPU / GPU Frame Time");
  });

  it("uses a second visual channel when the color palette repeats after eight series", () => {
    const first = seriesVisualIdentity(0);
    const ninth = seriesVisualIdentity(8);
    expect(ninth.color).toBe(first.color);
    expect(ninth.dash).not.toBe(first.dash);
  });
});

describe("Issue #21 advisor presentation", () => {
  it("renders user-facing enum values in Japanese instead of raw internal identifiers", () => {
    const advisor = {
      ...EMPTY_ADVISOR,
      selectedCaptureId: "capture",
      available: true,
      observations: [{ category: "RenderingGpu", severity: "High", confidence: "Low",
        evidenceIds: ["gpu.frame.ms"], rationale: "描画負荷を確認" }],
      recommendations: [{ settingId: "quality", displayName: "品質", currentValue: "High", recommendedValue: "Medium",
        direction: "LowerRecommended", priority: "High", confidence: "Low", rationale: "再計測してください",
        evidenceIds: ["gpu.frame.ms"], applyCapability: "ReadOnlyForAdvisor", applyBehavior: "ReadOnlyForAdvisor" }],
      changes: [{ settingId: "quality", originalValue: "High", appliedValue: "Medium",
        currentObservedValue: "Medium", status: "Applied" }]
    } as any;

    const rendered = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]} />).toJSON());
    expect(rendered).toContain("描画/GPU・高");
    expect(rendered).toContain("優先度: 高・確信度: 低");
    expect(rendered).toContain("状態: 適用済み");
    expect(rendered).not.toContain("RenderingGpu・High");
  });

  it("translates advisor availability failures instead of exposing the English transport message", () => {
    const advisor = {
      ...EMPTY_ADVISOR,
      unavailableReason: "Standard Options catalog unavailable: MissingMethodException"
    } as any;
    const rendered = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]} />).toJSON());
    expect(rendered).toContain("標準設定カタログを取得できません");
    expect(rendered).not.toContain("Standard Options catalog unavailable");
  });
});
