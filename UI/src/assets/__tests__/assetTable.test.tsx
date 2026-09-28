import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { VirtualAssetTable } from "../components/VirtualAssetTable";
import type { AssetPage, AssetRow } from "../types";

function row(
  prefabId: string,
  availability: "Available" | "NotScanned",
  value?: number,
  countKind: AssetRow["countKind"] = "TopLevelObjects",
): AssetRow {
  return {
    prefabId,
    displayName: prefabId,
    prefabType: "Building",
    sourceLabel: "Built-in",
    traits: ["Building"],
    countKind,
    instances: { availability, value },
    presence: "Unknown",
  };
}

describe("VirtualAssetTable", () => {
  it("labels unavailable counts and exposes the count kind beside zero", () => {
    const page: AssetPage = {
      offset: 0,
      limit: 2,
      totalCount: 2,
      items: [
        row("unscanned-building", "NotScanned", undefined, "TopLevelObjects"),
        row("empty-building", "Available", 0, "TopLevelObjects"),
      ],
    };
    const html = renderToStaticMarkup(<VirtualAssetTable page={page} />);

    expect(html).toContain("Not scanned");
    expect(html).toContain("Instances: 0 (Top-level objects)");
    expect(html).toContain("Top-level objects");
  });

  it("renders no more than the bounded page it received", () => {
    const items = Array.from({ length: 200 }, (_, index) =>
      row("asset-" + index, "Available", index),
    );
    const page: AssetPage = { offset: 0, limit: 200, totalCount: 1000, items };
    const html = renderToStaticMarkup(<VirtualAssetTable page={page} />);
    const renderedRows = html.match(/class="apa__asset-row"/g) ?? [];

    expect(renderedRows).toHaveLength(200);
    expect(html).not.toContain("<table");
    expect(html).toContain("1–200 of 1000");
  });
});
