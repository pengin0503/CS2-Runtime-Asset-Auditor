import type { Locale } from "../i18n/locale";
import type { UiFinding } from "./types";

// Finding titles and explanations are produced in English by the versioned rule set (and exported as such).
// The Japanese UI shows these translations for known rule IDs and the original text otherwise.
const FINDINGS_JA: Record<string, { title: string; explanation: string }> = {
  "APA-INT-001": {
    title: "必須の描画参照を解決できません",
    explanation: "必須の描画参照を解決できませんでした。これは構造上の根拠であり、性能に関するヒューリスティックではありません。"
  },
  "APA-LOD-001": {
    title: "下位 LOD が見つかりません",
    explanation: "下位 LOD は観察されませんでした。これは観察結果であり、それだけで性能上の欠陥とはみなしません。"
  },
  "APA-LOD-002": {
    title: "LOD の頂点削減が弱い",
    explanation: "下位 LOD が LOD0 の頂点を大きな割合で保持しています（バージョン管理されたヒューリスティックによる判定）。実行時のボトルネックを証明するものではありません。"
  },
  "APA-EXP-001": {
    title: "スナップショット内での配置数が多い",
    explanation: "このアセットは現在の都市スナップショットに多数配置されています。アセットが重い場合は配置数がコストを増幅しますが、配置数だけでは描画コストやボトルネックを証明しません。"
  },
  "APA-TEX-001": {
    title: "テクスチャのメタデータを取得できません",
    explanation: "テクスチャのメタデータを確実に読み取れませんでした。失敗はこのテクスチャに限られ、無関係なアセットの分析が失敗したことを意味しません。"
  },
  "APA-PEER-001": {
    title: "同種アセットより LOD0 頂点数が極端に多い",
    explanation: "選択した比較対象の同種 Prefab と比べて LOD0 頂点数が極端に多くなっています。アセットの複雑さの比較であり、フレーム時間や GPU 負荷の測定ではありません。"
  },
  "APA-PEER-002": {
    title: "同種アセットよりテクスチャ容量が極端に大きい",
    explanation: "選択した比較対象の同種 Prefab と比べて推定論理テクスチャ容量が極端に大きくなっています。実測の VRAM 常駐量ではありません。"
  }
};

export function findingTitle(finding: UiFinding, locale: Locale): string {
  return locale === "ja" ? FINDINGS_JA[finding.ruleId]?.title ?? finding.title : finding.title;
}

export function findingExplanation(finding: UiFinding, locale: Locale): string {
  return locale === "ja" ? FINDINGS_JA[finding.ruleId]?.explanation ?? finding.explanation : finding.explanation;
}
