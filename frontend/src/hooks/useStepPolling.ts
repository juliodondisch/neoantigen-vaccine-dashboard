"use client";

import { useEffect, useRef, useState } from "react";
import { getJob } from "@/lib/api/steps";
import { isApiError } from "@/lib/api/client";
import { POLL_INTERVAL_MS } from "@/lib/constants/config";
import type { JobRecord } from "@/types/step";
import type { StepId } from "@/types/step";

interface UseStepPollingOptions {
  patientId: string;
  stepId: StepId;
  jobId: string | null;
  intervalMs?: number;
  onComplete?: (job: JobRecord) => void;
  onError?: (error: string) => void;
}

const TERMINAL_STATUSES = new Set(["Succeeded", "Failed", "Cancelled"]);

export function useStepPolling(options: UseStepPollingOptions): {
  job: JobRecord | null;
  isPolling: boolean;
  stop: () => void;
} {
  const { patientId, stepId, jobId, intervalMs, onComplete, onError } = options;
  const [job, setJob] = useState<JobRecord | null>(null);
  const [isPolling, setIsPolling] = useState(false);
  const stoppedRef = useRef(false);
  const onCompleteRef = useRef(onComplete);
  const onErrorRef = useRef(onError);
  onCompleteRef.current = onComplete;
  onErrorRef.current = onError;

  useEffect(() => {
    if (!jobId) {
      setJob(null);
      setIsPolling(false);
      return;
    }

    stoppedRef.current = false;
    setIsPolling(true);

    const tick = async () => {
      if (stoppedRef.current) return;
      try {
        const result = await getJob(patientId, stepId, jobId);
        if (stoppedRef.current) return;
        setJob(result);
        if (TERMINAL_STATUSES.has(result.status)) {
          setIsPolling(false);
          if (result.status === "Succeeded") {
            onCompleteRef.current?.(result);
          } else if (result.status === "Failed") {
            onErrorRef.current?.(result.errorMessage ?? "Step failed");
          }
          return;
        }
      } catch (err) {
        if (stoppedRef.current) return;
        setIsPolling(false);
        onErrorRef.current?.(
          isApiError(err) ? err.message : "Failed to poll job status"
        );
        return;
      }
      if (!stoppedRef.current) {
        timeoutRef.current = setTimeout(tick, intervalMs ?? POLL_INTERVAL_MS);
      }
    };

    const timeoutRef = { current: null as ReturnType<typeof setTimeout> | null };
    void tick();

    return () => {
      stoppedRef.current = true;
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, [patientId, stepId, jobId, intervalMs]);

  const stop = () => {
    stoppedRef.current = true;
    setIsPolling(false);
  };

  return { job, isPolling, stop };
}
