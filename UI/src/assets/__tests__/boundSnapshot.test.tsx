import React from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import { useAuditorSnapshot } from "../bindings";
import { EMPTY_UI_SNAPSHOT, type UiSnapshot } from "../types";

const native = vi.hoisted(() => ({ raw: "{}" }));
vi.mock("cs2/api", () => ({
  bindValue: (_group: string, _name: string, fallback: string) => ({ value: fallback }),
  useValue: () => native.raw,
  trigger: () => {},
}));

describe("live Asset snapshot binding", () => {
  it("keeps the same snapshot object across unrelated renders and changes it when the binding changes", () => {
    const seen: UiSnapshot[] = [];
    native.raw = JSON.stringify({ ...EMPTY_UI_SNAPSHOT, summary: { ...EMPTY_UI_SNAPSHOT.summary, catalogCount: 1 } });
    function Probe({ tick }: { tick: number }) {
      const snapshot = useAuditorSnapshot();
      seen.push(snapshot);
      return <span>{tick}: {snapshot.summary.catalogCount}</span>;
    }
    let renderer!: ReactTestRenderer;
    act(() => { renderer = create(<Probe tick={0} />); });
    act(() => { renderer.update(<Probe tick={1} />); });
    expect(seen[1]).toBe(seen[0]);

    native.raw = JSON.stringify({ ...EMPTY_UI_SNAPSHOT, summary: { ...EMPTY_UI_SNAPSHOT.summary, catalogCount: 2 } });
    act(() => { renderer.update(<Probe tick={2} />); });
    expect(seen[2]).not.toBe(seen[1]);
    expect(seen[2].summary.catalogCount).toBe(2);
    act(() => renderer.unmount());
  });
});
