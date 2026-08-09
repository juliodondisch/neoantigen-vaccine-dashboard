"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/common/Button";
import { useStepStore } from "@/stores/useStepStore";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import type { StepId, StepResult } from "@/types/step";

interface StepRunButtonProps {
  patientId: string;
  stepId: StepId;
  label?: string;
  parameters?: Record<string, unknown>;
  disabled?: boolean;
  disabledReason?: string;
  onComplete?: (result: StepResult) => void;
}

export function StepRunButton({
  patientId,
  stepId,
  label = "Run step",
  parameters,
  disabled,
  disabledReason,
  onComplete,
}: StepRunButtonProps) {
  const runStep = useStepStore((s) => s.runStep);
  const activeJob = useStepStore((s) => s.activeJobs[stepId]);
  const showError = useToastStore((s) => s.error);
  const showSuccess = useToastStore((s) => s.success);
  const [isStarting, setIsStarting] = useState(false);
  const watchedJobId = useRef<string | null>(null);

  const isRunning = isStarting || activeJob?.status === "Running" || activeJob?.status === "Queued";

  useEffect(() => {
    if (!activeJob || activeJob.jobId !== watchedJobId.current) return;
    if (activeJob.status === "Succeeded") {
      showSuccess(`${label} succeeded`, activeJob.result?.message);
      if (activeJob.result) onComplete?.(activeJob.result);
      watchedJobId.current = null;
    } else if (activeJob.status === "Failed") {
      showError(`${label} failed`, activeJob.errorMessage);
      watchedJobId.current = null;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeJob]);

  const handleClick = async () => {
    setIsStarting(true);
    try {
      const jobId = await runStep(patientId, stepId, parameters);
      watchedJobId.current = jobId;
    } catch (err) {
      showError(
        "Could not start step",
        isApiError(err) ? err.detail ?? err.message : "Unexpected error"
      );
    } finally {
      setIsStarting(false);
    }
  };

  return (
    <div className="flex flex-col gap-1.5">
      <Button
        onClick={handleClick}
        isLoading={isRunning}
        disabled={disabled || isRunning}
      >
        {label}
      </Button>
      {disabled && disabledReason && (
        <span className="text-small text-slate">{disabledReason}</span>
      )}
      {isRunning && activeJob?.logTail && (
        <pre className="max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-md bg-paper p-2 font-mono text-small text-slate">
          {activeJob.logTail}
        </pre>
      )}
    </div>
  );
}
