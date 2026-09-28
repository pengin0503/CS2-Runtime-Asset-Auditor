import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const root = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  resolve: {
    alias: {
      "cs2/api": path.resolve(root, "src/test/cs2ApiStub.ts"),
      "cs2/ui": path.resolve(root, "src/test/cs2UiStub.tsx"),
      "cs2/input": path.resolve(root, "src/test/cs2InputStub.tsx")
    }
  }
});
