import { describe, expect, it, vi } from "vitest";

const send = vi.hoisted(() => vi.fn());
vi.mock("cs2/api", () => ({ bindValue: vi.fn(() => ({ value: null })), useValue: vi.fn(), trigger: send }));

import { EMPTY_ADVISOR, startAdvisorExperiment, applyAdvisorExperiment,
  startAdvisorExperimentFollowUp, cancelAdvisorExperiment, keepAdvisorExperiment,
  undoAdvisorExperiment } from "./bindings";

describe("investigation experiment bindings", () => {
  it("shows no observed experiment before one starts", () => {
    expect(EMPTY_ADVISOR.experiment).toBeNull();
  });

  it("sends each explicit command with its intended arguments", () => {
    startAdvisorExperiment("capture", "shadow", "Low");
    applyAdvisorExperiment();
    applyAdvisorExperiment(true);
    startAdvisorExperimentFollowUp();
    cancelAdvisorExperiment();
    keepAdvisorExperiment();
    undoAdvisorExperiment(true);
    expect(send.mock.calls.map((call) => call.slice(1))).toEqual([
      ["advisorStartExperiment", "capture", "shadow", "Low"],
      ["advisorApplyExperiment", false], ["advisorApplyExperiment", true],
      ["advisorStartExperimentFollowUp"], ["advisorCancelExperiment"],
      ["advisorKeepExperiment"], ["advisorUndoExperiment", true]
    ]);
  });
});
