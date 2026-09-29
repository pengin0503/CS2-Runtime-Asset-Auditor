import React from "react";
import type { UiFinding } from "../types";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";
import { findingExplanation, findingTitle } from "../findingText";

export function EvidencePanel({ finding }: { finding: UiFinding }): React.JSX.Element {
  const { locale, t } = useText();
  const title = findingTitle(finding, locale);
  const basisKey = `basis.${finding.basis}` as MessageKey;
  const basis = t(basisKey) === basisKey ? finding.basis : t(basisKey);
  return (
    <section className="apa__evidence" aria-label={title}>
      <h4>{title}</h4>
      <p>{findingExplanation(finding, locale)}</p>
      <dl>
        <dt>{t("evidence.evidence")}</dt>
        <dd>{finding.evidence.length > 0 ? finding.evidence.join(" · ") : t("evidence.none")}</dd>
        <dt>{t("evidence.basis")}</dt>
        <dd>{basis}</dd>
        <dt>{t("evidence.ruleVersion")}</dt>
        <dd>{finding.ruleVersion}</dd>
        <dt>{t("evidence.ruleId")}</dt>
        <dd>{finding.ruleId}</dd>
      </dl>
    </section>
  );
}
