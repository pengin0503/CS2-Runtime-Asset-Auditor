import React, { useMemo, useState } from "react";
import { EvidencePanel } from "../components/EvidencePanel";
import { ChoiceControl } from "../components/ChoiceControl";
import { FindingBadge } from "../components/FindingBadge";
import type { FindingCategory, FindingStatus, UiFinding } from "../types";
import { useText } from "../../i18n/locale";
import type { MessageKey } from "../../i18n/messages";

const statuses: Array<FindingStatus | "All"> = ["All", "Warning", "PotentialIssue", "Notice", "Observed", "Unknown"];
const categories: Array<FindingCategory | "All"> = ["All", "Geometry", "Lod", "Material", "Texture", "Exposure", "Integrity"];

// A large playset can produce thousands of findings; rendering them all at once makes Gameface sluggish.
export const FINDINGS_PAGE_SIZE = 100;

export function WarningsTab({ findings }: { findings: UiFinding[] }): React.JSX.Element {
  const { t } = useText();
  const [status, setStatus] = useState<FindingStatus | "All">("All");
  const [category, setCategory] = useState<FindingCategory | "All">("All");
  const [limit, setLimit] = useState(FINDINGS_PAGE_SIZE);
  const visible = useMemo(() => findings.filter((finding) =>
    (status === "All" || finding.status === status)
    && (category === "All" || finding.category === category)), [findings, status, category]);
  const shown = visible.slice(0, limit);
  const changeStatus = (value: FindingStatus | "All") => { setStatus(value); setLimit(FINDINGS_PAGE_SIZE); };
  const changeCategory = (value: FindingCategory | "All") => { setCategory(value); setLimit(FINDINGS_PAGE_SIZE); };

  return (
    <section className="apa__tab-content" aria-labelledby="apa-warnings-title">
      <div className="apa__section-heading"><div><p className="apa__eyebrow">{t("warnings.eyebrow")}</p><h2 id="apa-warnings-title">{t("warnings.title")}</h2></div><span className="apa__muted">{t("warnings.count", { count: visible.length })}</span></div>
      <div className="apa__filters">
        <ChoiceControl label={t("warnings.status")} value={status} choices={statuses.map(value => [value, t(`finding.status.${value}` as MessageKey)] as const)} onChange={changeStatus} />
        <ChoiceControl label={t("warnings.category")} value={category} choices={categories.map(value => [value, t(`finding.category.${value}` as MessageKey)] as const)} onChange={changeCategory} />
      </div>
      {visible.length === 0 ? <p>{t("warnings.empty")}</p> : shown.map((finding, index) => (
        <article className="apa__finding" key={`${finding.ruleId}:${finding.prefabType ?? ""}:${finding.prefabId ?? ""}:${index}`}>
          <FindingBadge finding={finding} />
          <EvidencePanel finding={finding} />
        </article>
      ))}
      {visible.length > shown.length ? <div className="apa__pager">
        <span className="apa__muted">{t("warnings.shown", { shown: shown.length, total: visible.length })}</span>
        <button type="button" className="apa__button" onClick={() => setLimit(limit + FINDINGS_PAGE_SIZE)}>
          {t("warnings.showMore", { count: Math.min(FINDINGS_PAGE_SIZE, visible.length - shown.length) })}
        </button>
      </div> : null}
    </section>
  );
}
