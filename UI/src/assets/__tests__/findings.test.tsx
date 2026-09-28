import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { FindingBadge } from "../components/FindingBadge";
import { EvidencePanel } from "../components/EvidencePanel";
import { CompareTab, normalizeCompareSelection } from "../tabs/CompareTab";
import { RenderStructure } from "../details/RenderStructure";
import { TextureDetails } from "../details/TextureDetails";
import type { AssetRow, UiFinding } from "../types";

const finding: UiFinding = {
  ruleId: "APA-LOD-002",
  status: "PotentialIssue",
  category: "Lod",
  title: "Weak LOD vertex reduction",
  explanation: "Heuristic evidence only.",
  evidence: ["asset=house", "vertexRetentionPercent=92"],
  basis: "Heuristic",
  ruleVersion: "1.0",
};

function row(id: string): AssetRow {
  return {
    prefabId: id,
    prefabType: "Building",
    displayName: id,
    sourceLabel: "Built-in",
    traits: ["Building"],
    countKind: "TopLevelObjects",
    instances: { availability: "Available", value: 1 },
    presence: "Present",
    renderCoverage: "Unknown",
    estimatedTexturePayload: { availability: "NotScanned" },
    findingCount: 0,
  };
}

describe("analysis findings UX", () => {
  it("shows finding status, evidence, basis, and rule version", () => {
    const html = renderToStaticMarkup(<><FindingBadge finding={finding} /><EvidencePanel finding={finding} /></>);
    expect(html).toContain("Potential Issue");
    expect(html).toContain("vertexRetentionPercent=92");
    expect(html).toContain("Heuristic");
    expect(html).toContain("1.0");
  });

  it("labels Unknown findings as Unknown rather than Observed", () => {
    const html = renderToStaticMarkup(<FindingBadge finding={{ ...finding, ruleId: "APA-TEX-001", status: "Unknown" }} />);
    expect(html).toContain(">Unknown<");
    expect(html).not.toContain("Observed");
  });

  it("unsupported render coverage is not rendered as zero geometry", () => {
    const html = renderToStaticMarkup(<RenderStructure coverage="Unsupported" relations={[]} />);
    expect(html).toContain("Unsupported");
    expect(html).not.toContain("0 geometry");
  });

  it("compare accepts two to four unique assets and never declares a winner", () => {
    expect(normalizeCompareSelection(["a", "b", "c", "d", "e"])).toEqual(["a", "b", "c", "d"]);
    const html = renderToStaticMarkup(<CompareTab assets={[row("a"), row("b")]} findings={[]} />);
    expect(html).toContain("a");
    expect(html).toContain("b");
    expect(html.toLowerCase()).not.toContain("winner");
    expect(html.toLowerCase()).not.toContain("best overall");
  });

  it("texture payload is explicitly estimated and not GPU residency", () => {
    const html = renderToStaticMarkup(<TextureDetails payload={{ availability: "Available", value: 1048576 }} />);
    expect(html).toContain("Estimated logical payload");
    expect(html).toContain("not GPU residency");
  });
});
