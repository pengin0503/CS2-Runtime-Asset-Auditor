export const TOP_SECTIONS = [
  { id: "overview", label: "概要" },
  { id: "runtime", label: "ランタイム" },
  { id: "assets", label: "アセット" },
  { id: "advisor", label: "改善提案" },
  { id: "diagnostics", label: "診断" }
] as const;

export const RUNTIME_SECTIONS = [
  { id: "systems", label: "システム" },
  { id: "mods", label: "MOD" },
  { id: "pathfinding", label: "経路探索" },
  { id: "timeline", label: "タイムライン" },
  { id: "captures", label: "キャプチャ" }
] as const;

export const ASSET_SECTIONS = [
  { id: "catalog", label: "カタログ" },
  { id: "census", label: "Census" },
  { id: "findings", label: "所見" },
  { id: "compare", label: "比較" },
  { id: "settings", label: "設定" }
] as const;

export type TopSection = typeof TOP_SECTIONS[number]["id"];
export type RuntimeSection = typeof RUNTIME_SECTIONS[number]["id"];
export type AssetSectionName = typeof ASSET_SECTIONS[number]["id"];
