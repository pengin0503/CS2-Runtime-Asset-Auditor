import React, { useEffect, useMemo, useState } from "react";
import { VirtualAssetTable } from "../components/VirtualAssetTable";
import { ChoiceControl } from "../components/ChoiceControl";
import { AssetDetails } from "../details/AssetDetails";
import {
  DEFAULT_ASSET_QUERY_STATE,
  type AssetPage,
  type AssetQueryState,
  type ExportAssetKey,
  type UiFinding,
} from "../types";

interface AssetsTabProps {
  page: AssetPage;
  query: AssetQueryState;
  onQueryChange: (patch: Partial<AssetQueryState>) => void;
  findings?: UiFinding[];
  onSelectedAssetChange?: (asset: ExportAssetKey | null) => void;
  onDeepInspect?: (renderKey: string) => void;
  onOpenRuntimeCaptures?: () => void;
}

export function AssetsTab({ page, query, onQueryChange, findings = [], onSelectedAssetChange, onDeepInspect, onOpenRuntimeCaptures }: AssetsTabProps): React.JSX.Element {
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const selected = useMemo(() => page.items.find((item) => `${item.prefabType}:${item.prefabId}` === selectedKey) ?? null, [page.items, selectedKey]);

  const selectedPrefabId = selected?.prefabId ?? null;
  const selectedPrefabType = selected?.prefabType ?? null;
  // Depend on the key, not the row object: each published snapshot carries new row objects for the same asset.
  useEffect(() => {
    onSelectedAssetChange?.(selectedPrefabId === null || selectedPrefabType === null
      ? null
      : { prefabId: selectedPrefabId, prefabType: selectedPrefabType });
  }, [onSelectedAssetChange, selectedPrefabId, selectedPrefabType]);

  return (
    <section className="apa__tab-content" aria-labelledby="apa-assets-title">
      <div className="apa__section-heading">
        <div><p className="apa__eyebrow">Bounded result window</p><h2 id="apa-assets-title">Assets</h2></div>
        <span className="apa__muted">{page.totalCount} matching Prefabs</span>
      </div>
      <div className="apa__filters">
        <div className="apa__field apa__field--search"><span>Search</span><input type="search" value={query.searchText} placeholder="Name, Prefab ID, or source / pack" aria-label="Search assets" onChange={(event) => onQueryChange({ searchText: event.currentTarget.value })} /></div>
        <ChoiceControl label="Type" value={query.traitFilter ?? "Any"} choices={[["Any", "Any type"], ["Building", "Building"], ["ServiceBuilding", "Service building"], ["Prop", "Prop"], ["Tree", "Tree"], ["Vehicle", "Vehicle"], ["Network", "Network"]]} onChange={value => onQueryChange({ traitFilter: value === "Any" ? null : value })} />
        <ChoiceControl label="Source" value={query.sourceFilter} choices={[["Any", "Any source"], ["Builtin", "Built-in"], ["SubscribedMod", "Subscribed mod"], ["Packaged", "Packaged"], ["Unknown", "Unknown"]]} onChange={value => onQueryChange({ sourceFilter: value })} />
        <ChoiceControl label="Presence" value={query.presenceFilter ?? "Any"} choices={[["Any", "Any presence"], ["Present", "Present"], ["NotPresentAtSnapshot", "Not present"], ["NotApplicable", "Not applicable"], ["Unknown", "Unknown"]]} onChange={value => onQueryChange({ presenceFilter: value === "Any" ? null : value })} />
        <ChoiceControl label="Sort" value={query.sort} choices={[["DisplayNameAscending", "Name A–Z"], ["DisplayNameDescending", "Name Z–A"], ["PrefabIdAscending", "Prefab ID"], ["InstancesDescending", "Instances"]]} onChange={value => onQueryChange({ sort: value })} />
      </div>
      <VirtualAssetTable page={page} onPageChange={(offset) => onQueryChange({ offset })} onSelect={(asset) => setSelectedKey(`${asset.prefabType}:${asset.prefabId}`)} selectedKey={selected ? `${selected.prefabType}:${selected.prefabId}` : null} />
      <button type="button" className="apa__link-button" onClick={() => onQueryChange(DEFAULT_ASSET_QUERY_STATE)}>Reset filters</button>
      {selected ? <AssetDetails asset={selected} findings={findings} onDeepInspect={onDeepInspect} onOpenRuntimeCaptures={onOpenRuntimeCaptures} /> : null}
    </section>
  );
}
