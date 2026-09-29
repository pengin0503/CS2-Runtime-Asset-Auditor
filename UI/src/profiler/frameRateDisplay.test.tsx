import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { expect, it } from "vitest";
import { OverviewTab } from "./tabs/OverviewTab";
import { EMPTY_SNAPSHOT } from "./bindings";
import { useGameLocale } from "../test/locale";

useGameLocale("en-US");

const render = (global: Partial<typeof EMPTY_SNAPSHOT.global>) => renderToStaticMarkup(
  <OverviewTab
    snapshot={{ ...EMPTY_SNAPSHOT, global: { ...EMPTY_SNAPSHOT.global, available: true, ...global } }}
    onManualCapture={() => undefined}
    onExport={() => undefined}
    exportResult=""
  />
);

it("shows the measured frame rate with the frame times that an average hides", () => {
  const html = render({ framesPerSecond: 42.46, frameMsMedian: 22.1, frameMsP95: 48.3 });
  expect(html).toContain("42.5 fps");
  expect(html).toContain("Median 22.10 ms / P95 48.30 ms");
});

it("shows an unmeasured frame rate as unavailable, never as zero", () => {
  const html = render({ framesPerSecond: null, frameMsMedian: null, frameMsP95: null });
  expect(html).not.toContain("0.0 fps");
  expect(html).toContain("Unavailable");
});
