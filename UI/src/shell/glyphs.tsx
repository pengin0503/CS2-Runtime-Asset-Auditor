import React from "react";
import { Icon } from "cs2/ui";
import styles from "../profiler/profiler.module.scss";

// Monochrome glyphs shipped with the game UI (Media/Glyphs in the game's UI folder). Relative paths
// resolve against the game UI document, the way the vanilla panels reference them; a url() in this
// mod's stylesheet would resolve against coui://ui-mods/ instead, so glyphs are only set from markup.
export const GLYPHS = {
  close: "Media/Glyphs/Close.svg",
  expand: "Media/Glyphs/ThickStrokeArrowRight.svg",
  collapse: "Media/Glyphs/ThickStrokeArrowDown.svg"
} as const;

/** The vanilla foldout arrow (right when collapsed, down when expanded), tinted with the text color. */
export function ExpandGlyph({ expanded }: { expanded: boolean }) {
  return <Icon tinted src={expanded ? GLYPHS.collapse : GLYPHS.expand} className={styles.expandGlyph} />;
}
