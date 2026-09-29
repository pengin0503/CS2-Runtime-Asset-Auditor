import fs from "node:fs";
import path from "node:path";
import { expect, test } from "vitest";
import { advisorTextLabel, advisorTranslatedTexts } from "./text";

// Every rationale sentence the advisor backend writes must have a Japanese translation; an untranslated one
// would show English in the Japanese UI.
const backendSources = ["BottleneckClassifier.cs", "RecommendationEngine.cs"]
  .map(file => path.resolve(process.cwd(), "../src/CS2RuntimeAssetAuditor/Core/Advisor", file));

function rationaleLiterals(source: string): string[] {
  const literals = [...source.matchAll(/"((?:[^"\\]|\\.)*)"/g)].map(match => match[1].trim());
  // Rationale sentences are the literals that read as English sentences; ids and names have no spaces.
  return literals.filter(text => /^[A-Z].* .*\.$/.test(text));
}

test("translates every advisor rationale sentence the backend writes", () => {
  const translated = new Set(advisorTranslatedTexts());
  const sentences = backendSources.flatMap(file => rationaleLiterals(fs.readFileSync(file, "utf8")));
  expect(sentences.length).toBeGreaterThan(10);
  expect(sentences.filter(sentence => !translated.has(sentence))).toEqual([]);
});

test("translates a rationale with the Performance Preference note appended", () => {
  const text = "Simulation efficiency stays low across the measurement window, including the later part of the capture."
    + " The Performance Preference option also limits simulation steps to the time left in each frame, so part of the slowdown may come from that setting.";
  const ja = advisorTextLabel(text, "ja");
  expect(ja).toContain("シミュレーション効率");
  expect(ja).toContain("Performance Preference 設定");
  expect(ja).not.toContain("time left");
  expect(advisorTextLabel(text, "en")).toBe(text);
});
