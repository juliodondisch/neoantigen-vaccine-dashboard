"use client";

import { useState } from "react";
import { registerFilePath, uploadFiles } from "@/lib/api/files";
import { isApiError } from "@/lib/api/client";
import type { FileKind, ManagedFile, UploadProgress } from "@/types/file";
import type { StepId } from "@/types/step";

interface UseFileUploadOptions {
  patientId: string;
  stepId: StepId;
  fileKind?: FileKind;
  onSuccess?: (files: ManagedFile[]) => void;
  onError?: (error: string) => void;
}

export function useFileUpload(options: UseFileUploadOptions): {
  upload: (files: File[]) => Promise<void>;
  registerPath: (path: string) => Promise<void>;
  progress: UploadProgress[];
  isUploading: boolean;
  reset: () => void;
} {
  const { patientId, stepId, fileKind, onSuccess, onError } = options;
  const [progress, setProgress] = useState<UploadProgress[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  const upload = async (files: File[]) => {
    setIsUploading(true);
    setProgress(
      files.map((f) => ({
        fileName: f.name,
        loaded: 0,
        total: f.size,
        percent: 0,
        status: "pending",
      }))
    );
    try {
      const response = await uploadFiles(patientId, stepId, files, fileKind, (p) => {
        setProgress((prev) =>
          prev.map((entry) => ({
            ...entry,
            percent: p,
            loaded: Math.round((p / 100) * entry.total),
            status: p >= 100 ? "complete" : "uploading",
          }))
        );
      });
      if (response.success) {
        setProgress((prev) => prev.map((entry) => ({ ...entry, status: "complete", percent: 100 })));
        onSuccess?.(response.uploadedFiles);
      } else {
        const message = response.error ?? "Upload failed";
        setProgress((prev) => prev.map((entry) => ({ ...entry, status: "error", error: message })));
        onError?.(message);
      }
    } catch (err) {
      const message = isApiError(err)
        ? err.detail ?? err.message
        : "Upload failed";
      setProgress((prev) => prev.map((entry) => ({ ...entry, status: "error", error: message })));
      onError?.(message);
    } finally {
      setIsUploading(false);
    }
  };

  const registerPath = async (path: string) => {
    setIsUploading(true);
    try {
      const response = await registerFilePath(patientId, stepId, path, fileKind, false);
      if (response.success) {
        onSuccess?.(response.uploadedFiles);
      } else {
        onError?.(response.error ?? "Could not register path");
      }
    } catch (err) {
      const message = isApiError(err) ? err.detail ?? err.message : "Could not register path";
      onError?.(message);
    } finally {
      setIsUploading(false);
    }
  };

  const reset = () => {
    setProgress([]);
    setIsUploading(false);
  };

  return { upload, registerPath, progress, isUploading, reset };
}
