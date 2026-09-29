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
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

const TYPE_CHOICES = ["Any", "Building", "ServiceBuilding", "Prop", "Tree", "Vehicle", "Network"] as const;
const SOURCE_CHOICES = ["Any", "Builtin", "SubscribedMod", "Packaged", "Unknown"] as const;
const PRESENCE_CHOICES = ["Any", "Present", "NotPresentAtSnapshot", "NotApplicable", "Unknown"] as const;
const SORT_CHOICES = ["DisplayNameAscending", "DisplayNameDescending", "PrefabIdAscending", "InstancesDescending"] as const;

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
  const { t } = useText();
  const choices = <T extends string>(prefix: string, values: readonly T[]) =>
    values.map(value => [value, t(`${prefix}.${value}` as MessageKey)] as const);
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
        <div><p className="apa__eyebrow">{t("atab.eyebrow")}</p><h2 id="apa-assets-title">{t("atab.title")}</h2></div>
        <span className="apa__muted">{t("atab.matching", { count: page.totalCount })}</span>
      </div>
      <div className="apa__filters">
        <div className="apa__field apa__field--search"><span>{t("atab.search")}</span><input type="search" value={query.searchText} placeholder={t("atab.searchPlaceholder")} aria-label={t("atab.searchAria")} onChange={(event) => onQueryChange({ searchText: event.currentTarget.value })} /></div>
        <ChoiceControl label={t("atab.type")} value={query.traitFilter ?? "Any"} choices={choices("atab.type", TYPE_CHOICES)} onChange={value => onQueryChange({ traitFilter: value === "Any" ? null : value })} />
        <ChoiceControl label={t("atab.source")} value={query.sourceFilter} choices={choices("atab.source", SOURCE_CHOICES)} onChange={value => onQueryChange({ sourceFilter: value })} />
        <ChoiceControl label={t("atab.presence")} value={query.presenceFilter ?? "Any"} choices={choices("atab.presence", PRESENCE_CHOICES)} onChange={value => onQueryChange({ presenceFilter: value === "Any" ? null : value })} />
        <ChoiceControl label={t("atab.sort")} value={query.sort} choices={choices("atab.sort", SORT_CHOICES)} onChange={value => onQueryChange({ sort: value })} />
      </div>
      <VirtualAssetTable page={page} onPageChange={(offset) => onQueryChange({ offset })} onSelect={(asset) => setSelectedKey(`${asset.prefabType}:${asset.prefabId}`)} selectedKey={selected ? `${selected.prefabType}:${selected.prefabId}` : null} />
      <button type="button" className="apa__link-button" onClick={() => onQueryChange(DEFAULT_ASSET_QUERY_STATE)}>{t("atab.reset")}</button>
      {selected ? <AssetDetails asset={selected} findings={findings} onDeepInspect={onDeepInspect} onOpenRuntimeCaptures={onOpenRuntimeCaptures} /> : null}
    </section>
  );
}
