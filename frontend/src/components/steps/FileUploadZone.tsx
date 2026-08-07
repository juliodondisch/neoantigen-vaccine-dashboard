"use client";

import { useRef, useState } from "react";
import { cn } from "@/lib/utils/cn";
import { useFileUpload } from "@/hooks/useFileUpload";
import { useToastStore } from "@/stores/useToastStore";
import type { FileKind, ManagedFile } from "@/types/file";
import type { StepId } from "@/types/step";

interface FileUploadZoneProps {
  patientId: string;
  stepId: StepId;
  fileKind: FileKind;
  label: string;
  description?: string;
  acceptedExtensions: string[];
  required?: boolean;
  allowServerPath?: boolean;
  onUploaded: (files: ManagedFile[]) => void;
}

export function FileUploadZone({
  patientId,
  stepId,
  fileKind,
  label,
  description,
  acceptedExtensions,
  required,
  allowServerPath = true,
  onUploaded,
}: FileUploadZoneProps) {
  const showError = useToastStore((s) => s.error);
  const showSuccess = useToastStore((s) => s.success);
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [serverPath, setServerPath] = useState("");
  const [showPathInput, setShowPathInput] = useState(false);

  const { upload, registerPath, progress, isUploading } = useFileUpload({
    patientId,
    stepId,
    fileKind,
    onSuccess: (files) => {
      showSuccess(`${label} uploaded`, `${files.length} file(s)`);
      onUploaded(files);
    },
    onError: (message) => showError(`${label} upload failed`, message),
  });

  const handleFiles = (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    void upload(Array.from(fileList));
  };

  const handlePathSubmit = () => {
    if (!serverPath.trim()) return;
    void registerPath(serverPath.trim());
    setServerPath("");
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-baseline justify-between">
        <span className="text-ui text-ink">
          {label}
          {required && <span className="ml-1 text-state-failed">*</span>}
        </span>
        <span className="font-mono text-small text-slate">
          {acceptedExtensions.join(", ")}
        </span>
      </div>
      {description && <p className="text-small text-slate">{description}</p>}

      <div
        role="button"
        tabIndex={0}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") inputRef.current?.click();
        }}
        onDragOver={(e) => {
          e.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setIsDragging(false);
          handleFiles(e.dataTransfer.files);
        }}
        className={cn(
          "flex flex-col items-center justify-center gap-2 rounded-md border border-dashed px-6 py-8 text-center transition-colors",
          isDragging ? "border-accent bg-accent-muted" : "border-rule-strong hover:bg-paper"
        )}
      >
        <span className="text-ui text-ink">
          Drop files here or click to browse
        </span>
        <span className="text-small text-slate">
          Large files (100GB+) ,  consider a server path below instead.
        </span>
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={acceptedExtensions.join(",")}
          className="hidden"
          onChange={(e) => handleFiles(e.target.files)}
        />
      </div>

      {isUploading && progress.length > 0 && (
        <div className="flex flex-col gap-1.5">
          {progress.map((p) => (
            <div key={p.fileName} className="flex items-center gap-3">
              <span className="w-40 truncate font-mono text-small text-slate">
                {p.fileName}
              </span>
              <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-rule">
                <div
                  className="h-full bg-accent transition-[width]"
                  style={{ width: `${p.percent}%` }}
                />
              </div>
              <span className="w-10 text-right font-mono text-small text-slate">
                {p.percent}%
              </span>
            </div>
          ))}
        </div>
      )}

      {allowServerPath && (
        <div>
          <button
            type="button"
            onClick={() => setShowPathInput((v) => !v)}
            className="text-small text-accent hover:text-accent-hover"
          >
            {showPathInput ? "Hide server path option" : "Point at a path on disk instead"}
          </button>
          {showPathInput && (
            <div className="mt-2 flex gap-2">
              <input
                value={serverPath}
                onChange={(e) => setServerPath(e.target.value)}
                placeholder="/data/incoming/tumor.bam"
                className="flex-1 rounded-md border border-rule-strong px-3 py-2 font-mono text-small text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
              />
              <button
                type="button"
                onClick={handlePathSubmit}
                className="rounded-md border border-rule-strong px-3 py-2 text-small text-ink hover:bg-paper"
              >
                Register
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
