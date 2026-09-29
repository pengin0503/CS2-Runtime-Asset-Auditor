import React, { useCallback, useEffect, useRef, useState } from "react";
import { Button, Scrollable } from "cs2/ui";
import { InputActionConsumer } from "cs2/input";
import {
  closePanel,
  exportReport,
  requestManualCapture,
  resetPanelLayout,
  savePanelLayout,
  selectCapture,
  requestAdvisorDiagnosis,
  requestAdvisorRediagnosis,
  selectAdvisorBaseline,
  advisorApply,
  advisorUndo,
  advisorUndoSession,
  advisorResolveConflict,
  useExportResult,
  usePanelLayout,
  usePanelVisible,
  useProfilerSnapshot,
  useUiScalePercent
} from "../profiler/bindings";
import { OverviewTab } from "../profiler/tabs/OverviewTab";
import { SystemsTab } from "../profiler/tabs/SystemsTab";
import { ModsTab } from "../profiler/tabs/ModsTab";
import { PathfindingTab } from "../profiler/tabs/PathfindingTab";
import { TimelineTab } from "../profiler/tabs/TimelineTab";
import { CapturesTab } from "../profiler/tabs/CapturesTab";
import { DiagnosticsTab } from "../profiler/tabs/DiagnosticsTab";
import { PerformanceAdvisorTab } from "../profiler/tabs/PerformanceAdvisorTab";
import { captureStateLabel } from "../profiler/text";
import { useText } from "../i18n/locale";
import { PanelRect, clampPanelRect, movePanelRect, resizePanelRect } from "../profiler/panelLayout";
import styles from "../profiler/profiler.module.scss";
import { AssetSection } from "../assets/AssetSection";
import { TOP_SECTIONS, RUNTIME_SECTIONS, ASSET_SECTIONS, type TopSection, type RuntimeSection, type AssetSectionName } from "./navigation";

type DragMode = "move" | "resize";

interface DragState {
  mode: DragMode;
  startX: number;
  startY: number;
  startRect: PanelRect;
}

function viewport() {
  return { width: window.innerWidth, height: window.innerHeight };
}

// Escape (keyboard) and B (gamepad) reach the UI as the game's "Back" input action, not as DOM
// keydown events; the vanilla pause-menu handler consumed them before a DOM listener could.
// Registering a Back consumer while the panel is open lets the game route Back to this panel.
const BACK_ACTIONS = { Back: closePanel };

export function RuntimeAssetAuditorRoot() {
  const visible = usePanelVisible();
  const snapshot = useProfilerSnapshot();
  const exportResult = useExportResult();
  const uiScalePercent = useUiScalePercent();
  const savedLayout = usePanelLayout();
  const { locale, t } = useText();
  const [section, setSection] = useState<TopSection>("overview");
  const [runtimeView, setRuntimeView] = useState<RuntimeSection>("systems");
  const [assetView, setAssetView] = useState<AssetSectionName>("catalog");
  const [investigationCaptureId, setInvestigationCaptureId] = useState<string | null>(null);
  const [rect, setRect] = useState<PanelRect | null>(null);
  const panelRef = useRef<HTMLDivElement | null>(null);
  const dragRef = useRef<DragState | null>(null);
  const rectRef = useRef<PanelRect | null>(null);

  const safeScalePercent = Math.min(150, Math.max(75, uiScalePercent || 100));
  const scale = safeScalePercent / 100;

  // Follow the persisted layout (initial load, reset, or another save) unless a drag is in progress.
  useEffect(() => {
    if (dragRef.current) return;
    const next = savedLayout?.custom
      ? clampPanelRect({ left: savedLayout.left, top: savedLayout.top, width: savedLayout.width, height: savedLayout.height }, viewport(), scale)
      : null;
    rectRef.current = next;
    setRect(next);
  }, [savedLayout?.custom, savedLayout?.left, savedLayout?.top, savedLayout?.width, savedLayout?.height, scale]);

  const onMouseMove = useCallback((event: MouseEvent) => {
    const drag = dragRef.current;
    if (!drag) return;
    const deltaX = event.clientX - drag.startX;
    const deltaY = event.clientY - drag.startY;
    const next = drag.mode === "move"
      ? movePanelRect(drag.startRect, deltaX, deltaY, viewport(), scale)
      : resizePanelRect(drag.startRect, deltaX, deltaY, viewport(), scale);
    rectRef.current = next;
    setRect(next);
  }, [scale]);

  const onMouseUp = useCallback(() => {
    document.removeEventListener("mousemove", onMouseMove);
    document.removeEventListener("mouseup", onMouseUp);
    const finished = dragRef.current;
    dragRef.current = null;
    const current = rectRef.current;
    if (finished && current) savePanelLayout(current.left, current.top, current.width, current.height);
  }, [onMouseMove]);

  useEffect(() => () => {
    document.removeEventListener("mousemove", onMouseMove);
    document.removeEventListener("mouseup", onMouseUp);
  }, [onMouseMove, onMouseUp]);

  const beginDrag = (mode: DragMode) => (event: React.MouseEvent) => {
    if (event.button !== 0) return;
    event.preventDefault();
    event.stopPropagation();
    let startRect = rectRef.current;
    if (!startRect) {
      // First interaction with the default CSS layout: adopt its current on-screen geometry.
      const bounds = panelRef.current?.getBoundingClientRect();
      if (!bounds) return;
      startRect = { left: bounds.left, top: bounds.top, width: bounds.width / scale, height: bounds.height / scale };
    }
    dragRef.current = { mode, startX: event.clientX, startY: event.clientY, startRect };
    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  };

  if (!visible) return null;

  const inverseScale = 1 / scale;
  const panelStyle: React.CSSProperties = rect
    ? {
        transform: `scale(${scale})`,
        transformOrigin: "top left",
        left: `${rect.left}px`,
        top: `${rect.top}px`,
        width: `${rect.width}px`,
        height: `${rect.height}px`,
        maxWidth: "none",
        maxHeight: "none"
      }
    : {
        transform: `scale(${scale})`,
        transformOrigin: "top left",
        maxWidth: `calc(${100 * inverseScale}vw - ${56 * inverseScale}rem)`,
        height: `calc(${100 * inverseScale}vh - ${110 * inverseScale}rem)`,
        maxHeight: `calc(${100 * inverseScale}vh - ${110 * inverseScale}rem)`
      };

  return (
    <InputActionConsumer actions={BACK_ACTIONS} ignoreFocusState>
      <div ref={panelRef} className={styles.panel} style={panelStyle} role="dialog" aria-label="CS2 Runtime Asset Auditor">
        <header className={styles.panelHeader}>
          <div className={styles.dragHandle} onMouseDown={beginDrag("move")} title={t("shell.dragMove")}>
            <strong>CS2 Runtime Asset Auditor</strong>
            <small>{snapshot.capture.isDeepCapture ? t("shell.deepCaptureRunning") : t("shell.lowOverhead", { state: captureStateLabel(snapshot.capture.state, false, locale) })}</small>
          </div>
          <div className={styles.headerActions}>
            {rect && (
              <Button as="button" variant="flat" className={styles.headerButton} onSelect={resetPanelLayout} aria-label={t("shell.resetLayoutAria")}>
                {t("shell.resetLayout")}
              </Button>
            )}
            <Button as="button" variant="flat" className={styles.closeButton} onSelect={closePanel} aria-label={t("shell.close")}>×</Button>
          </div>
        </header>

        <nav className={styles.tabs} aria-label={t("shell.sectionsAria")}>
          {TOP_SECTIONS.map(({ id, labelKey }) => (
            <Button
              as="button"
              variant="flat"
              key={id}
              selected={section === id}
              className={`${styles.tabButton} ${section === id ? styles.activeTab : ""}`}
              onSelect={() => setSection(id)}
            >
              {t(labelKey)}
            </Button>
          ))}
        </nav>

        <Scrollable vertical trackVisibility="scrollable" className={styles.panelBody}>
          <div className={styles.panelContent}>
            {section === "runtime" && <nav className={styles.tabs} aria-label={t("shell.runtimeViewsAria")}>
              {RUNTIME_SECTIONS.map(({ id, labelKey }) => <Button as="button" variant="flat" key={id}
                selected={runtimeView === id} className={`${styles.tabButton} ${runtimeView === id ? styles.activeTab : ""}`}
                onSelect={() => setRuntimeView(id)}>{t(labelKey)}</Button>)}
            </nav>}
            {section === "assets" && <nav className={styles.tabs} aria-label={t("shell.assetViewsAria")}>
              {ASSET_SECTIONS.map(({ id, labelKey }) => <Button as="button" variant="flat" key={id}
                selected={assetView === id} className={`${styles.tabButton} ${assetView === id ? styles.activeTab : ""}`}
                onSelect={() => setAssetView(id)}>{t(labelKey)}</Button>)}
            </nav>}
            {section === "overview" && <><OverviewTab snapshot={snapshot} onManualCapture={requestManualCapture} onExport={exportReport} exportResult={exportResult} />
              <AssetSection view="overview" active={visible} /></>}
            {section === "runtime" && runtimeView === "systems" && <SystemsTab systems={snapshot.systems} />}
            {section === "runtime" && runtimeView === "mods" && <ModsTab mods={snapshot.mods} systems={snapshot.systems} />}
            {section === "runtime" && runtimeView === "pathfinding" && <PathfindingTab metrics={snapshot.pathfinding.metrics} />}
            {section === "runtime" && runtimeView === "timeline" && <TimelineTab points={snapshot.timeline} />}
            {section === "runtime" && runtimeView === "captures" && <CapturesTab captures={snapshot.captures} onSelect={selectCapture}
              onInvestigate={id => { setInvestigationCaptureId(id); selectCapture(id); setAssetView("catalog"); setSection("assets"); }} />}
            {section === "assets" && <><p>{t("shell.assetDisclaimer")}</p>
              <AssetSection view={assetView} active={visible} capture={snapshot.captures.find(capture => capture.id === investigationCaptureId)}
                onOpenRuntimeCaptures={() => { setRuntimeView("captures"); setSection("runtime"); }} /></>}
            {section === "advisor" && <PerformanceAdvisorTab advisor={snapshot.advisor} captures={snapshot.captures}
              onDiagnose={requestAdvisorDiagnosis} onBaseline={selectAdvisorBaseline} onManualCapture={requestManualCapture}
              onRediagnose={requestAdvisorRediagnosis}
              onApply={advisorApply} onUndo={advisorUndo} onUndoSession={advisorUndoSession}
              onResolveConflict={advisorResolveConflict} />}
            {section === "diagnostics" && <DiagnosticsTab diagnostics={snapshot.diagnostics} captures={snapshot.captures} />}
          </div>
        </Scrollable>

        <div className={styles.resizeGrip} onMouseDown={beginDrag("resize")} title={t("shell.dragResize")} />
      </div>
    </InputActionConsumer>
  );
}
