"use client";

import { useEffect, useState } from "react";
import { StepExplanation } from "../StepExplanation";
import { StepRunButton } from "../StepRunButton";
import { HlaAlleleList } from "../widgets/HlaAlleleList";
import { useStepFiles } from "@/hooks/useStepFiles";
import { useToastStore } from "@/stores/useToastStore";
import { getStepSummary } from "@/lib/api/steps";
import type { HlaProfile } from "@/types/candidate";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

export function HlaTypingPanel({ patientId, definition, state }: PanelProps) {
  const { refresh } = useStepFiles(patientId, definition.id);
  const showInfo = useToastStore((s) => s.info);
  const [profile, setProfile] = useState<HlaProfile | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    getStepSummary(patientId, definition.id)
      .then((summary) => {
        if (!cancelled && summary?.hlaProfile) {
          setProfile(summary.hlaProfile as HlaProfile);
        }
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

      <div className="rounded-md border border-rule bg-surface p-6">
        <StepRunButton patientId={patientId} stepId={definition.id} label="Type HLA" onComplete={refresh} />
      </div>

      <div className="rounded-md border border-rule bg-surface p-6">
        <h3 className="mb-4 text-h2 text-ink">Patient HLA type</h3>
        <HlaAlleleList
          profile={profile}
          isLoading={isLoading}
          allowManualOverride
          onOverride={(alleles) => showInfo("Manual override recorded", alleles.join(", "))}
        />
      </div>
    </div>
  );
}
