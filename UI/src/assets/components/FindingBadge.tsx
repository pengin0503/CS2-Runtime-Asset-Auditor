import React from "react";
import type { UiFinding } from "../types";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

const KNOWN = new Set(["PotentialIssue", "Warning", "Notice", "Unknown", "Observed"]);

export function FindingBadge({ finding }: { finding: UiFinding }): React.JSX.Element {
  const { t } = useText();
  const status = KNOWN.has(finding.status) ? finding.status : "Observed";
  return <span className={`apa__finding-badge apa__finding-badge--${finding.status.toLowerCase()}`}>{t(`finding.status.${status}` as MessageKey)}</span>;
}
