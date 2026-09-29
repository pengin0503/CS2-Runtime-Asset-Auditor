import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { MetricBadge } from "./MetricBadge";
import { useGameLocale } from "../../test/locale";

useGameLocale("ja-JP");

describe("MetricBadge", () => {
  it("shows Japanese unavailable text and preserves the technical reason", () => {
    const html = renderToStaticMarkup(<MetricBadge confidence="Unavailable" availability="Unavailable" reason="member missing" />);
    expect(html).toContain("利用不可");
    expect(html).toContain("member missing");
  });

  it("localizes managed and indirect evidence labels", () => {
    expect(renderToStaticMarkup(<MetricBadge confidence="Managed" availability="Available" />)).toContain("管理コード");
    expect(renderToStaticMarkup(<MetricBadge confidence="Indirect" availability="Available" />)).toContain("間接");
  });
});
