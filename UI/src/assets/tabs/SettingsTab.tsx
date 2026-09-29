import React from "react";
import { ChoiceControl } from "../components/ChoiceControl";
import type { NormalizedUiScanOptions } from "../types";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

type BoolKey = { [K in keyof NormalizedUiScanOptions]: NormalizedUiScanOptions[K] extends boolean ? K : never }[keyof NormalizedUiScanOptions];
type NumKey = { [K in keyof NormalizedUiScanOptions]: NormalizedUiScanOptions[K] extends number ? K : never }[keyof NormalizedUiScanOptions];

const POPULATIONS = ["SameCategory", "BuiltinDlc", "Custom", "SameSourcePack"] as const;

export function SettingsTab({ settings, onChange }: {
  settings: NormalizedUiScanOptions;
  onChange: (settings: NormalizedUiScanOptions) => void;
}): React.JSX.Element {
  const { t } = useText();
  const toggle = (key: BoolKey, labelKey: MessageKey, detailKey: MessageKey) =>
    <div className="apa__setting-row" key={key}>
      <span><strong>{t(labelKey)}</strong><small>{t(detailKey)}</small></span>
      <button type="button" className="apa__button" aria-pressed={settings[key]}
        onClick={() => onChange({ ...settings, [key]: !settings[key] })}>{settings[key] ? t("settings.on") : t("settings.off")}</button>
    </div>;
  const number = (key: NumKey, labelKey: MessageKey, min: number, max: number, step: number) => {
    const label = t(labelKey);
    return <div className="apa__setting-row" key={key} role="group" aria-label={label}>
      <span>{label}</span>
      <div className="apa__choices">
        <button type="button" className="apa__button" disabled={settings[key] <= min} onClick={() => onChange({ ...settings, [key]: Math.max(min, +(settings[key] - step).toFixed(2)) })} aria-label={t("settings.decrease", { label })}>−</button>
        <strong>{settings[key]}</strong>
        <button type="button" className="apa__button" disabled={settings[key] >= max} onClick={() => onChange({ ...settings, [key]: Math.min(max, +(settings[key] + step).toFixed(2)) })} aria-label={t("settings.increase", { label })}>+</button>
      </div>
    </div>;
  };

  return <section className="apa__tab-content" aria-labelledby="apa-settings-title">
    <div className="apa__section-heading"><div><p className="apa__eyebrow">{t("settings.eyebrow")}</p><h2 id="apa-settings-title">{t("settings.title")}</h2></div></div>
    <h3>{t("settings.scanning")}</h3>
    {toggle("collectSubordinateObjects", "settings.subordinate", "settings.subordinateDetail")}
    {toggle("collectNetworkEdges", "settings.edges", "settings.edgesDetail")}
    {toggle("refreshCatalogAtScanStart", "settings.refresh", "settings.refreshDetail")}
    {number("frameBudgetMs", "settings.frameBudget", 0.25, 8, 0.25)}
    {number("progressUpdateMs", "settings.progress", 50, 2000, 50)}
    <h3>{t("settings.analysis")}</h3>
    {toggle("enableHeuristicFindings", "settings.heuristics", "settings.heuristicsDetail")}
    {toggle("enablePeerOutliers", "settings.peers", "settings.peersDetail")}
    {toggle("showNoticeFindings", "settings.notices", "settings.noticesDetail")}
    <ChoiceControl label={t("settings.population")} value={settings.comparisonPopulation}
      choices={POPULATIONS.map(value => [value, t(`settings.population.${value}` as MessageKey)] as const)}
      onChange={value => onChange({ ...settings, comparisonPopulation: value })} />
    <h3>{t("settings.advanced")}</h3>
    {number("pageSize", "settings.pageSize", 25, 200, 25)}
    {number("metadataCacheLimit", "settings.cache", 64, 4096, 64)}
    {number("deepInspectionLimit", "settings.deepLimit", 1, 16, 1)}
    {number("uiScale", "settings.uiScale", 0.75, 1.5, 0.05)}
    <p className="apa__muted">{t("settings.note")}</p>
  </section>;
}
