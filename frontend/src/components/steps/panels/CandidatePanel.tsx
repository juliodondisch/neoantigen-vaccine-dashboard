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

export function CandidatePanel({ patientId, definition, state }: PanelProps) {
  const { refresh } = useStepFiles(patientId, definition.id);
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

  const total = (state?.lastSummary?.candidateCount as number) ?? candidates.length;

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="rounded-md border border-rule bg-surface p-6">
        <StepRunButton patientId={patientId} stepId={definition.id} label="Generate candidates" onComplete={refresh} />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-h2 text-ink">Candidate peptides</h3>
          <span className="font-mono text-ui text-slate">{total.toLocaleString()} total</span>
        </div>
        <CandidateTable
          candidates={candidates}
          columns={["peptide", "wildType", "allele", "gene"]}
          maxRows={50}
          highlightSelected={false}
          isLoading={isLoading}
          emptyMessage="No candidates generated yet."
        />
      </div>
    </div>
  );
}
