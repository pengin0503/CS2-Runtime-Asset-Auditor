import React from "react";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearBindingValues, resetTriggeredCalls, setBindingValue, triggeredCalls } from "../test/cs2ApiStub";
import { EMPTY_HUD_SNAPSHOT } from "../profiler/bindings";
import { ProfilerHud } from "../profiler/components/ProfilerHud";
import { PerformanceAdvisorTab } from "../profiler/tabs/PerformanceAdvisorTab";
import { en } from "../i18n/messages";
import { RuntimeAssetAuditorRoot } from "./RuntimeAssetAuditorRoot";

const GROUP = "CS2RuntimeAssetAuditor";
type Listener = (event: { clientX: number; clientY: number }) => void;
const documentListeners = new Map<string, Listener>();

// The node test environment has no DOM; the panel needs the viewport size and document mouse listeners.
beforeEach(() => {
  const globals = globalThis as { document?: unknown; window?: unknown };
  globals.window = { innerWidth: 1920, innerHeight: 1080, setTimeout, clearTimeout };
  globals.document = {
    addEventListener: (type: string, listener: Listener) => documentListeners.set(type, listener),
    removeEventListener: (type: string) => documentListeners.delete(type),
  };
  setBindingValue(GROUP, "panelVisible", true);
});

afterEach(() => {
  clearBindingValues();
  resetTriggeredCalls();
  documentListeners.clear();
});

function renderPanel(): ReactTestRenderer {
  let renderer!: ReactTestRenderer;
  act(() => { renderer = create(<RuntimeAssetAuditorRoot />); });
  return renderer;
}

const panelOf = (renderer: ReactTestRenderer): ReactTestInstance => renderer.root.findByProps({ role: "dialog" });
const dragTargets = (renderer: ReactTestRenderer) => renderer.root.findAll(node => node.type === "div" && typeof node.props.onMouseDown === "function");

describe("profiler panel behaviour", () => {
  it("switches sections through the tab buttons", () => {
    const renderer = renderPanel();
    expect(renderer.root.findAllByType(PerformanceAdvisorTab)).toHaveLength(0);

    const tab = renderer.root.findAllByType("button").find(button => button.children.join("") === en["nav.advisor"]);
    act(() => tab!.props.onClick());

    const advisor = renderer.root.findAllByType(PerformanceAdvisorTab);
    expect(advisor).toHaveLength(1);
    // Every section renders inside the vanilla Scrollable, the panel's only scroll viewport.
    let parent = advisor[0].parent;
    while (parent && parent.props["data-scrollable"] !== "vertical") parent = parent.parent;
    expect(parent).not.toBeNull();
    // Gameface draws no native scrollbar for overflow containers; Scrollable renders a draggable track.
    expect(parent!.props["data-track"]).toBe("scrollable");
    act(() => renderer.unmount());
  });

  it("applies the configured UI scale to the panel", () => {
    setBindingValue(GROUP, "uiScalePercent", 125);
    const renderer = renderPanel();
    expect(panelOf(renderer).props.style.transform).toBe("scale(1.25)");
    act(() => renderer.unmount());
  });

  it("saves the layout once when a drag ends, not on every mouse move", () => {
    setBindingValue(GROUP, "panelLayout", { custom: true, left: 100, top: 80, width: 900, height: 600 });
    const renderer = renderPanel();
    const resizeGrip = dragTargets(renderer).at(-1)!;

    act(() => resizeGrip.props.onMouseDown({ button: 0, clientX: 1000, clientY: 680, preventDefault() {}, stopPropagation() {} }));
    act(() => documentListeners.get("mousemove")!({ clientX: 1040, clientY: 700 }));
    act(() => documentListeners.get("mousemove")!({ clientX: 1100, clientY: 720 }));
    expect(triggeredCalls().filter(call => call.name === "setPanelLayout")).toHaveLength(0);

    act(() => documentListeners.get("mouseup")!({ clientX: 1100, clientY: 720 }));

    expect(triggeredCalls().filter(call => call.name === "setPanelLayout")).toEqual([
      { group: GROUP, name: "setPanelLayout", args: [100, 80, 1000, 640] },
    ]);
    expect(documentListeners.size).toBe(0);
    act(() => renderer.unmount());
  });

  it("opens from the top-left launcher, a floating icon button without a text label", () => {
    const onToggle = vi.fn();
    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(<ProfilerHud snapshot={EMPTY_HUD_SNAPSHOT} panelVisible={false} onToggle={onToggle} />); });
    const button = renderer.root.findByType("button");

    act(() => button.props.onClick());

    expect(onToggle).toHaveBeenCalledTimes(1);
    expect(button.props["data-variant"]).toBe("floating");
    expect(button.findAll(node => typeof node === "string" || node.children.some(child => typeof child === "string"))).toHaveLength(0);
    act(() => renderer.unmount());
  });
});
