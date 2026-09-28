/** Panel geometry in screen pixels. Width/height are pre-scale CSS sizes; the panel is drawn at `scale`. */
export interface PanelRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface Viewport {
  width: number;
  height: number;
}

export const MIN_PANEL_WIDTH = 420;
export const MIN_PANEL_HEIGHT = 240;
/** Part of the header that must stay on screen so a moved panel can always be grabbed again. */
const GRAB_MARGIN = 48;

function clampNumber(value: number, min: number, max: number): number {
  if (!Number.isFinite(value)) return min;
  return Math.min(Math.max(value, min), Math.max(min, max));
}

/** Keeps the panel at least minimum size, no larger than the screen, and with its header reachable. */
export function clampPanelRect(rect: PanelRect, viewport: Viewport, scale: number): PanelRect {
  const safeScale = scale > 0 ? scale : 1;
  const maxWidth = viewport.width / safeScale;
  const maxHeight = viewport.height / safeScale;
  const width = clampNumber(rect.width, Math.min(MIN_PANEL_WIDTH, maxWidth), maxWidth);
  const height = clampNumber(rect.height, Math.min(MIN_PANEL_HEIGHT, maxHeight), maxHeight);
  const left = clampNumber(rect.left, GRAB_MARGIN - width * safeScale, viewport.width - GRAB_MARGIN);
  const top = clampNumber(rect.top, 0, viewport.height - GRAB_MARGIN);
  return { left, top, width, height };
}

export function movePanelRect(start: PanelRect, deltaX: number, deltaY: number, viewport: Viewport, scale: number): PanelRect {
  return clampPanelRect({ ...start, left: start.left + deltaX, top: start.top + deltaY }, viewport, scale);
}

/** Mouse deltas are screen pixels; the panel is scaled, so sizes change by delta / scale. */
export function resizePanelRect(start: PanelRect, deltaX: number, deltaY: number, viewport: Viewport, scale: number): PanelRect {
  const safeScale = scale > 0 ? scale : 1;
  const resized = { ...start, width: start.width + deltaX / safeScale, height: start.height + deltaY / safeScale };
  const clamped = clampPanelRect(resized, viewport, safeScale);
  // Do not let a resize push the panel past the right/bottom screen edge.
  return {
    ...clamped,
    width: Math.min(clamped.width, Math.max(Math.min(MIN_PANEL_WIDTH, viewport.width / safeScale), (viewport.width - clamped.left) / safeScale)),
    height: Math.min(clamped.height, Math.max(Math.min(MIN_PANEL_HEIGHT, viewport.height / safeScale), (viewport.height - clamped.top) / safeScale))
  };
}
