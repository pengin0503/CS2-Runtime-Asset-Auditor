import React from "react";
import { Button, Tooltip } from "cs2/ui";
import type { UiHudSnapshot } from "../bindings";
import { formatSpeed } from "../format";
import { captureStateLabel } from "../text";
import { useText } from "../../i18n/locale";
import profilerIcon from "../../images/profiler-icon.svg";

interface ProfilerHudProps {
  snapshot: UiHudSnapshot;
  panelVisible: boolean;
  onToggle: () => void;
}

// The icon ships as a real file (coui://ui-mods/...), the way other UI mods ship theirs; the
// inline data: URI SVG used before rendered no image in game. The button has no wrapper element so the
// GameTopLeft row aligns it like the other floating launchers; the previous vertically centering
// flex wrapper most likely stretched to the row height (badges on other launchers) and pushed it down.
export function ProfilerHud({ snapshot, panelVisible, onToggle }: ProfilerHudProps) {
  const { locale, t } = useText();
  const tooltip = t("hud.tooltip", {
    selected: formatSpeed(snapshot.selectedSpeed),
    actual: formatSpeed(snapshot.actualSpeed),
    state: captureStateLabel(snapshot.state, snapshot.isDeepCapture, locale)
  });
  return (
    <Tooltip tooltip={tooltip}>
      <Button
        as="button"
        variant="floating"
        src={profilerIcon}
        selected={panelVisible}
        onSelect={onToggle}
        aria-pressed={panelVisible}
        aria-label="CS2 Runtime Asset Auditor"
      />
    </Tooltip>
  );
}
