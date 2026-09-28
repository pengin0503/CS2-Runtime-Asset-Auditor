import React from "react";
import type { ModRegistrar } from "cs2/modding";
import { ProfilerRoot } from "./profiler/ProfilerRoot";
import { ProfilerHud } from "./profiler/components/ProfilerHud";
import { togglePanel, usePanelVisible, useProfilerHudSnapshot } from "./profiler/bindings";

export const hasCSS = true;

function ProfilerHudEntry() {
  const snapshot = useProfilerHudSnapshot();
  const panelVisible = usePanelVisible();
  return <ProfilerHud snapshot={snapshot} panelVisible={panelVisible} onToggle={togglePanel} />;
}

const register: ModRegistrar = moduleRegistry => {
  moduleRegistry.append("GameTopLeft", ProfilerHudEntry);
  moduleRegistry.append("Game", ProfilerRoot);
};

export default register;
