"use client";

import { useEffect, useState } from "react";
import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { DataTable, type DataTableColumn } from "@/components/common/DataTable";
import { useStepFiles } from "@/hooks/useStepFiles";
import { getStepSummary } from "@/lib/api/steps";
import { listFiles } from "@/lib/api/files";
import type { NeoantigenCandidate } from "@/types/candidate";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function FilteringPanel({ patientId, definition, state }: PanelProps) {
  const { refresh } = useStepFiles(patientId, definition.id);
  const [applyExpressionFilter, setApplyExpressionFilter] = useState(true);
  const [hasRna, setHasRna] = useState(false);
  const [removed, setRemoved] = useState<NeoantigenCandidate[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    listFiles(patientId, "01_upload")
      .then((files) => setHasRna(files.some((f) => f.fileKind === "rna")))
      .catch(() => setHasRna(false));
  }, [patientId]);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    getStepSummary(patientId, definition.id)
      .then((summary) => {
        if (!cancelled) setRemoved((summary?.removedCandidates as NeoantigenCandidate[]) ?? []);
      })
      .catch(() => undefined)
      .finally(() => !cancelled && setIsLoading(false));
    return () => {
      cancelled = true;
    };
  }, [patientId, definition.id, state?.lastRunAt]);

  const columns: DataTableColumn<NeoantigenCandidate>[] = [
    { key: "peptide", header: "Peptide", render: (c) => <span className="font-mono text-ui">{c.mutantPeptide}</span> },
    { key: "gene", header: "Gene", render: (c) => <span className="font-mono text-ui">{c.geneSymbol}</span> },
    { key: "reason", header: "Removal reason", render: (c) => c.removalReason ?? "—" },
  ];

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="flex flex-col gap-4 rounded-md border border-rule bg-surface p-6">
        <label className={`flex items-center gap-3 ${hasRna ? "" : "opacity-40"}`}>
          <input
            type="checkbox"
            checked={applyExpressionFilter && hasRna}
            disabled={!hasRna}
            onChange={(e) => setApplyExpressionFilter(e.target.checked)}
            className="h-4 w-4 accent-accent"
          />
          <span className="text-ui text-ink">Apply expression filter</span>
          {!hasRna && (
            <span className="text-small text-slate">
              (disabled — no RNA-seq uploaded in step 1)
            </span>
          )}
        </label>
        <StepRunButton
          patientId={patientId}
          stepId={definition.id}
          label="Run filtering"
          parameters={{ applyExpressionFilter: applyExpressionFilter && hasRna }}
          onComplete={refresh}
        />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <h3 className="mb-4 text-h2 text-ink">Removed candidates</h3>
        <DataTable
          data={removed}
          columns={columns}
          keyExtractor={(c) => c.candidateId}
          isLoading={isLoading}
          emptyMessage="Nothing removed yet."
        />
      </div>
    </div>
  );
}
