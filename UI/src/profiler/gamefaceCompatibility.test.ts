import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

const stylesheet = fs.readFileSync(
  path.resolve(process.cwd(), "src/profiler/profiler.module.scss"),
  "utf8"
);

describe("Gameface runtime compatibility", () => {
  it("keeps the Game-mounted profiler panel pointer-interactive", () => {
    const panelRule = stylesheet.match(/\.panel\s*\{([\s\S]*?)\}/)?.[1] ?? "";
    expect(panelRule).toMatch(/pointer-events\s*:\s*auto\s*;/);
  });

  it("avoids CSS syntax rejected by the CS2 Gameface runtime", () => {
    const rejectedPatterns: Array<[string, RegExp]> = [
      ["CSS Grid", /display\s*:\s*grid\b/i],
      ["inline-flex", /display\s*:\s*inline-flex\b/i],
      ["fractional grid units", /\b\d+(?:\.\d+)?fr\b/i],
      ["CSS min()", /\bmin\s*\(/i],
      [":disabled pseudo-class", /:disabled\b/i],
      ["inherit color value", /color\s*:\s*inherit\b/i],
      ["inherit font-size value", /font-size\s*:\s*inherit\b/i],
      ["background shorthand", /(^|[;{]\s*)background\s*:/im],
      ["border shorthand", /(^|[;{]\s*)border(?:-(?:top|right|bottom|left))?\s*:/im]
    ];

    for (const [name, pattern] of rejectedPatterns) {
      expect(stylesheet, `${name} should not be present`).not.toMatch(pattern);
    }
  });
});

// Every in-game UI defect so far came from browser features the Coherent Gameface runtime lacks:
// CSS grid/min()/shorthands (above), <table> layout (every cell rendered on its own line),
// <input type="checkbox"> (rendered as an editable text field), native overflow scrollbars (none drawn),
// inline data: URI icons (blank) and DOM keydown for Escape (the game consumes it as an input action).
// Guard the markup the same way the stylesheet is guarded.
describe("Gameface markup compatibility", () => {
  const sourceRoot = path.resolve(process.cwd(), "src");
  const sources = (function collect(dir: string): Array<[string, string]> {
    return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) return entry.name === "test" ? [] : collect(full);
      return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [[full, fs.readFileSync(full, "utf8")] as [string, string]] : [];
    });
  })(sourceRoot);

  it("scans the UI sources", () => {
    expect(sources.length).toBeGreaterThan(5);
  });

  it("uses only elements and APIs that render correctly in Gameface", () => {
    const rejected: Array<[string, RegExp]> = [
      ["<table> layout", /<(table|thead|tbody|tr|td|th)[\s>]/],
      ["<details>/<summary>", /<(details|summary)[\s>]/],
      ["native form controls other than text search", /<(select|textarea|label)[\s>]|<input(?!\s+type="search")[\s>]/],
      ["inline data: URI images", /data:image\//],
      ["DOM keydown handling (use a cs2/input consumer)", /addEventListener\(\s*["']key(down|up|press)["']/]
    ];

    for (const [file, source] of sources) {
      for (const [name, pattern] of rejected) {
        expect(source, `${name} in ${path.relative(sourceRoot, file)}`).not.toMatch(pattern);
      }
    }
  });
});
