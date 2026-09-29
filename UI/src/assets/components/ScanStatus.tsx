import React from "react";
import { formatObservation } from "../bindings";
import type { ScanStatusData, UiObservation } from "../types";
import { useText, type Translate } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

interface ScanStatusProps {
  scan: ScanStatusData;
  onCancel?: () => void;
}

function translated(t: Translate, key: string, fallback: string): string {
  const text = t(key as MessageKey);
  return text === key ? fallback : text;
}

export function scanStateLabel(state: string, t: Translate): string {
  switch (state) {
    case "WaitingForRuntimeCapture": return t("scan.waiting");
    case "InterruptedByRuntimeCapture": return t("scan.interrupted");
    case "CancellationRequested": return t("scan.cancelling");
    case "Running": return t("scan.inProgress");
    default: return translated(t, `scan.state.${state}`, state);
  }
}

export function ScanStatus({ scan, onCancel }: ScanStatusProps): React.JSX.Element {
  const { t } = useText();
  const hasExactProgress =
    scan.completedItems !== null &&
    scan.totalItems !== null &&
    scan.totalItems > 0;
  const percent = hasExactProgress
    ? Math.max(0, Math.min(100, Math.round((scan.completedItems! / scan.totalItems!) * 100)))
    : null;
  const isActive = scan.state === "Running" || scan.state === "CancellationRequested";
  const stateLabel = scanStateLabel(scan.state, t);

  return (
    <section className="apa__scan-status" aria-label={t("scan.aria")}>
      <div className="apa__scan-heading">
        <div>
          <p className="apa__eyebrow">{stateLabel}</p>
          <h2>{translated(t, `scan.stage.${scan.stage}`, scan.stage)}</h2>
        </div>
        {scan.state === "Running" && onCancel ? (
          <button type="button" className="apa__button apa__button--quiet" onClick={onCancel}>
            {t("scan.cancel")}
          </button>
        ) : null}
      </div>
      {isActive ? (
        <div className="apa__progress-block">
          <div
            role="progressbar"
            aria-label={t("scan.progressAria")}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={percent ?? undefined}
            className={`apa__progress ${percent === null ? "apa__progress--indeterminate" : ""}`}
          >
            {percent !== null ? <span style={{ width: `${percent}%` }} /> : null}
          </div>
          <p className="apa__progress-copy">
            {percent === null
              ? t("scan.stageIndeterminate", { stage: scan.stageNumber, total: scan.totalStages })
              : t("scan.stageProgress", { stage: scan.stageNumber, total: scan.totalStages, done: scan.completedItems ?? 0, items: scan.totalItems ?? 0 })}
          </p>
        </div>
      ) : (
        <p className="apa__progress-copy">
          {scan.state === "Idle" ? t("scan.ready") : stateLabel}
        </p>
      )}
    </section>
  );
}

export function ObservationValue({
  observation,
  label,
}: {
  observation: UiObservation<number>;
  label: string;
}): React.JSX.Element {
  const { t } = useText();
  const value = formatObservation(observation, t);
  return (
    <span className="apa__observation" aria-label={`${label}: ${value}`}>
      {value}
    </span>
  );
}
