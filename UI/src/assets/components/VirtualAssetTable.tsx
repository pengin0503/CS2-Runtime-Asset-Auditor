import React from "react";
import { formatCountKind, formatObservation } from "../bindings";
import { MAX_ASSET_PAGE_SIZE, type AssetPage, type AssetRow } from "../types";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

interface VirtualAssetTableProps {
  page: AssetPage;
  onPageChange?: (offset: number) => void;
  onSelect?: (asset: AssetRow) => void;
  selectedKey?: string | null;
}

export function VirtualAssetTable({ page, onPageChange, onSelect, selectedKey }: VirtualAssetTableProps): React.JSX.Element {
  const { t } = useText();
  const items = page.items.slice(0, Math.min(page.limit, MAX_ASSET_PAGE_SIZE));
  const start = page.totalCount === 0 ? 0 : page.offset + 1;
  const end = page.offset + items.length;
  const previousOffset = Math.max(0, page.offset - Math.max(1, page.limit));
  const nextOffset = page.offset + items.length;
  const presenceLabel = (presence: string) => {
    const key = `atab.presence.${presence}` as MessageKey;
    return t(key) === key ? presence : t(key);
  };

  return <section className="apa__table-shell" aria-label={t("table.aria")}>
    <div role="list" className="apa__asset-list">
      {items.length === 0 ? <p>{t("table.empty")}</p> : items.map(item => (
        <div role="listitem" className="apa__asset-row" key={`${item.prefabType}:${item.prefabId}`}
          data-selected={selectedKey === `${item.prefabType}:${item.prefabId}`}>
          <div className="apa__asset-main">
            <button type="button" className="apa__link-button" onClick={() => onSelect?.(item)} disabled={!onSelect}>
              <strong>{item.displayName}</strong>
            </button>
            <small className="apa__asset-id">{item.prefabId}</small>
          </div>
          <div className="apa__asset-facts">
            <span>{item.sourceLabel} · {item.prefabType}</span>
            <span>{t("table.instances", { value: formatObservation(item.instances, t), kind: formatCountKind(item.countKind, t) })}</span>
            <span>{t("table.geometry", { value: item.renderCoverage === "Available"
              ? formatObservation(item.lod0Vertices ?? { availability: "NotScanned" }, t)
              : item.renderCoverage === "NotScanned" || !item.renderCoverage ? t("obs.notScanned") : item.renderCoverage })}</span>
            <span>{t("table.findings", { count: item.findingCount ?? 0 })}</span><span>{presenceLabel(item.presence)}</span>
          </div>
        </div>
      ))}
    </div>
    <footer className="apa__table-footer">
      <span>{t("table.range", { start, end, total: page.totalCount })}</span>
      <div className="apa__pager">
        <button type="button" className="apa__button" disabled={page.offset <= 0 || !onPageChange} onClick={() => onPageChange?.(previousOffset)}>{t("table.previous")}</button>
        <button type="button" className="apa__button" disabled={nextOffset >= page.totalCount || !onPageChange} onClick={() => onPageChange?.(nextOffset)}>{t("table.next")}</button>
      </div>
    </footer>
  </section>;
}
