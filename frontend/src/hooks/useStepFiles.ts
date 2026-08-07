"use client";

import { useCallback, useEffect, useState } from "react";
import { listFiles } from "@/lib/api/files";
import type { ManagedFile } from "@/types/file";
import type { StepId } from "@/types/step";

export function useStepFiles(
  patientId: string,
  stepId: StepId
): {
  inputFiles: ManagedFile[];
  outputFiles: ManagedFile[];
  isLoading: boolean;
  refresh: () => Promise<void>;
} {
  const [inputFiles, setInputFiles] = useState<ManagedFile[]>([]);
  const [outputFiles, setOutputFiles] = useState<ManagedFile[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      const files = await listFiles(patientId, stepId);
      setInputFiles(files.filter((f) => f.isUserUploaded));
      setOutputFiles(files.filter((f) => !f.isUserUploaded));
    } catch {
      // leave previous file lists in place; caller decides how to surface it
    } finally {
      setIsLoading(false);
    }
  }, [patientId, stepId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return { inputFiles, outputFiles, isLoading, refresh };
}
