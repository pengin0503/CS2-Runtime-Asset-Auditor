import React from "react";
import TestRenderer, { act } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { PerformanceAdvisorTab } from "./tabs/PerformanceAdvisorTab";
import { EMPTY_ADVISOR, type AdvisorExperiment, type AdvisorRecommendation, type AdvisorUiState } from "./bindings";
import { useGameLocale } from "../test/locale";
import { en, ja } from "../i18n/messages";

useGameLocale("ja-JP");

const recommendation = (id: string, capability = "Available"): AdvisorRecommendation => ({
  settingId: id, displayName: id, currentValue: "High", recommendedValue: "Low",
  direction: "LowerRecommended", priority: "High", confidence: "High", rationale: "Measured",
  evidenceIds: ["frame.p95.ms"], applyCapability: capability, applyBehavior: "ConfirmationRequired"
});

const experiment = (state: AdvisorExperiment["state"], overrides: Partial<AdvisorExperiment> = {}): AdvisorExperiment => ({
  experimentId: "experiment", state, validity: "Valid", invalidationReason: "None",
  baselineCaptureId: "capture", followUpCaptureId: "", settingId: "shadow", settingDisplayName: "Shadow",
  originalValue: "High", testedValue: "Low", changeAppliedAtUtc: null, stabilizationReadyAtUtc: null,
  completionOutcome: "None", lastFailureReason: "", followUpWarnings: [], comparison: null, ...overrides
});

const advisor = (value?: AdvisorExperiment | null): AdvisorUiState => ({ ...EMPTY_ADVISOR, available: true,
  selectedCaptureId: "capture", recommendations: [recommendation("shadow"), recommendation("fog")], experiment: value ?? null });

function content(node: any): string {
  if (node == null) return "";
  if (Array.isArray(node)) return node.map(content).join("");
  return typeof node === "object" ? content(node.children) : String(node);
}

function press(renderer: TestRenderer.ReactTestRenderer, label: string) {
  const button = renderer.root.findAll(node => node.type === "button" && content(node.props.children).includes(label))[0];
  expect(button, `Missing button: ${label}`).toBeDefined();
  act(() => button.props.onClick());
}

describe("guided investigation experiment", () => {
  it("offers Test change only for an applicable change with a diagnosed capture", () => {
    const onStartExperiment = vi.fn();
    const eligible = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor()} onStartExperiment={onStartExperiment} />);
    press(eligible, "変更を試す");
    expect(onStartExperiment).toHaveBeenCalledWith("capture", "shadow", "Low");
    const disabled = advisor();
    disabled.recommendations = [recommendation("readonly", "ReadOnlyForAdvisor"),
      { ...recommendation("noop"), recommendedValue: "High" }];
    expect(content(TestRenderer.create(<PerformanceAdvisorTab advisor={disabled} onStartExperiment={onStartExperiment} />).toJSON()))
      .not.toContain("変更を試す");
    expect(content(TestRenderer.create(<PerformanceAdvisorTab advisor={{ ...advisor(), selectedCaptureId: "" }}
      onStartExperiment={onStartExperiment} />).toJSON())).not.toContain("変更を試す");
  });

  it("requires explicit Apply and a separate confirmation action", () => {
    const onApplyExperiment = vi.fn();
    const initial = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("BaselineReady"))}
      onApplyExperiment={onApplyExperiment} />);
    press(initial, "テスト変更を適用");
    expect(onApplyExperiment).toHaveBeenCalledWith(false);
    const confirming = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("AwaitingApplyConfirmation"))}
      onApplyExperiment={onApplyExperiment} />);
    press(confirming, "適用を確認");
    expect(onApplyExperiment).toHaveBeenLastCalledWith(true);
  });

  it("guides five seconds and sends follow-up only on an explicit click", () => {
    const onFollowUp = vi.fn();
    const ready = experiment("AwaitingFollowUp", { changeAppliedAtUtc: "2026-09-29T00:00:00Z",
      stabilizationReadyAtUtc: "2026-09-29T00:00:05Z" });
    const rendered = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(ready)} onStartExperimentFollowUp={onFollowUp} />);
    expect(content(rendered.toJSON())).toContain("5秒");
    expect(onFollowUp).not.toHaveBeenCalled();
    press(rendered, "追跡キャプチャを開始");
    expect(onFollowUp).toHaveBeenCalledTimes(1);
    expect(content(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("FollowUpCapturing"))} />).toJSON()))
      .toContain("追跡キャプチャを計測中");
  });

  it("prevents a second normal Apply while active and explains why", () => {
    const onApply = vi.fn();
    const rendered = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("AwaitingFollowUp"))} onApply={onApply} />);
    expect(content(rendered.toJSON())).toContain("他の設定の適用はできません");
    expect(content(rendered.toJSON())).not.toContain("確認が必要: 変更内容を確認");
  });

  it("shows localized invalidation and a rejected request without a result verdict", () => {
    const invalid = experiment("Invalidated", { validity: "Invalidated", invalidationReason: "SessionChanged",
      lastFailureReason: "FollowUpRequestRejected" });
    const rendered = content(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(invalid)} />).toJSON());
    expect(rendered).toContain("都市セッションが変わりました");
    expect(rendered).toContain("追跡キャプチャを開始できませんでした");
    expect(rendered).not.toContain("成功率");
  });

  it("lists neutral counts, exact metrics, and the non-causality notice after comparison", () => {
    const completed = experiment("Completed", { followUpCaptureId: "follow-up", comparison: {
      multipleChanges: false, changedSettingIds: ["shadow"], metrics: [
        { id: "frame.p95.ms", baselineValue: 25, followUpValue: 20, state: "Improved", reason: "" },
        { id: "gpu.frame.ms", baselineValue: 12, followUpValue: 15, state: "Regressed", reason: "" },
        { id: "fps", baselineValue: 60, followUpValue: 60, state: "NoMaterialChange", reason: "" },
        { id: "system.unknown.ms", baselineValue: null, followUpValue: null, state: "NotComparable", reason: "missing" }
      ] }, followUpWarnings: ["Capture finalized early"] });
    const rendered = content(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(completed)} />).toJSON());
    for (const value of ["改善 1", "悪化 1", "大きな変化なし 1", "比較不可 1", "frame.p95.ms", "25", "20",
      "因果関係を証明するものではありません", "Capture finalized early"])
      expect(rendered).toContain(value);
    expect(rendered).not.toContain("総合スコア");
  });

  it("shows that an interrupted follow-up was still compared, and why it stopped", () => {
    const comparison = { multipleChanges: false, changedSettingIds: ["shadow"], metrics: [
      { id: "frame.p95.ms", baselineValue: 25, followUpValue: 20, state: "Improved" as const, reason: "" }] };
    const interrupted = experiment("Completed", { followUpCaptureId: "follow-up", comparison,
      followUpInterruption: "SafetyLimit" });
    const rendered = content(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(interrupted)}
      onKeepExperiment={() => {}} />).toJSON());
    expect(rendered).toContain("計測の安全上限により途中で停止しました");
    expect(rendered).toContain("改善 1");
    expect(rendered).toContain("変更を維持");

    const full = experiment("Completed", { followUpCaptureId: "follow-up", comparison, followUpInterruption: "" });
    expect(content(TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(full)} />).toJSON()))
      .not.toContain("途中で");
  });

  it("uses explicit Keep, Undo and Cancel, preserving conflict controls", () => {
    const onKeepExperiment = vi.fn();
    const onUndoExperiment = vi.fn();
    const onCancelExperiment = vi.fn();
    const result = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("Completed"))}
      onKeepExperiment={onKeepExperiment} onUndoExperiment={onUndoExperiment} />);
    press(result, "変更を維持");
    press(result, "テスト変更を元に戻す");
    expect(onKeepExperiment).toHaveBeenCalledOnce();
    expect(onUndoExperiment).toHaveBeenCalledWith(false);
    const active = TestRenderer.create(<PerformanceAdvisorTab advisor={advisor(experiment("AwaitingFollowUp", {
      changeAppliedAtUtc: "2026-09-29T00:00:00Z" }))} onCancelExperiment={onCancelExperiment} />);
    expect(content(active.toJSON())).toContain("現在の設定値を維持して実験を中止");
    press(active, "現在の設定値を維持して実験を中止");
    expect(onCancelExperiment).toHaveBeenCalledOnce();
    const conflict = { ...advisor(experiment("Invalidated", { validity: "Invalidated" })), changes: [{
      settingId: "shadow", originalValue: "High", appliedValue: "Low", currentObservedValue: "Medium",
      status: "ExternallyModified" }] };
    expect(content(TestRenderer.create(<PerformanceAdvisorTab advisor={conflict} onResolveConflict={() => {}} />).toJSON()))
      .toContain("変更前の値へ戻す");
  });

  it("has English and Japanese copy for every experiment state and invalidation reason", () => {
    const keys = ["advisor.experiment.title", "advisor.experiment.test", "advisor.experiment.apply",
      "advisor.experiment.confirmApply", "advisor.experiment.stabilize", "advisor.experiment.followUp",
      "advisor.experiment.capturing", "advisor.experiment.keep", "advisor.experiment.undo",
      "advisor.experiment.cancelAfterApply", "advisor.experiment.disclaimer", "advisor.experiment.state.Invalidated",
      "advisor.experiment.reason.SessionChanged", "advisor.experiment.reason.AdditionalAdvisorSettingChanged",
      "advisor.experiment.reason.TestedSettingExternallyModified", "advisor.experiment.reason.FollowUpCaptureWrongSession",
      "advisor.experiment.interrupted.Unknown", "advisor.experiment.interrupted.SafetyLimit",
      "advisor.experiment.interrupted.MonitoringDisabled", "advisor.experiment.interrupted.Requested"] as const;
    for (const key of keys) {
      expect(en[key]).toBeTruthy();
      expect(ja[key]).toBeTruthy();
    }
  });
});
