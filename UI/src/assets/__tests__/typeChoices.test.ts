import fs from "node:fs";
import path from "node:path";
import { expect, test } from "vitest";
import { en, ja } from "../../i18n/messages";

// Type labels are looked up as `atab.type.${choice}`, which the MessageKey type cannot check.
test("every Asset type filter choice has an English and a Japanese label", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/assets/tabs/AssetsTab.tsx"), "utf8");
  const declaration = source.match(/const TYPE_CHOICES = \[([^\]]+)\]/);
  expect(declaration).not.toBeNull();
  const choices = [...declaration![1].matchAll(/"([^"]+)"/g)].map(match => match[1]);
  expect(choices).toEqual(expect.arrayContaining(["BuildingExtension", "Plant"]));
  for (const choice of choices) {
    const key = `atab.type.${choice}`;
    expect((en as Record<string, string>)[key], key).toBeTruthy();
    expect((ja as Record<string, string>)[key], key).toBeTruthy();
  }
});
