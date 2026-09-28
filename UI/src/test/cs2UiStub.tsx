import React from "react";

export function Button({ onSelect, children, src, variant, selected, as: _as, tooltipLabel: _tooltipLabel, ...rest }: any) {
  return (
    <button type="button" data-variant={variant} data-selected={selected ? "true" : "false"} onClick={onSelect} {...rest}>
      {src ? <img src={src} alt="" /> : null}
      {children}
    </button>
  );
}

export function Tooltip({ tooltip, children }: any) {
  return <span title={typeof tooltip === "string" ? tooltip : undefined}>{children}</span>;
}

export function Scrollable({ children, className, vertical, trackVisibility }: any) {
  return <div className={className} data-scrollable={vertical ? "vertical" : "none"} data-track={trackVisibility}>{children}</div>;
}
