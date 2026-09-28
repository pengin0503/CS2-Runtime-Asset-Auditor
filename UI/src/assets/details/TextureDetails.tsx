import React from "react";
import { formatObservation } from "../bindings";
import type { UiObservation } from "../types";

export function TextureDetails({ payload }: { payload: UiObservation<number> }): React.JSX.Element {
  return (
    <section className="apa__detail-section" aria-label="Texture details">
      <h3>Textures</h3>
      <p><strong>Estimated logical payload:</strong> {formatObservation(payload)}</p>
      <p className="apa__muted">This is an estimated logical full payload, not GPU residency or measured VRAM usage.</p>
    </section>
  );
}
