import { describe, expect, it } from "vitest";
import { formatByUnit, formatCount, formatMetricValue, formatPercent, shortMetricName } from "./format";

describe("metric formatting", () => {
  it("never renders unavailable metrics as zero", () => {
    expect(formatMetricValue({ value: 0, availability: "Unavailable" })).toBe("—");
    expect(formatMetricValue({ value: null, availability: "Available" })).toBe("—");
  });

  it("formats ratios as percentages", () => {
    expect(formatPercent(0.875)).toBe("87.5%");
    expect(formatPercent(null)).toBe("—");
  });

  it("removes profiler category separators from marker names", () => {
    expect(shortMetricName("CPU\u001fMain Thread")).toBe("Main Thread");
  });
});

describe("unit-aware formatting of values seen in in-game reports", () => {
  it("never prints raw nanoseconds or bytes", () => {
    expect(formatByUnit(44_673_900, "TimeNanoseconds")).toBe("44.67 ms");
    expect(formatByUnit(160_822_608, "Bytes")).toBe("153.37 MiB");
    expect(formatByUnit(502.4926, "Milliseconds")).toBe("502.49 ms");
    expect(formatByUnit(0.548, "Ratio")).toBe("54.8%");
    expect(formatByUnit(2.19, "Speed")).toBe("2.19×");
    expect(formatByUnit(59.94, "FramesPerSecond")).toBe("59.9 fps");
  });

  it("groups plain integer counts instead of adding decimals", () => {
    expect(formatByUnit(36_378_653, "")).toBe("36,378,653");
    expect(formatByUnit(-124.84, undefined)).toBe("-124.84");
    expect(formatCount(42)).toBe("42");
  });
});
