"use client";

import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { FileTable } from "../FileTable";
import { VafHistogram } from "../widgets/VafHistogram";
import { useStepFiles } from "@/hooks/useStepFiles";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function VariantPanel({ patientId, definition, state }: PanelProps) {
  const { outputFiles, refresh } = useStepFiles(patientId, definition.id);

  const summary = state?.lastSummary ?? {};
  const variantCount = (summary.variantCount as number) ?? 0;
  const vafValues = (summary.vafValues as number[]) ?? [];
  const filterReasons = (summary.filterReasonBreakdown as Record<string, number>) ?? {};

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="rounded-md border border-rule bg-surface p-6">
        <StepRunButton patientId={patientId} stepId={definition.id} label="Call variants" onComplete={refresh} />
      </div>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div className="rounded-md border border-rule bg-surface p-6">
          <h3 className="text-h2 text-ink">VAF distribution</h3>
          <p className="mb-4 text-small text-slate">
            {variantCount} PASS variant{variantCount === 1 ? "" : "s"}
          </p>
          <VafHistogram vafValues={vafValues} />
        </div>

        <div className="rounded-md border border-rule bg-surface p-6">
          <h3 className="mb-4 text-h2 text-ink">Filter reasons</h3>
          {Object.keys(filterReasons).length === 0 ? (
            <p className="text-body text-slate">No filtered calls recorded yet.</p>
          ) : (
            <table className="w-full text-ui">
              <tbody>
                {Object.entries(filterReasons).map(([reason, count]) => (
                  <tr key={reason} className="border-b border-rule last:border-b-0">
                    <td className="py-2 text-ink">{reason}</td>
                    <td className="py-2 text-right font-mono text-ink">{count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <FileTable
        patientId={patientId}
        stepId={definition.id}
        files={outputFiles}
        title="Output VCF"
        showPreview
        emptyMessage="Not run yet."
        onRefresh={refresh}
      />
    </div>
  );
}
