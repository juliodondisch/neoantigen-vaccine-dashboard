"use client";

import { useState } from "react";
import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { FileTable } from "../FileTable";
import { FileUploadZone } from "../FileUploadZone";
import { useStepFiles } from "@/hooks/useStepFiles";
import { formatPercent } from "@/lib/utils/format";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function AlignmentPanel({ patientId, definition, state }: PanelProps) {
  const { inputFiles, outputFiles, refresh } = useStepFiles(patientId, definition.id);
  const [threads, setThreads] = useState(4);

  const alreadyAligned = inputFiles.some((f) => f.extension.toLowerCase() === ".bam");
  const mappingRate = state?.lastSummary?.mappingRate as number | undefined;

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      {alreadyAligned && (
        <div className="rounded-md border border-rule bg-feedback-infoBg p-4 text-ui text-ink">
          Uploaded inputs already include BAM files ,  alignment may be a no-op pass-through
          for those samples.
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 rounded-md border border-rule bg-surface p-6 md:grid-cols-2">
        <FileUploadZone
          patientId={patientId}
          stepId={definition.id}
          fileKind="tumor_dna"
          label="Tumor BAM"
          description="Already-aligned BAM ,  skip alignment for this sample"
          acceptedExtensions={[".bam"]}
          allowServerPath
          onUploaded={refresh}
        />
        <FileUploadZone
          patientId={patientId}
          stepId={definition.id}
          fileKind="normal_dna"
          label="Normal BAM"
          description="Already-aligned BAM ,  skip alignment for this sample"
          acceptedExtensions={[".bam"]}
          allowServerPath
          onUploaded={refresh}
        />
      </div>

      <div className="flex items-end gap-6 rounded-md border border-rule bg-surface p-6">
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Threads</span>
          <input
            type="number"
            min={1}
            max={32}
            value={threads}
            onChange={(e) => setThreads(Number(e.target.value))}
            className="w-24 rounded-md border border-rule-strong px-3 py-2 font-mono text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
        </label>
        <StepRunButton
          patientId={patientId}
          stepId={definition.id}
          label="Run alignment"
          parameters={{ threads }}
          onComplete={refresh}
        />
        {mappingRate !== undefined && (
          <span className="ml-auto text-ui text-slate">
            Mapping rate: <span className="font-mono text-ink">{formatPercent(mappingRate)}</span>
          </span>
        )}
      </div>

      <FileTable
        patientId={patientId}
        stepId={definition.id}
        files={outputFiles}
        title="Output BAMs"
        showPreview
        emptyMessage="Not run yet."
        onRefresh={refresh}
      />
    </div>
  );
}
