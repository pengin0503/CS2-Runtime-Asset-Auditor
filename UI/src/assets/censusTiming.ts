import type { UiSummary } from "./types";

/**
 * Seconds between the object copy (censusCapturedAt) and the network-edge copy (censusNetworkCapturedAt). The
 * edges are copied only after every object reference is reduced, so the two counts describe the city at two
 * moments. Null when either time is missing or unreadable.
 */
export function censusNetworkLagSeconds(summary: Pick<UiSummary, "censusCapturedAt" | "censusNetworkCapturedAt">): number | null {
  if (!summary.censusCapturedAt || !summary.censusNetworkCapturedAt) return null;
  const objects = Date.parse(summary.censusCapturedAt);
  const network = Date.parse(summary.censusNetworkCapturedAt);
  if (Number.isNaN(objects) || Number.isNaN(network)) return null;
  return Math.max(0, (network - objects) / 1000);
}

export function formatLagSeconds(seconds: number): string {
  return seconds.toFixed(1);
}
