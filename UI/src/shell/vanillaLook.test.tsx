import fs from "node:fs";
import path from "node:path";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, describe, expect, it } from "vitest";
import { clearBindingValues, setBindingValue } from "../test/cs2ApiStub";
import { RuntimeAssetAuditorRoot } from "./RuntimeAssetAuditorRoot";
import { GLYPHS } from "./glyphs";

const stylesheet = fs.readFileSync(path.resolve(process.cwd(), "src/profiler/profiler.module.scss"), "utf8");
const rule = (selector: string) => stylesheet.match(new RegExp(`\\.${selector}\\s*\\{([^}]*)\\}`))?.[1] ?? "";

describe("panel follows the game's own panels", () => {
  afterEach(() => clearBindingValues());

  it("closes with the vanilla tinted close glyph on a round button", () => {
    setBindingValue("CS2RuntimeAssetAuditor", "panelVisible", true);
    const html = renderToStaticMarkup(<RuntimeAssetAuditorRoot />);
    expect(html).toMatch(new RegExp(`data-variant="round"[^>]*aria-label="[^"]+"[^>]*><img src="${GLYPHS.close}"`));
    expect(html).not.toContain(">×<");
  });

  it("takes colors from the game's theme variables instead of fixed values", () => {
    // The game swaps these variables for its interface styles, so the panel follows the player's choice.
    expect(rule("panelHeader")).toContain("var(--panelColorDark)");
    expect(rule("panelBody")).toContain("var(--gradientDarkBlueEnd)");
    expect(rule("activeTab, \\.activeTab:hover")).toContain("var(--gradientHighlightStart)");
    expect(rule("panel")).toContain("var(--normalTextColor)");
    expect(rule("panel")).toContain("var(--fontSizeS)");
  });
});
