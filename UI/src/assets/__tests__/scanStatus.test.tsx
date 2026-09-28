import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { ScanStatus } from "../components/ScanStatus";

describe("ScanStatus", () => {
  it("surfaces queued and interrupted scans without treating either as completed", () => {
    const queued = renderToStaticMarkup(<ScanStatus scan={{ state: "WaitingForRuntimeCapture", stage: "Idle", stageNumber: 0, totalStages: 8, completedItems: null, totalItems: null, queuedBecauseRuntimeCapture: true }} />);
    const interrupted = renderToStaticMarkup(<ScanStatus scan={{ state: "InterruptedByRuntimeCapture", stage: "Preparing", stageNumber: 1, totalStages: 8, completedItems: null, totalItems: null, interruptedByRuntimeCapture: true }} />);
    expect(queued).toContain("Waiting for Runtime capture to finish");
    expect(interrupted).toContain("Interrupted by Runtime capture — restart scan");
    expect(interrupted).not.toContain("Completed");
  });
  it("shows exact item progress when the total is known", () => {
    const html = renderToStaticMarkup(
      <ScanStatus
        scan={{
          state: "Running",
          stage: "Processing catalog",
          stageNumber: 3,
          totalStages: 8,
          completedItems: 24,
          totalItems: 80,
        }}
      />,
    );

    expect(html).toContain("24 of 80");
    expect(html).toContain('aria-valuenow="30"');
    expect(html).not.toContain("Indeterminate");
  });

  it("shows an indeterminate progress state when the total is unknown", () => {
    const html = renderToStaticMarkup(
      <ScanStatus
        scan={{
          state: "Running",
          stage: "Capturing object census",
          stageNumber: 4,
          totalStages: 8,
          completedItems: null,
          totalItems: null,
        }}
      />,
    );

    expect(html).toContain("Capturing object census");
    expect(html).toContain("Indeterminate");
    expect(html).not.toContain("aria-valuenow");
  });
});
