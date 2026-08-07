"use client";

import { useEffect, useState } from "react";
import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { CandidateTable } from "../widgets/CandidateTable";
import { useStepFiles } from "@/hooks/useStepFiles";
import { getStepSummary } from "@/lib/api/steps";
import type { NeoantigenCandidate } from "@/types/candidate";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

const PREDICTORS = ["MHCflurry 2.0", "BigMHC-EL"] as const;

export function PresentationPanel({ patientId, definition, state }: PanelProps) {
  const { refresh } = useStepFiles(patientId, definition.id);
  const [predictor, setPredictor] = useState<(typeof PREDICTORS)[number]>(PREDICTORS[0]);
  const [candidates, setCandidates] = useState<NeoantigenCandidate[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    getStepSummary(patientId, definition.id)
      .then((summary) => {
        if (!cancelled) setCandidates((summary?.candidates as NeoantigenCandidate[]) ?? []);
      })
      .catch(() => undefined)
      .finally(() => !cancelled && setIsLoading(false));
    return () => {
      cancelled = true;
    };
  }, [patientId, definition.id, state?.lastRunAt]);

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="flex items-end gap-6 rounded-md border border-rule bg-surface p-6">
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Predictor</span>
          <select
            value={predictor}
            onChange={(e) => setPredictor(e.target.value as (typeof PREDICTORS)[number])}
            className="rounded-md border border-rule-strong px-3 py-2 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {PREDICTORS.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </label>
        <StepRunButton
          patientId={patientId}
          stepId={definition.id}
          label="Predict presentation"
          parameters={{ predictor }}
          onComplete={refresh}
        />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <h3 className="mb-4 text-h2 text-ink">Ranked by presentation score</h3>
        <CandidateTable
          candidates={candidates}
          columns={["peptide", "wildType", "allele", "presentation"]}
          sortBy="presentationScore"
          isLoading={isLoading}
          emptyMessage="No predictions yet."
        />
      </div>
    </div>
  );
}
