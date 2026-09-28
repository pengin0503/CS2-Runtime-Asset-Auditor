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
} from "./bindings";
import { OverviewTab } from "./tabs/OverviewTab";
import { SystemsTab } from "./tabs/SystemsTab";
import { ModsTab } from "./tabs/ModsTab";
import { PathfindingTab } from "./tabs/PathfindingTab";
import { TimelineTab } from "./tabs/TimelineTab";
import { CapturesTab } from "./tabs/CapturesTab";
import { DiagnosticsTab } from "./tabs/DiagnosticsTab";
import { PerformanceAdvisorTab } from "./tabs/PerformanceAdvisorTab";
import { captureStateLabel } from "./text";
import { PanelRect, clampPanelRect, movePanelRect, resizePanelRect } from "./panelLayout";
import styles from "./profiler.module.scss";

type ProfilerTab = "overview" | "systems" | "mods" | "pathfinding" | "timeline" | "captures" | "diagnostics" | "advisor";
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

export function ProfilerRoot() {
  const visible = usePanelVisible();
  const snapshot = useProfilerSnapshot();
  const exportResult = useExportResult();
  const uiScalePercent = useUiScalePercent();
  const savedLayout = usePanelLayout();
  const [tab, setTab] = useState<ProfilerTab>("overview");
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

  const tabs: Array<[ProfilerTab, string]> = [
    ["overview", "概要"],
    ["systems", "システム"],
    ["mods", "MOD"],
    ["pathfinding", "経路探索"],
    ["timeline", "タイムライン"],
    ["captures", "キャプチャ"],
    ["advisor", "改善提案"],
    ["diagnostics", "診断"]
  ];

  return (
    <InputActionConsumer actions={BACK_ACTIONS} ignoreFocusState>
      <div ref={panelRef} className={styles.panel} style={panelStyle} role="dialog" aria-label="CS2 ランタイムプロファイラー">
        <header className={styles.panelHeader}>
          <div className={styles.dragHandle} onMouseDown={beginDrag("move")} title="ドラッグしてパネルを移動">
            <strong>CS2 ランタイムプロファイラー</strong>
            <small>{snapshot.capture.isDeepCapture ? "詳細キャプチャ実行中" : `低負荷監視・${captureStateLabel(snapshot.capture.state)}`}</small>
          </div>
          <div className={styles.headerActions}>
            {rect && (
              <Button as="button" variant="flat" className={styles.headerButton} onSelect={resetPanelLayout} aria-label="パネルの位置とサイズを初期状態に戻す">
                位置をリセット
              </Button>
            )}
            <Button as="button" variant="flat" className={styles.closeButton} onSelect={closePanel} aria-label="プロファイラーを閉じる">×</Button>
          </div>
        </header>

        <nav className={styles.tabs} aria-label="プロファイラー表示切替">
          {tabs.map(([id, label]) => (
            <Button
              as="button"
              variant="flat"
              key={id}
              selected={tab === id}
              className={`${styles.tabButton} ${tab === id ? styles.activeTab : ""}`}
              onSelect={() => setTab(id)}
            >
              {label}
            </Button>
          ))}
        </nav>

        <Scrollable vertical trackVisibility="scrollable" className={styles.panelBody}>
          <div className={styles.panelContent}>
            {tab === "overview" && <OverviewTab snapshot={snapshot} onManualCapture={requestManualCapture} onExport={exportReport} exportResult={exportResult} />}
            {tab === "systems" && <SystemsTab systems={snapshot.systems} />}
            {tab === "mods" && <ModsTab mods={snapshot.mods} systems={snapshot.systems} />}
            {tab === "pathfinding" && <PathfindingTab metrics={snapshot.pathfinding.metrics} />}
            {tab === "timeline" && <TimelineTab points={snapshot.timeline} />}
            {tab === "captures" && <CapturesTab captures={snapshot.captures} onSelect={selectCapture} />}
            {tab === "advisor" && <PerformanceAdvisorTab advisor={snapshot.advisor} captures={snapshot.captures}
              onDiagnose={requestAdvisorDiagnosis} onBaseline={selectAdvisorBaseline} onManualCapture={requestManualCapture}
              onRediagnose={requestAdvisorRediagnosis}
              onApply={advisorApply} onUndo={advisorUndo} onUndoSession={advisorUndoSession}
              onResolveConflict={advisorResolveConflict} />}
            {tab === "diagnostics" && <DiagnosticsTab diagnostics={snapshot.diagnostics} captures={snapshot.captures} />}
          </div>
        </Scrollable>

        <div className={styles.resizeGrip} onMouseDown={beginDrag("resize")} title="ドラッグしてパネルのサイズを変更" />
      </div>
    </InputActionConsumer>
  );
}
