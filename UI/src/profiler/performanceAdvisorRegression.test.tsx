import React from "react";
import TestRenderer, { act } from "react-test-renderer";
import { describe, expect, it } from "vitest";
import { PerformanceAdvisorTab } from "./tabs/PerformanceAdvisorTab";
import { EMPTY_ADVISOR, type AdvisorRecommendation, type AdvisorUiState } from "./bindings";
import { readFileSync } from "node:fs";

const recommendation = (id: string, direction: string, priority: string): AdvisorRecommendation => ({
  settingId: id, displayName: id, currentValue: "High", recommendedValue: "Medium", direction, priority,
  confidence: "High", rationale: "計測後に再診断", evidenceIds: ["gpu.frame.ms"],
  applyCapability: "ReadOnlyForAdvisor", applyBehavior: "ReadOnlyForAdvisor"
});

describe("Performance Advisor explicit settings actions", () => {
  it("offers individual apply and undo plus session undo, without bulk apply", () => {
    const advisor = { ...EMPTY_ADVISOR, selectedCaptureId: "capture", available: true,
      recommendations: [{ ...recommendation("shadow", "LowerRecommended", "High"), applyCapability: "Available" }],
      changes: [{ settingId: "texture", originalValue: "High", appliedValue: "Medium",
        currentObservedValue: "Medium", status: "Applied" }] };
    const onApply = () => { throw new Error("render must not write a setting"); };
    const html = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]}
      onApply={onApply} onUndo={() => {}} onUndoSession={() => {}} />).toJSON());
    expect(html).toContain("適用");
    expect(html).toContain("元に戻す");
    expect(html).toContain("セッションの変更を元に戻す");
    expect(html).not.toContain("Apply All");
    expect(html).not.toContain("すべて適用");
  });

  it("exposes both explicit conflict choices without silently restoring an external change", () => {
    const advisor = { ...EMPTY_ADVISOR, changes: [{ settingId: "shadow", originalValue: "High",
      appliedValue: "Medium", currentObservedValue: "Low", status: "ExternallyModified" }] };
    const html = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]}
      onResolveConflict={() => {}} />).toJSON());
    expect(html).toContain("現在値を維持");
    expect(html).toContain("変更前の値へ戻す");
    expect(html).toContain("Low");
  });
});

describe("Performance Advisor before/after measurements", () => {
  it("shows an explicit re-diagnose action and neutral measured differences", () => {
    const advisor: AdvisorUiState = { ...EMPTY_ADVISOR, selectedCaptureId: "after", baselineCaptureId: "before",
      comparison: { multipleChanges: true, changedSettingIds: ["shadow", "texture"],
        metrics: [{ id: "frame.p95.ms", baselineValue: 25, followUpValue: 20,
          state: "Improved", reason: "" }] } };
    const html = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor}
      captures={[{ id: "after" } as any]} onRediagnose={() => {}} />).toJSON());
    expect(html).toContain("再診断");
    expect(html).toContain("改善");
    expect(html).toContain("25");
    expect(html).toContain("20");
    expect(html).toContain("複数の設定");
    expect(html).not.toContain("Shadow設定がFPSを改善");
  });
});

function text(node: any): string {
  if (node == null) return "";
  if (Array.isArray(node)) return node.map(text).join("");
  return typeof node === "object" ? text(node.children) : String(node);
}

describe("Performance Advisor read-only tab", () => {
  it("is a Japanese tab inside the existing scroll viewport", () => {
    const root = readFileSync(new URL("./ProfilerRoot.tsx", import.meta.url), "utf8");
    expect(root).toContain('["advisor", "改善提案"]');
    expect(root.indexOf('tab === "advisor"')).toBeGreaterThan(root.indexOf('<Scrollable vertical'));
  });

  it("separates priority, headroom and no-recommendation groups and collapses the last by default", () => {
    const advisor = {
      ...EMPTY_ADVISOR, selectedCaptureId: "capture", available: true,
      recommendations: [
        recommendation("shadow", "LowerRecommended", "High"),
        recommendation("texture", "LowerRecommended", "Medium"),
        recommendation("detail", "HeadroomAvailable", "Low"),
        recommendation("volume", "NoRecommendation", "Low")
      ]
    };
    const renderer = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]} />);
    const before = text(renderer.toJSON());
    for (const label of ["高優先", "中優先", "上げる余地あり", "推奨なし", "shadow", "texture", "detail"])
      expect(before).toContain(label);
    expect(before).not.toContain("volume");
    const toggle = renderer.root.find(node => node.type === "button" && node.props["aria-label"] === "推奨なしを表示");
    act(() => toggle.props.onClick());
    expect(text(renderer.toJSON())).toContain("volume");
  });

  it("explains read-only capability and exposes no setting mutation control", () => {
    const advisor = { ...EMPTY_ADVISOR, selectedCaptureId: "capture", available: true,
      recommendations: [recommendation("shadow", "LowerRecommended", "High")] };
    const html = text(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor} captures={[]} />).toJSON());
    expect(html).toContain("ゲームの標準設定画面で変更");
    expect(html).not.toContain("適用");
    expect(html).not.toContain("元に戻す");
    expect(html).not.toContain("すべて適用");
  });
});
