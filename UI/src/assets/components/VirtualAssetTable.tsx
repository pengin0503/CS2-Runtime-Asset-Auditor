import React from "react";
import { formatCountKind, formatObservation } from "../bindings";
import { MAX_ASSET_PAGE_SIZE, type AssetPage, type AssetRow } from "../types";

interface VirtualAssetTableProps {
  page: AssetPage;
  onPageChange?: (offset: number) => void;
  onSelect?: (asset: AssetRow) => void;
  selectedKey?: string | null;
}

export function VirtualAssetTable({ page, onPageChange, onSelect, selectedKey }: VirtualAssetTableProps): React.JSX.Element {
  const items = page.items.slice(0, Math.min(page.limit, MAX_ASSET_PAGE_SIZE));
  const start = page.totalCount === 0 ? 0 : page.offset + 1;
  const end = page.offset + items.length;
  const previousOffset = Math.max(0, page.offset - Math.max(1, page.limit));
  const nextOffset = page.offset + items.length;

  return <section className="apa__table-shell" aria-label="Asset results">
    <div role="list" className="apa__asset-list">
      {items.length === 0 ? <p>No assets match this query.</p> : items.map(item => (
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
            <span>Instances: {formatObservation(item.instances)} ({formatCountKind(item.countKind)})</span>
            <span>Geometry: {item.renderCoverage === "Available"
              ? formatObservation(item.lod0Vertices ?? { availability: "NotScanned" })
              : item.renderCoverage === "NotScanned" || !item.renderCoverage ? "Not scanned" : item.renderCoverage}</span>
            <span>Findings: {item.findingCount ?? 0}</span><span>{item.presence}</span>
          </div>
        </div>
      ))}
    </div>
    <footer className="apa__table-footer">
      <span>{`${start}–${end} of ${page.totalCount}`}</span>
      <div className="apa__pager">
        <button type="button" className="apa__button" disabled={page.offset <= 0 || !onPageChange} onClick={() => onPageChange?.(previousOffset)}>Previous</button>
        <button type="button" className="apa__button" disabled={nextOffset >= page.totalCount || !onPageChange} onClick={() => onPageChange?.(nextOffset)}>Next</button>
      </div>
    </footer>
  </section>;
}
