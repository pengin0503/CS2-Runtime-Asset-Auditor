import { describe, expect, it } from "vitest";
import { MIN_PANEL_HEIGHT, MIN_PANEL_WIDTH, clampPanelRect, movePanelRect, resizePanelRect } from "./panelLayout";

const viewport = { width: 1920, height: 1080 };

describe("profiler panel layout", () => {
  it("enforces a usable minimum size", () => {
    const rect = clampPanelRect({ left: 10, top: 10, width: 50, height: 20 }, viewport, 1);
    expect(rect.width).toBe(MIN_PANEL_WIDTH);
    expect(rect.height).toBe(MIN_PANEL_HEIGHT);
  });

  it("keeps the header grabbable after moving off screen", () => {
    const rect = movePanelRect({ left: 100, top: 100, width: 800, height: 600 }, 5000, 5000, viewport, 1);
    expect(rect.left).toBeLessThan(viewport.width);
    expect(rect.top).toBeLessThan(viewport.height);
    const up = movePanelRect({ left: 100, top: 100, width: 800, height: 600 }, 0, -5000, viewport, 1);
    expect(up.top).toBe(0);
  });

  it("converts screen-pixel resize deltas through the UI scale", () => {
    const rect = resizePanelRect({ left: 0, top: 0, width: 800, height: 600 }, 150, 75, viewport, 1.5);
    expect(rect.width).toBeCloseTo(900);
    expect(rect.height).toBeCloseTo(650);
  });

  it("does not resize past the screen edge", () => {
    const rect = resizePanelRect({ left: 1500, top: 800, width: 400, height: 250 }, 4000, 4000, viewport, 1);
    expect(rect.left + rect.width).toBeLessThanOrEqual(viewport.width);
    expect(rect.top + rect.height).toBeLessThanOrEqual(viewport.height);
  });

  it("shrinks an oversized saved layout after a resolution change", () => {
    const rect = clampPanelRect({ left: 0, top: 0, width: 3000, height: 2000 }, viewport, 1);
    expect(rect.width).toBe(1920);
    expect(rect.height).toBe(1080);
  });
});
