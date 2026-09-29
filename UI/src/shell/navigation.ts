import type { MessageKey } from "../i18n/messages";

export const TOP_SECTIONS = [
  { id: "overview", labelKey: "nav.overview" },
  { id: "runtime", labelKey: "nav.runtime" },
  { id: "assets", labelKey: "nav.assets" },
  { id: "advisor", labelKey: "nav.advisor" },
  { id: "diagnostics", labelKey: "nav.diagnostics" }
] as const satisfies ReadonlyArray<{ id: string; labelKey: MessageKey }>;

export const RUNTIME_SECTIONS = [
  { id: "systems", labelKey: "nav.systems" },
  { id: "mods", labelKey: "nav.mods" },
  { id: "pathfinding", labelKey: "nav.pathfinding" },
  { id: "timeline", labelKey: "nav.timeline" },
  { id: "captures", labelKey: "nav.captures" }
] as const satisfies ReadonlyArray<{ id: string; labelKey: MessageKey }>;

export const ASSET_SECTIONS = [
  { id: "catalog", labelKey: "nav.catalog" },
  { id: "census", labelKey: "nav.census" },
  { id: "findings", labelKey: "nav.findings" },
  { id: "compare", labelKey: "nav.compare" },
  { id: "settings", labelKey: "nav.settings" }
] as const satisfies ReadonlyArray<{ id: string; labelKey: MessageKey }>;

export type TopSection = typeof TOP_SECTIONS[number]["id"];
export type RuntimeSection = typeof RUNTIME_SECTIONS[number]["id"];
export type AssetSectionName = typeof ASSET_SECTIONS[number]["id"];
