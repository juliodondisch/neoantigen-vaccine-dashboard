"use client";

import { StepExplanation } from "../StepExplanation";
import { FileUploadZone } from "../FileUploadZone";
import { FileTable } from "../FileTable";
import { useStepFiles } from "@/hooks/useStepFiles";
import { formatBytes } from "@/lib/utils/format";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function UploadPanel({ patientId, definition, state }: PanelProps) {
  const { inputFiles, isLoading, refresh } = useStepFiles(patientId, definition.id);

  const totalBytes = inputFiles.reduce((sum, f) => sum + f.sizeBytes, 0);

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="grid grid-cols-1 gap-6 rounded-md border border-rule bg-surface p-6 md:grid-cols-3">
        <FileUploadZone
          patientId={patientId}
          stepId={definition.id}
          fileKind="tumor_dna"
          label="Tumor DNA"
          description="FASTQ or BAM"
          acceptedExtensions={[".fastq", ".fastq.gz", ".fq.gz", ".bam"]}
          required
          onUploaded={refresh}
        />
        <FileUploadZone
          patientId={patientId}
          stepId={definition.id}
          fileKind="normal_dna"
          label="Normal DNA"
          description="FASTQ or BAM"
          acceptedExtensions={[".fastq", ".fastq.gz", ".fq.gz", ".bam"]}
          required
          onUploaded={refresh}
        />
        <FileUploadZone
          patientId={patientId}
          stepId={definition.id}
          fileKind="rna"
          label="Tumor RNA-seq"
          description="Optional ,  improves expression filtering"
          acceptedExtensions={[".fastq", ".fastq.gz", ".fq.gz", ".bam"]}
          onUploaded={refresh}
        />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-h2 text-ink">Manifest</h3>
          <span className="font-mono text-small text-slate">
            {inputFiles.length} file(s) &middot; {formatBytes(totalBytes)}
          </span>
        </div>
        <FileTable
          patientId={patientId}
          stepId={definition.id}
          files={inputFiles}
          showDownload={false}
          showDelete
          onRefresh={refresh}
          emptyMessage={isLoading ? "Loading…" : "No files uploaded yet."}
        />
      </div>

      {state?.status === "Completed" && (
        <p className="text-small text-state-complete">Upload complete ,  proceed to alignment.</p>
      )}
    </div>
  );
}
