import React from "react";
import { confidenceLabel, metricReasonLabel } from "../text";
import styles from "../profiler.module.scss";

interface MetricBadgeProps {
  confidence: string;
  availability: string;
  reason?: string | null;
}

export function MetricBadge({ confidence, availability, reason }: MetricBadgeProps) {
  const unavailable = availability !== "Available" || confidence === "Unavailable";
  const rawLabel = unavailable ? "Unavailable" : confidence || "Unavailable";
  const label = confidenceLabel(rawLabel);
  const className = unavailable
    ? styles.badgeUnavailable
    : rawLabel === "Full"
      ? styles.badgeFull
      : rawLabel === "Managed"
        ? styles.badgeManaged
        : styles.badgeIndirect;

  const tooltip = unavailable
    ? metricReasonLabel(reason) || "現在のゲーム環境ではこのメトリクスを取得できません。"
    : rawLabel === "Full"
      ? "ランタイムが公開しているメトリクスから直接取得した値です。"
      : rawLabel === "Managed"
        ? "管理コード境界で測定した値です。ワーカーやジョブの負荷は未帰属の場合があります。"
        : "対応するランタイムカウンターから得た間接的な補助指標です。";

  return <span className={`${styles.badge} ${className}`} title={tooltip}>{label}</span>;
}
