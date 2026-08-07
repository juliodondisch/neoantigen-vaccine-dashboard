"use client";

import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { FileTable } from "../FileTable";
import { ConsequenceChart } from "../widgets/ConsequenceChart";
import { useStepFiles } from "@/hooks/useStepFiles";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function ProteinEffectsPanel({ patientId, definition, state }: PanelProps) {
  const { outputFiles, refresh } = useStepFiles(patientId, definition.id);

  const summary = state?.lastSummary ?? {};
  const counts = (summary.consequenceCounts as Record<string, number>) ?? {};
  const kept = (summary.kept as number) ?? 0;
  const discarded = (summary.discarded as number) ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="rounded-md border border-rule bg-surface p-6">
        <StepRunButton patientId={patientId} stepId={definition.id} label="Annotate effects" onComplete={refresh} />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-h2 text-ink">Consequences</h3>
          <span className="text-ui text-slate">
            <span className="font-mono text-state-complete">{kept}</span> kept &middot;{" "}
            <span className="font-mono text-state-failed">{discarded}</span> discarded
          </span>
        </div>
        <ConsequenceChart counts={counts} />
      </div>

      <FileTable
        patientId={patientId}
        stepId={definition.id}
        files={outputFiles}
        title="Annotated VCF"
        showPreview
        emptyMessage="Not run yet."
        onRefresh={refresh}
      />
    </div>
  );
}
