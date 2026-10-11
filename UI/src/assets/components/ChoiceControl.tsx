import React from "react";
import { Button, Dropdown, DropdownItem, DropdownToggle } from "cs2/ui";

/** From this many choices on, the control is the game's dropdown instead of a row of buttons. */
export const DROPDOWN_MIN_CHOICES = 4;

export function ChoiceControl<T extends string>({ label, value, choices, onChange }: {
  label: string;
  value: T;
  choices: ReadonlyArray<readonly [T, string]>;
  onChange: (value: T) => void;
}): React.JSX.Element {
  if (choices.length >= DROPDOWN_MIN_CHOICES) {
    const current = choices.find(([choice]) => choice === value)?.[1] ?? value;
    return <div className="apa__field" role="group" aria-label={label}>
      <span>{label}</span>
      <Dropdown content={choices.map(([choice, title]) =>
        <DropdownItem<T> key={choice} value={choice} selected={value === choice} onChange={onChange}>{title}</DropdownItem>)}>
        <DropdownToggle className="apa__dropdown" aria-label={label}>{current}</DropdownToggle>
      </Dropdown>
    </div>;
  }
  return <div className="apa__field" role="group" aria-label={label}>
    <span>{label}</span>
    <div className="apa__choices">{choices.map(([choice, title]) =>
      <Button as="button" variant="flat" key={choice} className="apa__button" selected={value === choice} aria-pressed={value === choice}
        onSelect={() => onChange(choice)}>{title}</Button>)}</div>
  </div>;
}
