import React from "react";
import type { UiFinding } from "../types";

function label(status: UiFinding["status"]): string {
  switch (status) {
    case "PotentialIssue": return "Potential Issue";
    case "Warning": return "Warning";
    case "Notice": return "Notice";
    case "Unknown": return "Unknown";
    default: return "Observed";
  }
}

export function FindingBadge({ finding }: { finding: UiFinding }): React.JSX.Element {
  return <span className={`apa__finding-badge apa__finding-badge--${finding.status.toLowerCase()}`}>{label(finding.status)}</span>;
}
