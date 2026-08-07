"use client";

import { useEffect, useState } from "react";
import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { ConstructDiagram } from "../widgets/ConstructDiagram";
import { Button } from "@/components/common/Button";
import { useStepFiles } from "@/hooks/useStepFiles";
import { getStepSummary } from "@/lib/api/steps";
import { downloadFile } from "@/lib/api/files";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import type { VaccineConstruct } from "@/types/candidate";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

const LINKERS = ["AAY", "GGS", "None (direct fusion)"] as const;

export function VaccineDesignPanel({ patientId, definition, state }: PanelProps) {
  const { outputFiles, refresh } = useStepFiles(patientId, definition.id);
  const showError = useToastStore((s) => s.error);
  const [linker, setLinker] = useState<(typeof LINKERS)[number]>(LINKERS[0]);
  const [construct, setConstruct] = useState<VaccineConstruct | null>(null);

  useEffect(() => {
    let cancelled = false;
    getStepSummary(patientId, definition.id)
      .then((summary) => {
        if (!cancelled && summary?.construct) setConstruct(summary.construct as VaccineConstruct);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [patientId, definition.id, state?.lastRunAt]);

  const fastaFile = outputFiles.find((f) => f.extension.toLowerCase() === ".fasta");
  const genbankFile = outputFiles.find((f) => f.extension.toLowerCase() === ".gb" || f.extension.toLowerCase() === ".gbk");

  const handleDownload = async (fileName: string) => {
    try {
      await downloadFile(patientId, definition.id, fileName);
    } catch (err) {
      showError("Download failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="flex items-end gap-6 rounded-md border border-rule bg-surface p-6">
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Linker</span>
          <select
            value={linker}
            onChange={(e) => setLinker(e.target.value as (typeof LINKERS)[number])}
            className="rounded-md border border-rule-strong px-3 py-2 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {LINKERS.map((l) => (
              <option key={l} value={l}>
                {l}
              </option>
            ))}
          </select>
        </label>
        <StepRunButton
          patientId={patientId}
          stepId={definition.id}
          label="Design vaccine sequence"
          parameters={{ linker }}
          onComplete={refresh}
        />
        <div className="ml-auto flex gap-3">
          <Button
            variant="secondary"
            disabled={!fastaFile}
            onClick={() => fastaFile && handleDownload(fastaFile.name)}
          >
            Download FASTA
          </Button>
          <Button
            variant="secondary"
            disabled={!genbankFile}
            onClick={() => genbankFile && handleDownload(genbankFile.name)}
          >
            Download GenBank
          </Button>
        </div>
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <h3 className="mb-4 text-h2 text-ink">Construct</h3>
        {construct ? (
          <ConstructDiagram construct={construct} showSequence />
        ) : (
          <p className="text-body text-slate">Not designed yet.</p>
        )}
      </div>

      <p className="text-small text-slate">
        This produces a sequence file, not a physical vaccine — manufacturing requires
        specialized facilities and regulatory approval.
      </p>
    </div>
  );
}
