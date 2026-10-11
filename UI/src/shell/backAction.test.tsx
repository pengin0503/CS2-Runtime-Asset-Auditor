import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, describe, expect, it } from "vitest";
import { clearBindingValues, resetTriggeredCalls, setBindingValue, triggeredCalls } from "../test/cs2ApiStub";
import { renderedInputConsumers, resetInputConsumers } from "../test/cs2InputStub";
import { RuntimeAssetAuditorRoot } from "./RuntimeAssetAuditorRoot";

// Escape (keyboard) and B (gamepad) reach the UI as the game's "Back" input action, not as DOM keydown events.
describe("closing the panel with the game's Back action", () => {
  afterEach(() => {
    clearBindingValues();
    resetTriggeredCalls();
    resetInputConsumers();
  });

  it("asks the game to hide the panel when Back fires, even without UI focus", () => {
    setBindingValue("CS2RuntimeAssetAuditor", "panelVisible", true);
    renderToStaticMarkup(<RuntimeAssetAuditorRoot />);

    const consumer = renderedInputConsumers().find(registration => "Back" in registration.actions);
    expect(consumer).toBeDefined();
    expect(consumer!.ignoreFocusState).toBe(true);

    consumer!.actions.Back();
    consumer!.actions.Back();

    // A close is always "set hidden", never a toggle, so a repeated Back cannot reopen the panel.
    expect(triggeredCalls()).toEqual([
      { group: "CS2RuntimeAssetAuditor", name: "setPanelVisible", args: [false] },
      { group: "CS2RuntimeAssetAuditor", name: "setPanelVisible", args: [false] },
    ]);
  });

  it("does not consume Back while the panel is closed", () => {
    renderToStaticMarkup(<RuntimeAssetAuditorRoot />);
    expect(renderedInputConsumers().some(registration => "Back" in registration.actions)).toBe(false);
  });
});
