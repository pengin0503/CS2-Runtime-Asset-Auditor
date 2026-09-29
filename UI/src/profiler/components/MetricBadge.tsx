import React from "react";
import { confidenceLabel, metricReasonLabel } from "../text";
import { useText } from "../../i18n/locale";
import styles from "../profiler.module.scss";

interface MetricBadgeProps {
  confidence: string;
  availability: string;
  reason?: string | null;
}

export function MetricBadge({ confidence, availability, reason }: MetricBadgeProps) {
  const { locale, t } = useText();
  const unavailable = availability !== "Available" || confidence === "Unavailable";
  const rawLabel = unavailable ? "Unavailable" : confidence || "Unavailable";
  const label = confidenceLabel(rawLabel, locale);
  const className = unavailable
    ? styles.badgeUnavailable
    : rawLabel === "Full"
      ? styles.badgeFull
      : rawLabel === "Managed"
        ? styles.badgeManaged
        : styles.badgeIndirect;

  const tooltip = unavailable
    ? metricReasonLabel(reason, locale) || t("badge.unavailable")
    : rawLabel === "Full"
      ? t("badge.full")
      : rawLabel === "Managed"
        ? t("badge.managed")
        : t("badge.indirect");

  return <span className={`${styles.badge} ${className}`} title={tooltip}>{label}</span>;
}
