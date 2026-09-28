import React from "react";
import { formatObservation } from "../bindings";
import type { ScanStatusData, UiObservation } from "../types";

interface ScanStatusProps {
  scan: ScanStatusData;
  onCancel?: () => void;
}

export function ScanStatus({ scan, onCancel }: ScanStatusProps): React.JSX.Element {
  const hasExactProgress =
    scan.completedItems !== null &&
    scan.totalItems !== null &&
    scan.totalItems > 0;
  const percent = hasExactProgress
    ? Math.max(0, Math.min(100, Math.round((scan.completedItems! / scan.totalItems!) * 100)))
    : null;
  const isActive = scan.state === "Running" || scan.state === "CancellationRequested";
  const stateLabel =
    scan.state === "WaitingForRuntimeCapture"
      ? "Waiting for Runtime capture to finish"
      : scan.state === "InterruptedByRuntimeCapture"
        ? "Interrupted by Runtime capture — restart scan"
        : scan.state === "CancellationRequested"
      ? "Cancelling"
      : scan.state === "Running"
        ? "In progress"
        : scan.state;

  return (
    <section className="apa__scan-status" aria-label="Scan status">
      <div className="apa__scan-heading">
        <div>
          <p className="apa__eyebrow">{stateLabel}</p>
          <h2>{scan.stage}</h2>
        </div>
        {scan.state === "Running" && onCancel ? (
          <button type="button" className="apa__button apa__button--quiet" onClick={onCancel}>
            Cancel scan
          </button>
        ) : null}
      </div>
      {isActive ? (
        <div className="apa__progress-block">
          <div
            role="progressbar"
            aria-label="Scan progress"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={percent ?? undefined}
            className={`apa__progress ${percent === null ? "apa__progress--indeterminate" : ""}`}
          >
            {percent !== null ? <span style={{ width: `${percent}%` }} /> : null}
          </div>
          <p className="apa__progress-copy">
            {percent === null
              ? `Stage ${scan.stageNumber} of ${scan.totalStages} · Indeterminate`
              : `Stage ${scan.stageNumber} of ${scan.totalStages} · ${scan.completedItems} of ${scan.totalItems}`}
          </p>
        </div>
      ) : (
        <p className="apa__progress-copy">
          {scan.state === "Idle" ? "Ready when you are." : stateLabel}
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
  return (
    <span className="apa__observation" aria-label={`${label}: ${formatObservation(observation)}`}>
      {formatObservation(observation)}
    </span>
  );
}
