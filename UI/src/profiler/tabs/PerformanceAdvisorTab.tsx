import React, { useState } from "react";
import { Button } from "cs2/ui";
import { AdvisorAction, AdvisorChange, AdvisorExperiment, AdvisorRecommendation, AdvisorUiState, CaptureSummaryUi, EMPTY_ADVISOR } from "../bindings";
import { advisorTextLabel } from "../text";
import { useText, type Locale, type Translate } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";
import styles from "../profiler.module.scss";

const GROUPS: Array<{ id: string; labelKey: MessageKey; include: (r: AdvisorRecommendation) => boolean }> = [
  { id: "high", labelKey: "advisor.group.high", include: r => r.direction === "LowerRecommended" && r.priority === "High" },
  { id: "medium", labelKey: "advisor.group.medium", include: r => r.direction === "LowerRecommended" && r.priority === "Medium" },
  { id: "low", labelKey: "advisor.group.low", include: r => r.direction === "LowerRecommended" && r.priority === "Low" },
  { id: "headroom", labelKey: "advisor.group.headroom", include: r => r.direction === "HeadroomAvailable" },
  { id: "none", labelKey: "advisor.group.none", include: r => r.direction === "NoRecommendation" || r.direction === "KeepCurrent" }
];

const GROUP_TITLE_STYLE = { whiteSpace: "nowrap", margin: "0 0 3rem", fontSize: "13rem" } as const;

function lookup(t: Translate, prefix: string, value: string, fallback: MessageKey): string {
  const key = `${prefix}.${value}` as MessageKey;
  const text = t(key);
  return text === key ? (value || t(fallback)) : text;
}

const categoryLabel = (value: string, t: Translate) => lookup(t, "advisor.category", value, "advisor.category.Unknown");
const levelLabel = (value: string, t: Translate) => lookup(t, "advisor.level", value, "advisor.level.Unknown");
const changeStatusLabel = (value: string, t: Translate) => lookup(t, "advisor.status", value, "advisor.level.Unknown");
const applyBehaviorLabel = (value: string, t: Translate) => lookup(t, "advisor.behavior", value, "advisor.level.Unknown");

export function advisorReasonLabel(value: string | null | undefined, t: Translate): string {
  if (!value) return "";

  let match = value.match(/^Standard Options catalog unavailable(?:: (.+))?$/);
  if (match) return match[1] ? t("advisor.reason.catalogDetail", { detail: match[1] }) : t("advisor.reason.catalog");

  match = value.match(/^Advisor diagnosis unavailable(?:: (.+))?$/);
  if (match) return match[1] ? t("advisor.reason.diagnosisDetail", { detail: match[1] }) : t("advisor.reason.diagnosis");

  if (value === "Advisor export unavailable") return t("advisor.reason.export");
  return value;
}

/** Translates a machine-readable failure reason from the Advisor backend. */
export function advisorFailureLabel(reason: string, t: Translate): string {
  if (!reason) return "";
  const key = `advisor.failure.${reason}` as MessageKey;
  const text = t(key);
  return text === key ? t("advisor.failure.other", { reason }) : text;
}

export function advisorActionMessage(action: AdvisorAction, t: Translate): string {
  const name = action.displayName || action.settingId;
  if (action.kind === "UndoSession") {
    if (action.confirmationRequiredSettingIds?.length)
      return t("advisor.action.sessionConfirm", { count: action.succeededCount, pending: action.confirmationRequiredSettingIds.length });
    if (action.failedSettingIds?.length)
      return t("advisor.action.sessionPartial", { count: action.succeededCount, failed: action.failedSettingIds.join(", ") });
    return t("advisor.action.success.UndoSession", { count: action.succeededCount });
  }
  if (action.succeeded) return t(`advisor.action.success.${action.kind}` as MessageKey, { name });
  return t("advisor.action.failed", { name, reason: advisorFailureLabel(action.failureReason, t) });
}

function isApplied(recommendation: AdvisorRecommendation, changes: AdvisorChange[]): boolean {
  return changes.some(change => change.settingId === recommendation.settingId
    && change.status === "Applied" && change.appliedValue === recommendation.recommendedValue);
}

function RecommendationCard({ recommendation, applied, onApply, onTest, locale, t }: {
  recommendation: AdvisorRecommendation;
  applied: boolean;
  onApply?: (id: string, value: string, confirmed: boolean) => void;
  onTest?: (id: string, value: string) => void;
  locale: Locale;
  t: Translate;
}) {
  const [details, setDetails] = useState(false);
  const [acknowledge, setAcknowledge] = useState(false);
  const needsConfirmation = recommendation.applyBehavior === "ConfirmationRequired";
  return (
    <article className={styles.advisorCard}>
      <strong>{recommendation.displayName}</strong>
      <span>{t("advisor.current", { current: recommendation.currentValue, proposed: recommendation.recommendedValue })}</span>
      <span>{t("advisor.priority", { priority: levelLabel(recommendation.priority, t), confidence: levelLabel(recommendation.confidence, t) })}</span>
      <span>{recommendation.applyCapability === "ReadOnlyForAdvisor" ? t("advisor.readOnly") : t("advisor.checkEvidence")}</span>
      {applied
        ? <span>{t("advisor.applied")}</span>
        : onApply && recommendation.applyCapability === "Available" &&
          (needsConfirmation && !acknowledge
            ? <Button as="button" variant="flat" onSelect={() => setAcknowledge(true)}>{t("advisor.confirmRequired")}</Button>
            : <Button as="button" variant="flat" onSelect={() => onApply(recommendation.settingId,
                recommendation.recommendedValue, needsConfirmation && acknowledge)}>{t("advisor.apply")}</Button>)}
      {!applied && onTest && recommendation.applyCapability === "Available" &&
        recommendation.currentValue !== recommendation.recommendedValue &&
        <Button as="button" variant="flat" onSelect={() => onTest(recommendation.settingId,
          recommendation.recommendedValue)}>{t("advisor.experiment.test")}</Button>}
      <Button as="button" variant="flat" onSelect={() => setDetails(!details)} aria-expanded={details}>
        {details ? t("advisor.hideDetails") : t("advisor.showDetails")}
      </Button>
      {details && <div className={styles.advisorDetails}>
        <span>{advisorTextLabel(recommendation.rationale, locale)}</span>
        <span>{t("advisor.evidence", { evidence: recommendation.evidenceIds?.join(", ") || t("advisor.noEvidence") })}</span>
        <span>{t("advisor.applyBehavior", { behavior: applyBehaviorLabel(recommendation.applyBehavior, t) })}</span>
      </div>}
    </article>
  );
}

const experimentActive = (experiment: AdvisorExperiment | null | undefined): boolean => !!experiment && (
  experiment.state === "BaselineReady" || experiment.state === "AwaitingApplyConfirmation" ||
  experiment.state === "AwaitingFollowUp" || experiment.state === "FollowUpCapturing" ||
  experiment.state === "Completed" && experiment.completionOutcome === "None"
);

function ExperimentCard({ experiment, locale, t, onApply, onFollowUp, onCancel, onKeep, onUndo }: {
  experiment: AdvisorExperiment;
  locale: Locale;
  t: Translate;
  onApply?: (confirmed: boolean) => void;
  onFollowUp?: () => void;
  onCancel?: () => void;
  onKeep?: () => void;
  onUndo?: (confirmed: boolean) => void;
}) {
  const [confirmUndo, setConfirmUndo] = useState(false);
  const compared = experiment.comparison;
  const counts = { Improved: 0, Regressed: 0, NoMaterialChange: 0, NotComparable: 0 };
  compared?.metrics.forEach(metric => { counts[metric.state]++; });
  const hasApplied = !!experiment.changeAppliedAtUtc;
  const awaitingDecision = experiment.state === "Completed" && experiment.completionOutcome === "None";
  const needsUndoConfirmation = experiment.lastFailureReason === "ConfirmationRequired" || confirmUndo;
  const stabilizationPending = experiment.stabilizationReadyAtUtc != null &&
    Date.now() < Date.parse(experiment.stabilizationReadyAtUtc);
  return <section className={styles.experimentCard} aria-label={t("advisor.experiment.title")}>
    <h3>{t("advisor.experiment.title")}</h3>
    <strong>{experiment.settingDisplayName || experiment.settingId}</strong>
    <span>{t("advisor.current", { current: experiment.originalValue, proposed: experiment.testedValue })}</span>
    <span>{t(`advisor.experiment.state.${experiment.state}` as MessageKey)}</span>
    <span>{t("advisor.experiment.baseline", { id: experiment.baselineCaptureId })}</span>
    {experiment.followUpCaptureId && <span>{t("advisor.experiment.followUpId", { id: experiment.followUpCaptureId })}</span>}
    {experiment.state === "Invalidated" && <p role="status">{lookup(t, "advisor.experiment.reason",
      experiment.invalidationReason, "advisor.experiment.reason.Unknown")}</p>}
    {experiment.lastFailureReason && <p role="status">{advisorFailureLabel(experiment.lastFailureReason, t)}</p>}
    {experiment.state === "BaselineReady" && onApply &&
      <Button as="button" variant="flat" onSelect={() => onApply(false)}>{t("advisor.experiment.apply")}</Button>}
    {experiment.state === "AwaitingApplyConfirmation" && onApply &&
      <Button as="button" variant="flat" onSelect={() => onApply(true)}>{t("advisor.experiment.confirmApply")}</Button>}
    {experiment.state === "AwaitingFollowUp" && <>
      <p>{t("advisor.experiment.stabilize")}</p>
      {onFollowUp && <Button as="button" variant="flat" disabled={stabilizationPending}
        onSelect={onFollowUp}>{t("advisor.experiment.followUp")}</Button>}
    </>}
    {experiment.state === "FollowUpCapturing" && <p>{t("advisor.experiment.capturing")}</p>}
    {compared && <>
      <p>{t("advisor.experiment.observed")}</p>
      <div className={styles.experimentCounts}>
        {(["Improved", "Regressed", "NoMaterialChange", "NotComparable"] as const).map(state =>
          <span key={state}>{t(`advisor.compare.${state}` as MessageKey)} {counts[state]}</span>)}
      </div>
      {compared.metrics.map(metric => <div className={styles.advisorObservation} key={metric.id}>
        <strong>{metric.id}: {t(`advisor.compare.${metric.state}` as MessageKey)}</strong>
        <span>{metric.baselineValue ?? t("advisor.notAvailableValue")} → {metric.followUpValue ?? t("advisor.notAvailableValue")}</span>
        {metric.reason && <span>{advisorTextLabel(metric.reason, locale)}</span>}
      </div>)}
      <p>{t("advisor.experiment.disclaimer")}</p>
    </>}
    {experiment.followUpWarnings?.map((warning, index) =>
      <p className={styles.experimentWarning} key={`${index}-${warning}`}>{warning}</p>)}
    {awaitingDecision && <div className={styles.experimentButtons}>
      {onKeep && <Button as="button" variant="flat" onSelect={onKeep}>{t("advisor.experiment.keep")}</Button>}
      {onUndo && <Button as="button" variant="flat" onSelect={() => {
        onUndo(needsUndoConfirmation);
        setConfirmUndo(true);
      }}>{needsUndoConfirmation ? t("advisor.experiment.confirmUndo") : t("advisor.experiment.undo")}</Button>}
    </div>}
    {experimentActive(experiment) && !awaitingDecision && onCancel && <Button as="button" variant="flat" onSelect={onCancel}>
      {hasApplied ? t("advisor.experiment.cancelAfterApply") : t("advisor.experiment.cancel")}
    </Button>}
    {experiment.completionOutcome !== "None" &&
      <p>{t(`advisor.experiment.outcome.${experiment.completionOutcome}` as MessageKey)}</p>}
  </section>;
}

function ChangeCard({ change, onUndo, onResolveConflict, t }: {
  change: AdvisorChange;
  onUndo?: (id: string, confirmed: boolean) => void;
  onResolveConflict?: (id: string, restoreOriginal: boolean) => void;
  t: Translate;
}) {
  const [acknowledgeUndo, setAcknowledgeUndo] = useState(false);
  return <article className={styles.advisorCard}>
    <strong title={change.settingId}>{change.displayName || change.settingId}</strong>
    <span>{t("advisor.changeValues", { original: change.originalValue, applied: change.appliedValue, current: change.currentObservedValue })}</span>
    <span>{t("advisor.changeStatus", { status: changeStatusLabel(change.status, t) })}</span>
    {change.status === "Applied" && onUndo && (
      acknowledgeUndo
        ? <Button as="button" variant="flat" onSelect={() => onUndo(change.settingId, true)}>{t("advisor.undoConfirm")}</Button>
        : <Button as="button" variant="flat" onSelect={() => setAcknowledgeUndo(true)}>{t("advisor.undo")}</Button>
    )}
    {change.status === "ExternallyModified" && onResolveConflict && <div className={styles.advisorActions}>
      <span>{t("advisor.conflict")}</span>
      <Button as="button" variant="flat" onSelect={() => onResolveConflict(change.settingId, false)}>{t("advisor.keepCurrent")}</Button>
      <Button as="button" variant="flat" onSelect={() => onResolveConflict(change.settingId, true)}>{t("advisor.restoreOriginal")}</Button>
    </div>}
  </article>;
}

function UndoSessionControl({ appliedCount, onUndoSession, t }: {
  appliedCount: number;
  onUndoSession: (confirmed: boolean) => void;
  t: Translate;
}) {
  const [confirming, setConfirming] = useState(false);
  if (!confirming)
    return <Button as="button" variant="flat" onSelect={() => setConfirming(true)}>{t("advisor.undoSession")}</Button>;
  return <div className={styles.advisorActions}>
    <Button as="button" variant="flat" onSelect={() => { setConfirming(false); onUndoSession(true); }}>
      {t("advisor.undoSessionConfirm", { count: appliedCount })}
    </Button>
    <Button as="button" variant="flat" onSelect={() => setConfirming(false)}>{t("advisor.cancel")}</Button>
  </div>;
}

export function PerformanceAdvisorTab({ advisor = EMPTY_ADVISOR, captures = [], onDiagnose, onBaseline, onManualCapture,
  onApply, onUndo, onUndoSession, onResolveConflict, onRediagnose, onStartExperiment, onApplyExperiment,
  onStartExperimentFollowUp, onCancelExperiment, onKeepExperiment, onUndoExperiment }: {
  advisor?: AdvisorUiState;
  captures?: CaptureSummaryUi[];
  onDiagnose?: (id: string) => void;
  onBaseline?: (id: string) => void;
  onManualCapture?: () => void;
  onApply?: (id: string, value: string, confirmed: boolean) => void;
  onUndo?: (id: string, confirmed: boolean) => void;
  onUndoSession?: (confirmed: boolean) => void;
  onResolveConflict?: (id: string, restoreOriginal: boolean) => void;
  onRediagnose?: (id: string) => void;
  onStartExperiment?: (captureId: string, settingId: string, proposedValue: string) => void;
  onApplyExperiment?: (confirmed: boolean) => void;
  onStartExperimentFollowUp?: () => void;
  onCancelExperiment?: () => void;
  onKeepExperiment?: () => void;
  onUndoExperiment?: (confirmed: boolean) => void;
}) {
  const { locale, t } = useText();
  const [showNoRecommendation, setShowNoRecommendation] = useState(false);
  const changes = advisor.changes ?? [];
  const appliedCount = changes.filter(change => change.status === "Applied").length;
  const activeExperiment = experimentActive(advisor.experiment);
  return (
    <section className={styles.advisorTab}>
      <h2>{t("advisor.title")}</h2>
      <p>{t("advisor.intro")}</p>
      {advisor.lastAction && <p className={styles.exportResult} role="status">{advisorActionMessage(advisor.lastAction, t)}</p>}
      {advisor.experiment && <ExperimentCard experiment={advisor.experiment} locale={locale} t={t}
        onApply={onApplyExperiment} onFollowUp={onStartExperimentFollowUp}
        onCancel={onCancelExperiment} onKeep={onKeepExperiment} onUndo={onUndoExperiment} />}
      <div className={styles.advisorActions}>
        {onManualCapture && <Button as="button" variant="flat" onSelect={onManualCapture}>{t("advisor.manualCapture")}</Button>}
        {captures.map(capture => (
          <div className={styles.advisorCapture} key={capture.id}>
            <span>{capture.id}</span>
            {onDiagnose && <Button as="button" variant="flat" onSelect={() => onDiagnose(capture.id)}>{t("advisor.diagnose")}</Button>}
            {onRediagnose && advisor.selectedCaptureId && <Button as="button" variant="flat"
              onSelect={() => onRediagnose(capture.id)}>{t("advisor.rediagnose")}</Button>}
            {onBaseline && <Button as="button" variant="flat" onSelect={() => onBaseline(capture.id)}>{t("advisor.baseline")}</Button>}
          </div>
        ))}
      </div>
      {advisor.unavailableReason && <p className={styles.empty}>{t("advisor.unavailable", { reason: advisorReasonLabel(advisor.unavailableReason, t) })}</p>}
      {advisor.selectedCaptureId
        ? <p>{t("advisor.target", { id: advisor.selectedCaptureId })}{advisor.baselineCaptureId ? t("advisor.targetBaseline", { id: advisor.baselineCaptureId }) : ""}</p>
        : <p>{t("advisor.pick")}</p>}
      {advisor.observations.map((observation, index) => (
        <div className={styles.advisorObservation} key={`${observation.category}-${index}`}>
          <strong>{t("advisor.observation", { category: categoryLabel(observation.category, t), severity: levelLabel(observation.severity, t) })}</strong>
          <span>{t("advisor.observationEvidence", { confidence: levelLabel(observation.confidence, t), evidence: observation.evidenceIds?.join(", ") ?? "" })}</span>
          <span>{advisorTextLabel(observation.rationale, locale)}</span>
        </div>
      ))}
      {activeExperiment && <p>{t("advisor.experiment.otherApplyDisabled")}</p>}
      {GROUPS.map(group => {
        const entries = advisor.recommendations.filter(group.include);
        const open = group.id !== "none" || showNoRecommendation;
        return (
          <section className={styles.advisorGroup} key={group.id}>
            {group.id === "none"
              ? <Button as="button" variant="flat" aria-label={t("advisor.showNone")} aria-expanded={open}
                  onSelect={() => setShowNoRecommendation(!showNoRecommendation)}>{t(group.labelKey)} ({entries.length})</Button>
              : <h3 style={GROUP_TITLE_STYLE}>{t(group.labelKey)} ({entries.length})</h3>}
            {open && entries.map(entry => <RecommendationCard key={entry.settingId} recommendation={entry}
              applied={isApplied(entry, changes)} onApply={activeExperiment ? undefined : onApply}
              onTest={!activeExperiment && advisor.available && !!advisor.selectedCaptureId && onStartExperiment
                ? (id, value) => onStartExperiment(advisor.selectedCaptureId, id, value) : undefined}
              locale={locale} t={t} />)}
          </section>
        );
      })}
      {!!changes.length && <section className={styles.advisorGroup}>
        <h3 style={GROUP_TITLE_STYLE}>{t("advisor.changes", { count: changes.length })}</h3>
        {changes.map((change, index) => <ChangeCard key={`${change.settingId}-${index}`} change={change}
          onUndo={onUndo} onResolveConflict={onResolveConflict} t={t} />)}
        {onUndoSession && appliedCount > 0 && <UndoSessionControl appliedCount={appliedCount} onUndoSession={onUndoSession} t={t} />}
      </section>}
      {advisor.comparison && <section className={styles.advisorGroup}>
        <h3 style={GROUP_TITLE_STYLE}>{t("advisor.comparison")}</h3>
        {advisor.comparison.multipleChanges && <p>{t("advisor.multipleChanges")}</p>}
        {advisor.comparison.metrics.map(metric => (
          <div className={styles.advisorObservation} key={metric.id}>
            <strong>{metric.id}: {t(`advisor.compare.${metric.state}` as MessageKey)}</strong>
            <span>{metric.baselineValue ?? t("advisor.notAvailableValue")} → {metric.followUpValue ?? t("advisor.notAvailableValue")}</span>
            {metric.reason && <span>{advisorTextLabel(metric.reason, locale)}</span>}
          </div>
        ))}
      </section>}
    </section>
  );
}
