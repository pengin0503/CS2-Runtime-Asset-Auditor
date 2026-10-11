import React from "react";

export function Button({ onSelect, children, src, variant, selected, tinted: _tinted, as: _as, tooltipLabel: _tooltipLabel, ...rest }: any) {
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

export function Icon({ src, className, children }: any) {
  return <span className={className} data-icon={src}>{children}</span>;
}

// Mirrors the vanilla dropdown's behaviour that the mod relies on: the toggle opens and closes the menu, the items
// exist only while it is open, and choosing an item reports it (or onToggleSelected for the selected one) and closes.
const DropdownContext = React.createContext({ visible: false, toggle: () => {}, hide: () => {} });

export function Dropdown({ content, children, onToggle }: any) {
  const [visible, setVisible] = React.useState(false);
  const context = React.useMemo(() => ({
    visible,
    toggle: () => { setVisible(!visible); onToggle?.(!visible); },
    hide: () => { setVisible(false); onToggle?.(false); },
  }), [visible, onToggle]);
  return <DropdownContext.Provider value={context}>
    {children}
    {visible ? <div data-dropdown-menu="true">{content}</div> : null}
  </DropdownContext.Provider>;
}

export function DropdownToggle({ children, showHint: _showHint, ...rest }: any) {
  const dropdown = React.useContext(DropdownContext);
  return <Button {...rest} data-dropdown-toggle={dropdown.visible ? "open" : "closed"} onSelect={dropdown.toggle}>{children}</Button>;
}

export function DropdownItem({ value, selected, disabled, onChange, onToggleSelected, closeOnSelect = true, children, focusKey: _focusKey, ...rest }: any) {
  const dropdown = React.useContext(DropdownContext);
  const select = () => {
    if (selected) onToggleSelected?.(value);
    else onChange?.(value);
    if (closeOnSelect) dropdown.hide();
  };
  return <button type="button" data-dropdown-item={String(value)} data-selected={selected ? "true" : "false"} disabled={disabled} onClick={select} {...rest}>{children}</button>;
}
