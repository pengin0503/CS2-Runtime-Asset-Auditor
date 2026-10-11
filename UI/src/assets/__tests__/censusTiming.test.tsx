import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { censusNetworkLagSeconds } from "../censusTiming";
import { CensusTab } from "../tabs/CensusTab";
import { OverviewTab } from "../tabs/OverviewTab";
import { EMPTY_UI_SNAPSHOT, type AssetAuditorBindings, type UiSnapshot } from "../types";

const bindings: AssetAuditorBindings = {
  requestCensus() {}, requestAssetAudit() {}, requestDeepInspection() {}, cancelCensus() {},
  requestAssetsPage() {}, requestExport() {}, updateSettings() {},
};

function scanned(network: string | null): UiSnapshot {
  return {
    ...EMPTY_UI_SNAPSHOT,
    summary: {
      ...EMPTY_UI_SNAPSHOT.summary,
      censusWasScanned: true,
      censusMatchesCatalog: true,
      censusCapturedAt: "2026-10-11T10:00:00.0000000+00:00",
      censusNetworkCapturedAt: network,
    },
  };
}

describe("Census capture times per domain", () => {
  it("measures the lag from the object copy to the network-edge copy", () => {
    expect(censusNetworkLagSeconds(scanned("2026-10-11T10:00:12.5000000+00:00").summary)).toBe(12.5);
    expect(censusNetworkLagSeconds(scanned(null).summary)).toBeNull();
    expect(censusNetworkLagSeconds({ censusCapturedAt: "not a time", censusNetworkCapturedAt: "2026-10-11T10:00:00Z" })).toBeNull();
  });

  it("shows both capture times and the lag on the Census tab", () => {
    const html = renderToStaticMarkup(<CensusTab snapshot={scanned("2026-10-11T10:00:12.5000000+00:00")} />);
    expect(html).toContain("Objects were copied at 2026-10-11T10:00:00.0000000+00:00 and network edges at 2026-10-11T10:00:12.5000000+00:00, 12.5 s later.");
  });

  it("says network edges were not scanned when only objects were copied", () => {
    const html = renderToStaticMarkup(<CensusTab snapshot={scanned(null)} />);
    expect(html).toContain("Network edges were not scanned.");
    expect(html).not.toContain("s later");
  });

  it("adds the network-edge time to the overview metadata", () => {
    const html = renderToStaticMarkup(<OverviewTab snapshot={scanned("2026-10-11T10:00:03.0000000+00:00")} bindings={bindings} />);
    expect(html).toContain("Network edges captured 2026-10-11T10:00:03.0000000+00:00, 3.0 s after the objects");
  });
});
