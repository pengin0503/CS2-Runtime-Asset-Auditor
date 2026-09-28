import React from "react";
import { ChoiceControl } from "../components/ChoiceControl";
import type { NormalizedUiScanOptions } from "../types";

type BoolKey = { [K in keyof NormalizedUiScanOptions]: NormalizedUiScanOptions[K] extends boolean ? K : never }[keyof NormalizedUiScanOptions];
type NumKey = { [K in keyof NormalizedUiScanOptions]: NormalizedUiScanOptions[K] extends number ? K : never }[keyof NormalizedUiScanOptions];

export function SettingsTab({ settings, onChange }: {
  settings: NormalizedUiScanOptions;
  onChange: (settings: NormalizedUiScanOptions) => void;
}): React.JSX.Element {
  const toggle = (key: BoolKey, label: string, detail: string) =>
    <div className="apa__setting-row" key={key}>
      <span><strong>{label}</strong><small>{detail}</small></span>
      <button type="button" className="apa__button" aria-pressed={settings[key]}
        onClick={() => onChange({ ...settings, [key]: !settings[key] })}>{settings[key] ? "On" : "Off"}</button>
    </div>;
  const number = (key: NumKey, label: string, min: number, max: number, step: number) =>
    <div className="apa__setting-row" key={key} role="group" aria-label={label}>
      <span>{label}</span>
      <div className="apa__choices">
        <button type="button" className="apa__button" disabled={settings[key] <= min} onClick={() => onChange({ ...settings, [key]: Math.max(min, +(settings[key] - step).toFixed(2)) })} aria-label={`Decrease ${label}`}>−</button>
        <strong>{settings[key]}</strong>
        <button type="button" className="apa__button" disabled={settings[key] >= max} onClick={() => onChange({ ...settings, [key]: Math.min(max, +(settings[key] + step).toFixed(2)) })} aria-label={`Increase ${label}`}>+</button>
      </div>
    </div>;

  return <section className="apa__tab-content" aria-labelledby="apa-settings-title">
    <div className="apa__section-heading"><div><p className="apa__eyebrow">Bounded and snapshot-aware</p><h2 id="apa-settings-title">Settings</h2></div></div>
    <h3>Scanning</h3>
    {toggle("collectSubordinateObjects", "Collect subordinate objects", "Disabled collection is recorded as Not scanned, never zero.")}
    {toggle("collectNetworkEdges", "Collect network edges", "Applies to the next Census.")}
    {toggle("refreshCatalogAtScanStart", "Refresh catalog at scan start", "Applies to the next scan.")}
    {number("frameBudgetMs", "Managed frame budget (ms)", 0.25, 8, 0.25)}
    {number("progressUpdateMs", "Progress update interval (ms)", 50, 2000, 50)}
    <h3>Analysis</h3>
    {toggle("enableHeuristicFindings", "Heuristic findings", "Potential issues remain evidence-based and versioned.")}
    {toggle("enablePeerOutliers", "Peer-outlier analysis", "Requires a sufficient comparable population.")}
    {toggle("showNoticeFindings", "Show Notice findings", "Display informational findings in Warnings.")}
    <ChoiceControl label="Comparison population" value={settings.comparisonPopulation}
      choices={[["SameCategory", "Same category"], ["BuiltinDlc", "Vanilla / DLC"], ["Custom", "Custom assets"], ["SameSourcePack", "Same source pack"]]}
      onChange={value => onChange({ ...settings, comparisonPopulation: value })} />
    <h3>Advanced</h3>
    {number("pageSize", "Asset page size", 25, 200, 25)}
    {number("metadataCacheLimit", "Metadata cache limit", 64, 4096, 64)}
    {number("deepInspectionLimit", "Deep Inspection limit", 1, 16, 1)}
    {number("uiScale", "UI scale", 0.75, 1.5, 0.05)}
    <p className="apa__muted">Unsafe numeric values are clamped before they are sent to the game. Collection changes apply to the next Census.</p>
  </section>;
}
