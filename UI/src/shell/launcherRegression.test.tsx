import fs from "node:fs";
import path from "node:path";
import { expect, test } from "vitest";

test("one launcher and one Game panel own the Back action", () => {
  const index = fs.readFileSync(path.resolve(process.cwd(), "src/index.tsx"), "utf8");
  const shell = fs.readFileSync(path.resolve(process.cwd(), "src/shell/RuntimeAssetAuditorRoot.tsx"), "utf8");
  expect(index.match(/append\("GameTopLeft"/g)).toHaveLength(1);
  expect(index.match(/append\("Game"/g)).toHaveLength(1);
  expect(shell).toContain("InputActionConsumer");
  expect(shell).toContain("BACK_ACTIONS");
  expect(shell).not.toContain('addEventListener("keydown"');
});
