import { afterEach, beforeEach } from "vitest";
import { clearBindingValues, setBindingValue } from "./cs2ApiStub";

/** Publishes the game locale binding for every test in the calling file. */
export function useGameLocale(localeId: string): void {
  beforeEach(() => setBindingValue("CS2RuntimeAssetAuditor", "locale", localeId));
  afterEach(() => clearBindingValues());
}
