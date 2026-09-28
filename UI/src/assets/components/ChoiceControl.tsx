import React from "react";

export function ChoiceControl<T extends string>({ label, value, choices, onChange }: {
  label: string;
  value: T;
  choices: ReadonlyArray<readonly [T, string]>;
  onChange: (value: T) => void;
}): React.JSX.Element {
  return <div className="apa__field" role="group" aria-label={label}>
    <span>{label}</span>
    <div className="apa__choices">{choices.map(([choice, title]) =>
      <button key={choice} type="button" className="apa__button" aria-pressed={value === choice}
        onClick={() => onChange(choice)}>{title}</button>)}</div>
  </div>;
}
