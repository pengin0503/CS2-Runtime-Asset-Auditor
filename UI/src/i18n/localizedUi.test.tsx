import React from "react";
import TestRenderer, { act } from "react-test-renderer";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { LocaleProvider, translate, type Locale } from "./locale";
import { OverviewTab } from "../profiler/tabs/OverviewTab";
import { PerformanceAdvisorTab, advisorActionMessage } from "../profiler/tabs/PerformanceAdvisorTab";
import { EMPTY_ADVISOR, EMPTY_SNAPSHOT, type AdvisorUiState } from "../profiler/bindings";
import { ScanStatus } from "../assets/components/ScanStatus";
import { WarningsTab, FINDINGS_PAGE_SIZE } from "../assets/tabs/WarningsTab";
import { EvidencePanel } from "../assets/components/EvidencePanel";
import { parseFindings } from "../assets/bindings";
import { exportResultLabel, diagnosticMessageLabel, advisorTextLabel } from "../profiler/text";
import type { UiFinding } from "../assets/types";

const inLocale = (locale: Locale, element: React.ReactElement) => <LocaleProvider locale={locale}>{element}</LocaleProvider>;

function text(node: any): string {
  if (node == null) return "";
  if (typeof node === "string") return node;
  if (Array.isArray(node)) return node.map(text).join("");
  return text(node.children);
}

const finding = (index: number, overrides: Partial<UiFinding> = {}): UiFinding => ({
  ruleId: "APA-LOD-001", status: "Notice", category: "Lod", title: "No lower LOD observed",
  explanation: "No lower LOD was observed. This is an observation and is not automatically a performance defect.",
  evidence: [`asset=P${index}`], basis: "Observation", ruleVersion: "1.1", prefabId: `P${index}`, prefabType: "Building",
  ...overrides
});

describe("interface language follows the game locale", () => {
  it("renders the runtime overview in English and Japanese", () => {
    const props = { snapshot: EMPTY_SNAPSHOT, onManualCapture: () => {}, onExport: () => {}, exportResult: "" };
    const english = renderToStaticMarkup(inLocale("en", <OverviewTab {...props} />));
    const japanese = renderToStaticMarkup(inLocale("ja", <OverviewTab {...props} />));
    expect(english).toContain("Selected speed");
    expect(english).not.toContain("指定速度");
    expect(japanese).toContain("指定速度");
    expect(japanese).not.toContain("Selected speed");
  });

  it("renders the asset scan status in Japanese, including backend stage names", () => {
    const scan = { state: "Running", stage: "Collecting geometry metadata", stageNumber: 3, totalStages: 8, completedItems: 1, totalItems: 4,
      queuedBecauseRuntimeCapture: false, interruptedByRuntimeCapture: false } as any;
    const japanese = renderToStaticMarkup(inLocale("ja", <ScanStatus scan={scan} />));
    expect(japanese).toContain("形状メタデータを収集中");
    expect(japanese).toContain("段階 3 / 8");
    const english = renderToStaticMarkup(inLocale("en", <ScanStatus scan={scan} />));
    expect(english).toContain("Collecting geometry metadata");
    expect(english).toContain("Stage 3 of 8");
  });

  it("translates finding titles by rule ID for Japanese and keeps backend English otherwise", () => {
    const item = finding(1);
    expect(renderToStaticMarkup(inLocale("ja", <EvidencePanel finding={item} />))).toContain("下位 LOD が見つかりません");
    expect(renderToStaticMarkup(inLocale("en", <EvidencePanel finding={item} />))).toContain("No lower LOD observed");
    const unknownRule = finding(2, { ruleId: "APA-NEW-999", title: "Future rule" });
    expect(renderToStaticMarkup(inLocale("ja", <EvidencePanel finding={unknownRule} />))).toContain("Future rule");
  });

  it("translates canonical backend diagnostics and advisor rationales only for Japanese", () => {
    const pending = "Per-system timing becomes available once a matching Deep Capture completes.";
    expect(diagnosticMessageLabel(pending, "ja")).toContain("詳細キャプチャ");
    expect(diagnosticMessageLabel(pending, "en")).toBe(pending);
    const rationale = "Both frame time and GPU time show elevated rendering load.";
    expect(advisorTextLabel(rationale, "ja")).toContain("描画負荷");
    expect(advisorTextLabel(rationale, "en")).toBe(rationale);
  });

  it("formats the shared export protocol for both export buttons", () => {
    expect(exportResultLabel("ok:report.json", "en")).toBe("Export complete: report.json");
    expect(exportResultLabel("error:APA-EXP-001: disk full", "ja")).toBe("エクスポート失敗: APA-EXP-001: disk full");
  });
});

describe("Advisor feedback and session undo", () => {
  const t = (key: any, params?: any) => translate("en", key, params);

  it("reports why an action failed instead of staying silent", () => {
    const advisor: AdvisorUiState = { ...EMPTY_ADVISOR, lastAction: { kind: "Apply", settingId: "Game.Settings.Graphics::shadow",
      displayName: "Shadow quality", succeeded: false, failureReason: "StaleOrUnavailableRecommendation", succeededCount: 0,
      failedSettingIds: ["Game.Settings.Graphics::shadow"], confirmationRequiredSettingIds: [], atUtc: "" } };
    const html = text(TestRenderer.create(inLocale("en", <PerformanceAdvisorTab advisor={advisor} />)).toJSON());
    expect(html).toContain("Shadow quality: The recommendation is out of date");
    expect(advisorActionMessage({ ...advisor.lastAction!, failureReason: "AdvisorApplyFailure:IOException" }, t))
      .toContain("AdvisorApplyFailure:IOException");
  });

  it("asks for confirmation before undoing the session and sends the confirmation", () => {
    const calls: boolean[] = [];
    const advisor: AdvisorUiState = { ...EMPTY_ADVISOR, changes: [
      { settingId: "id.display", displayName: "Display mode", originalValue: "Full", appliedValue: "Window", currentObservedValue: "Window", status: "Applied" },
      { settingId: "id.shadow", displayName: "Shadows", originalValue: "High", appliedValue: "Low", currentObservedValue: "Low", status: "Applied" }
    ] };
    const renderer = TestRenderer.create(inLocale("en", <PerformanceAdvisorTab advisor={advisor} onUndoSession={confirmed => calls.push(confirmed)} />));
    const buttonWith = (label: string) => renderer.root.findAll(node => node.props.onSelect && text(node.props.children).includes(label))[0];

    act(() => buttonWith("Undo this session's changes").props.onSelect());
    expect(calls).toEqual([]);
    act(() => buttonWith("Confirm: restore 2 setting(s)").props.onSelect());
    expect(calls).toEqual([true]);
    const html = text(renderer.toJSON());
    expect(html).toContain("Display mode");
    expect(html).not.toContain("id.display");
  });

  it("marks an applied recommendation instead of offering Apply again", () => {
    const advisor: AdvisorUiState = { ...EMPTY_ADVISOR, recommendations: [{ settingId: "id.shadow", displayName: "Shadows", currentValue: "High",
      recommendedValue: "Low", direction: "LowerRecommended", priority: "High", confidence: "High", rationale: "", evidenceIds: [],
      applyCapability: "Available", applyBehavior: "ApplyRequired" }],
      changes: [{ settingId: "id.shadow", originalValue: "High", appliedValue: "Low", currentObservedValue: "Low", status: "Applied" }] };
    const html = text(TestRenderer.create(inLocale("en", <PerformanceAdvisorTab advisor={advisor} onApply={() => {}} />)).toJSON());
    expect(html).toContain("Applied. Capture and diagnose again");
    expect(html).not.toMatch(/\bApply\b(?!ing)/);
  });
});

describe("findings delivery", () => {
  it("parses the separate findings binding and treats malformed payloads as no analysis", () => {
    expect(parseFindings('{"analysisGeneration":3,"findings":[]}').analysisGeneration).toBe(3);
    expect(parseFindings("{}")).toEqual({ analysisGeneration: 0, findings: [] });
    expect(parseFindings("not json")).toEqual({ analysisGeneration: 0, findings: [] });
  });

  it("renders findings a page at a time", () => {
    const findings = Array.from({ length: FINDINGS_PAGE_SIZE + 30 }, (_, index) => finding(index));
    const renderer = TestRenderer.create(inLocale("en", <WarningsTab findings={findings} />));
    expect(renderer.root.findAll(node => node.props.className === "apa__finding").length).toBe(FINDINGS_PAGE_SIZE);
    const more = renderer.root.findAll(node => node.type === "button" && text(node.props.children).includes("Show 30 more"))[0];
    act(() => more.props.onClick());
    expect(renderer.root.findAll(node => node.props.className === "apa__finding").length).toBe(FINDINGS_PAGE_SIZE + 30);
  });
});
