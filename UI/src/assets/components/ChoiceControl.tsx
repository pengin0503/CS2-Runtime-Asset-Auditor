import React from "react";
import { Button } from "cs2/ui";

export function ChoiceControl<T extends string>({ label, value, choices, onChange }: {
  label: string;
  value: T;
  choices: ReadonlyArray<readonly [T, string]>;
  onChange: (value: T) => void;
}): React.JSX.Element {
  return <div className="apa__field" role="group" aria-label={label}>
    <span>{label}</span>
    <div className="apa__choices">{choices.map(([choice, title]) =>
      <Button as="button" variant="flat" key={choice} className="apa__button" selected={value === choice} aria-pressed={value === choice}
        onSelect={() => onChange(choice)}>{title}</Button>)}</div>
  </div>;
}
