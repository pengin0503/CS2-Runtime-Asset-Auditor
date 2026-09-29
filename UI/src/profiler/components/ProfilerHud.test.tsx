import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { expect, it } from "vitest";
import { ProfilerHud } from "./ProfilerHud";
import { useGameLocale } from "../../test/locale";

useGameLocale("ja-JP");

it("renders a native floating launcher with a Japanese status tooltip", () => {
  const html = renderToStaticMarkup(
    <ProfilerHud
      snapshot={{ selectedSpeed: 4, actualSpeed: 2.5, state: "DeepCapture", isDeepCapture: true, framesPerSecond: 28.04, frameMsP95: 51.2 }}
      panelVisible={false}
      onToggle={() => {}}
    />
  );
  expect(html).toContain('data-variant="floating"');
  expect(html).toContain("CS2 Runtime Asset Auditor");
  expect(html).toContain("指定速度 4×");
  expect(html).toContain("詳細キャプチャ中");
  expect(html).toContain("28.0 fps / フレーム時間 P95 51.20 ms");
});
