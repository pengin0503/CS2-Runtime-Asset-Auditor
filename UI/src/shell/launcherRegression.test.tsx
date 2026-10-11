import { expect, test } from "vitest";
import register from "../index";
import { RuntimeAssetAuditorRoot } from "./RuntimeAssetAuditorRoot";

test("registers one launcher and one Game panel", () => {
  const appended: Array<{ slot: string; component: unknown }> = [];
  const registry = { append: (slot: string, component: unknown) => { appended.push({ slot, component }); } };

  register(registry as unknown as Parameters<typeof register>[0]);

  expect(appended.map(entry => entry.slot)).toEqual(["GameTopLeft", "Game"]);
  // The Game panel is the one that consumes the Back action (see backAction.test.tsx).
  expect(appended[1].component).toBe(RuntimeAssetAuditorRoot);
});
