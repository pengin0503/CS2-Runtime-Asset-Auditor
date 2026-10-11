import React from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { expect, it, vi } from "vitest";
import { ChoiceControl, DROPDOWN_MIN_CHOICES } from "./ChoiceControl";

// Issue #59: filters with many choices use the game's Dropdown; short ones keep the button row.
const SOURCES = [["Any", "Any"], ["Builtin", "Built-in"], ["SubscribedMod", "Subscribed mod"], ["Packaged", "Packaged"], ["Unknown", "Unknown"]] as const;

function render(element: React.ReactElement): ReactTestRenderer {
  let renderer!: ReactTestRenderer;
  act(() => { renderer = create(element); });
  return renderer;
}

const items = (renderer: ReactTestRenderer) => renderer.root.findAll(node => node.type === "button" && node.props["data-dropdown-item"] !== undefined);
const toggle = (renderer: ReactTestRenderer) => renderer.root.find(node => node.type === "button" && node.props["data-dropdown-toggle"] !== undefined);
const text = (node: { children: unknown[] }) => node.children.join("");

it("shows a long filter as a dropdown that reports the chosen value and closes", () => {
  const onChange = vi.fn();
  const renderer = render(<ChoiceControl label="Source" value="Builtin" choices={SOURCES} onChange={onChange} />);

  expect(SOURCES.length).toBeGreaterThanOrEqual(DROPDOWN_MIN_CHOICES);
  expect(text(toggle(renderer))).toBe("Built-in");
  expect(toggle(renderer).props["aria-label"]).toBe("Source");
  expect(items(renderer)).toHaveLength(0);

  act(() => toggle(renderer).props.onClick());
  expect(items(renderer).map(text)).toEqual(SOURCES.map(([, title]) => title));
  expect(items(renderer).filter(item => item.props["data-selected"] === "true").map(text)).toEqual(["Built-in"]);

  act(() => items(renderer)[4].props.onClick());
  expect(onChange).toHaveBeenCalledWith("Unknown");
  expect(items(renderer)).toHaveLength(0);
});

it("choosing the current value again does not report a change", () => {
  const onChange = vi.fn();
  const renderer = render(<ChoiceControl label="Source" value="Builtin" choices={SOURCES} onChange={onChange} />);
  act(() => toggle(renderer).props.onClick());
  act(() => items(renderer)[1].props.onClick());
  expect(onChange).not.toHaveBeenCalled();
  expect(items(renderer)).toHaveLength(0);
});

it("keeps a short choice as a row of buttons", () => {
  const onChange = vi.fn();
  const renderer = render(<ChoiceControl label="Format" value="Json" choices={[["Json", "JSON"], ["Csv", "CSV"]]} onChange={onChange} />);
  const buttons = renderer.root.findAll(node => node.type === "button");
  expect(buttons.map(text)).toEqual(["JSON", "CSV"]);
  expect(buttons.map(button => button.props["aria-pressed"])).toEqual([true, false]);
  act(() => buttons[1].props.onClick());
  expect(onChange).toHaveBeenCalledWith("Csv");
});
