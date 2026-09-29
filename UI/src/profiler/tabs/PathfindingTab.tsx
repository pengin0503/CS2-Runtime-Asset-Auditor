import React from "react";
import type { UiMetricRow } from "../bindings";
import { formatMetricValue, shortMetricName } from "../format";
import { MetricBadge } from "../components/MetricBadge";
import { metricReasonLabel } from "../text";
import { useText } from "../../i18n/locale";
import styles from "../profiler.module.scss";

export function PathfindingTab({ metrics }: { metrics: UiMetricRow[] }) {
  const { locale, t } = useText();
  if (!metrics?.length) {
    return <p className={styles.empty}>{t("pathfinding.empty")}</p>;
  }

  return (
    <div className={styles.tabBody}>
      <p className={styles.explainer}>{t("pathfinding.explainer")}</p>
      <div className={styles.metricList}>
        {metrics.map(metric => {
          const reason = metricReasonLabel(metric.reason, locale);
          return (
            <div className={styles.metricRow} key={metric.id}>
              <div className={styles.metricIdentity}>
                <strong>{shortMetricName(metric.id)}</strong>
                {reason && <small title={reason}>{reason}</small>}
              </div>
              <span className={styles.metricValue}>{formatMetricValue(metric)}</span>
              <MetricBadge confidence={metric.confidence} availability={metric.availability} reason={metric.reason} />
            </div>
          );
        })}
      </div>
    </div>
  );
}
