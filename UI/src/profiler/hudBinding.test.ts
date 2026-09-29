import { expect, it } from "vitest";
import { EMPTY_HUD_SNAPSHOT, useProfilerHudSnapshot } from "./bindings";

it("exposes a dedicated lightweight HUD snapshot binding", () => {
  expect(EMPTY_HUD_SNAPSHOT).toEqual({
    selectedSpeed: null,
    actualSpeed: null,
    state: "Monitoring",
    isDeepCapture: false,
    framesPerSecond: null,
    frameMsP95: null
  });
  expect(typeof useProfilerHudSnapshot).toBe("function");
});
