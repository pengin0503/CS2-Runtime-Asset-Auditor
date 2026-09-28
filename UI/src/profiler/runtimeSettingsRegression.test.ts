import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function repoFile(relativePath: string): string {
  return readFileSync(path.resolve(process.cwd(), "..", relativePath), "utf8");
}

describe("runtime settings and panel ergonomics", () => {
  it("exposes display, capture, sampling and advanced settings through the native options UI", () => {
    const source = repoFile("src/CS2RuntimeAssetAuditor/Setting.cs");

    for (const property of [
      "EnableMonitoring",
      "EnableAutomaticCapture",
      "UiScalePercent",
      "UiRefreshMilliseconds",
      "SamplingIntervalMilliseconds",
      "EfficiencyThresholdPercent",
      "LowEfficiencySustainSeconds",
      "PrebufferSeconds",
      "DeepCaptureSeconds",
      "PostbufferSeconds",
      "CooldownSeconds",
      "MaxConcurrentMarkers",
      "ProfilerOverheadLimitPercent",
      "MaxCompletedCaptures"
    ]) {
      expect(source).toContain(property);
    }

    expect(source).toContain("SettingsUISlider");
    expect(source).toContain("SettingsUIAdvanced");
  });

  it("scrolls the panel body with the vanilla Scrollable inside a definite flex viewport", () => {
    const stylesheet = readFileSync(new URL("./profiler.module.scss", import.meta.url), "utf8");
    const root = readFileSync(new URL("../shell/RuntimeAssetAuditorRoot.tsx", import.meta.url), "utf8");
    const panelBody = stylesheet.match(/\.panelBody\s*\{([\s\S]*?)\}/)?.[1] ?? "";

    // Gameface draws no native scrollbar for overflow containers; Scrollable renders a draggable track.
    expect(root).toMatch(/<Scrollable vertical trackVisibility="scrollable" className=\{styles\.panelBody\}>/);
    expect(panelBody).toMatch(/flex\s*:\s*1\s+1\s+0\s*;/);
    expect(panelBody).toMatch(/min-height\s*:\s*0\s*;/);
  });

  it("persists a user-adjusted panel position and size through hidden settings", () => {
    const setting = repoFile("src/CS2RuntimeAssetAuditor/Setting.cs");
    const system = repoFile("src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs");
    const root = readFileSync(new URL("../shell/RuntimeAssetAuditorRoot.tsx", import.meta.url), "utf8");

    for (const property of ["PanelLeft", "PanelTop", "PanelWidth", "PanelHeight"]) {
      expect(setting).toMatch(new RegExp(`\\[SettingsUIHidden\\]\\s*public int ${property}`));
    }
    expect(system).toContain('new TriggerBinding<int, int, int, int>(Group, "setPanelLayout", SetPanelLayout)');
    expect(system).toContain('new RawValueBinding(Group, "panelLayout", WritePanelLayout)');
    expect(system).toContain("settings.ApplyAndSave()");
    expect(root).toContain('onMouseDown={beginDrag("move")}');
    expect(root).toContain('onMouseDown={beginDrag("resize")}');
    // Layout is saved once per drag, not on every mouse move.
    expect(root).toMatch(/const onMouseUp = useCallback\(\(\) => \{[\s\S]*?savePanelLayout\(/);
    expect(root).not.toMatch(/const onMouseMove = useCallback\([\s\S]*?savePanelLayout[\s\S]*?\}, \[scale\]\);/);
  });

  it("binds the configured UI scale into the profiler panel", () => {
    const bindings = readFileSync(new URL("./bindings.ts", import.meta.url), "utf8");
    const root = readFileSync(new URL("../shell/RuntimeAssetAuditorRoot.tsx", import.meta.url), "utf8");

    expect(bindings).toContain('bindValue<number>(GROUP, "uiScalePercent"');
    expect(bindings).toContain("useUiScalePercent");
    expect(root).toContain("useUiScalePercent");
    expect(root).toContain("transform: `scale(${scale})`");
  });

  it("localizes the new option groups and user-facing controls in Japanese", () => {
    const locale = repoFile("src/CS2RuntimeAssetAuditor/Localization/LocaleJA.cs");

    for (const text of [
      "表示",
      "キャプチャ",
      "高度な設定",
      "UI倍率",
      "詳細キャプチャ時間",
      "通常監視のサンプリング間隔",
      "同時プロファイラーマーカー数"
    ]) {
      expect(locale).toContain(text);
    }
  });
});
