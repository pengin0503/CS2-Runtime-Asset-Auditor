import { describe, expect, it } from "vitest";
import { createAssetQuery, formatObservation, updateAssetQueryState } from "../bindings";
import { MAX_ASSET_PAGE_SIZE, DEFAULT_ASSET_QUERY_STATE } from "../types";

describe("asset query state", () => {
  it("caps page requests and keeps the requested result window bounded", () => {
    const request = createAssetQuery({
      ...DEFAULT_ASSET_QUERY_STATE,
      offset: 400,
      pageSize: 10000,
    });

    expect(request.offset).toBe(400);
    expect(request.limit).toBe(MAX_ASSET_PAGE_SIZE);
  });

  it("resets the page window when a filter changes", () => {
    const next = updateAssetQueryState(
      { ...DEFAULT_ASSET_QUERY_STATE, offset: 200 },
      { searchText: "oak" },
    );

    expect(next.searchText).toBe("oak");
    expect(next.offset).toBe(0);
  });

  it("keeps available zero distinct from not scanned", () => {
    expect(formatObservation({ availability: "Available", value: 0 })).toBe("0");
    expect(formatObservation({ availability: "NotScanned" })).toBe("Not scanned");
  });
});
