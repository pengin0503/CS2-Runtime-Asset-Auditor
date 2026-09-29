import type { UiMetricRow } from "./bindings";

export function formatNumber(value: number | null | undefined, digits = 2): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return value.toFixed(digits);
}

export function formatMilliseconds(value: number | null | undefined): string {
  const formatted = formatNumber(value, 2);
  return formatted === "—" ? formatted : `${formatted} ms`;
}

export function formatSpeed(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  const digits = Number.isInteger(value) ? 0 : Math.abs(value) >= 10 ? 1 : 2;
  return `${Number(value.toFixed(digits))}×`;
}

export function formatFramesPerSecond(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return `${value.toFixed(1)} fps`;
}

export function formatPercent(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return `${(value * 100).toFixed(1)}%`;
}

// Explicit grouping instead of toLocaleString: the Gameface JS runtime is not guaranteed to ship Intl data.
function groupDigits(integer: number): string {
  const sign = integer < 0 ? "-" : "";
  return sign + String(Math.abs(integer)).replace(/\B(?=(\d{3})+(?!\d))/g, ",");
}

export function formatInteger(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return groupDigits(Math.round(value));
}

/** Plain counts print as grouped integers; fractional values keep two decimals. */
export function formatCount(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return Number.isInteger(value) ? groupDigits(value) : formatNumber(value, 2);
}

export function formatBytes(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  const absolute = Math.abs(value);
  if (absolute >= 1024 ** 3) return `${(value / (1024 ** 3)).toFixed(2)} GiB`;
  if (absolute >= 1024 ** 2) return `${(value / (1024 ** 2)).toFixed(2)} MiB`;
  if (absolute >= 1024) return `${(value / 1024).toFixed(2)} KiB`;
  return `${Math.round(value)} B`;
}

/**
 * Formats a raw value by its unit identifier. Unity recorder units ("TimeNanoseconds", "Bytes") pass through
 * from C#; "Milliseconds", "Ratio", "Speed" and "FramesPerSecond" describe values the profiler derives itself.
 */
export function formatByUnit(value: number | null | undefined, unitType?: string): string {
  if (value == null || !Number.isFinite(value)) return "—";
  switch (unitType) {
    case "TimeNanoseconds": return formatMilliseconds(value / 1_000_000);
    case "Milliseconds": return formatMilliseconds(value);
    case "Bytes": return formatBytes(value);
    case "Ratio": return formatPercent(value);
    case "Speed": return formatSpeed(value);
    case "FramesPerSecond": return formatFramesPerSecond(value);
    default: return formatCount(value);
  }
}

export function formatMetricValue(metric: Pick<UiMetricRow, "value" | "availability"> & { unitType?: string }): string {
  if (metric.availability !== "Available") return "—";
  return formatByUnit(metric.value, metric.unitType);
}

export function shortMetricName(id: string): string {
  const parts = id.split("\u001f");
  return parts[parts.length - 1] || id;
}
