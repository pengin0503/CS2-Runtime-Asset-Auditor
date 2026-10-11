import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, beforeAll, describe, expect, it } from "vitest";
import { clearBindingValues, resetTriggeredCalls, setBindingValue, triggeredCalls } from "../test/cs2ApiStub";
import { AssetSection } from "../assets/AssetSection";
import { EMPTY_SNAPSHOT } from "../profiler/bindings";
import { en } from "../i18n/messages";
import { RuntimeAssetAuditorRoot } from "./RuntimeAssetAuditorRoot";
import { RuntimeContextCard, temporalLink } from "../assets/RuntimeContextCard";
import type { CaptureSummaryUi } from "../profiler/bindings";
import type { UiSummary } from "../assets/types";

const capture = { id: "capture", sessionId: "same", startedAtUtc: "2026-09-28T12:00:10Z", completedAtUtc: "2026-09-28T12:00:20Z" } as CaptureSummaryUi;
const summary = { latestAssetSnapshotId: "asset", latestAssetSnapshotStartedAtUtc: "2026-09-28T12:00:00Z", latestAssetSnapshotCompletedAtUtc: "2026-09-28T12:00:05Z" } as UiSummary;

describe("Runtime → Asset investigation", () => {
  afterEach(() => {
    clearBindingValues();
    resetTriggeredCalls();
  });

  beforeAll(() => {
    // The node test environment has no DOM: the panel removes drag listeners from the document on unmount and the
    // asset section debounces its page request with window timers.
    const globals = globalThis as { document?: unknown; window?: unknown };
    globals.document ??= { addEventListener() {}, removeEventListener() {} };
    globals.window ??= { setTimeout: (callback: () => void, ms: number) => setTimeout(callback, ms), clearTimeout: (id: ReturnType<typeof setTimeout>) => clearTimeout(id) };
  });

  it("keeps the selected capture when navigating to Assets", () => {
    const investigated = {
      id: "capture-7", triggerKind: "Manual", triggeredAtSeconds: 3, durationSeconds: 5, discoveredMarkers: 10, capturedMarkers: 10,
      batched: false, coverageRatio: 1, warningCount: 0, profilerOverheadShare: 0.01, warnings: [], correlatedChanges: [],
    } as unknown as CaptureSummaryUi;
    setBindingValue("CS2RuntimeAssetAuditor", "panelVisible", true);
    setBindingValue("CS2RuntimeAssetAuditor", "snapshot", { ...EMPTY_SNAPSHOT, captures: [investigated] });

    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(<RuntimeAssetAuditorRoot />); });
    const press = (label: string) => {
      const button = renderer.root.findAllByType("button").find(item => item.children.join("") === label);
      if (!button) throw new Error("Missing button: " + label);
      act(() => button.props.onClick());
    };
    press(en["nav.runtime"]);
    press(en["nav.captures"]);
    press(en["captures.investigate"]);

    const section = renderer.root.findAllByType(AssetSection).find(instance => instance.props.view !== "overview");
    expect(section?.props.view).toBe("catalog");
    expect(section?.props.capture?.id).toBe("capture-7");
    expect(triggeredCalls()).toContainEqual({ group: "CS2RuntimeAssetAuditor", name: "selectCapture", args: ["capture-7"] });
    act(() => renderer.unmount());
  });

  it("matches only the same session with complete timestamps", () => {
    expect(temporalLink(capture, "same", summary)).toBe("Before");
    expect(temporalLink(capture, "other", summary)).toBeNull();
    expect(temporalLink({ ...capture, completedAtUtc: null }, "same", summary)).toBeNull();
  });

  it("shows a linked snapshot or a scan action with the non-causal notice", () => {
    const linked = renderToStaticMarkup(<RuntimeContextCard capture={capture} sessionId="same" summary={summary} onRunAudit={() => {}} />);
    expect(linked).toContain("asset");
    expect(linked).toContain("Before");
    const missing = renderToStaticMarkup(<RuntimeContextCard capture={capture} sessionId="other" summary={summary} onRunAudit={() => {}} />);
    expect(missing).toContain("Run Asset Audit");
    expect(missing).toContain("not measured per-asset frame/GPU cost");
    expect(missing).not.toContain("perAssetGpuMs");
  });
});
