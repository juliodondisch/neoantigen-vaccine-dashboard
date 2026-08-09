"use client";

import { DataTable, type DataTableColumn } from "@/components/common/DataTable";
import { downloadFile, deleteFile, previewFile } from "@/lib/api/files";
import { formatBytes, formatDate } from "@/lib/utils/format";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import type { ManagedFile } from "@/types/file";
import type { StepId } from "@/types/step";
import { useState } from "react";
import { Modal } from "@/components/common/Modal";

interface FileTableProps {
  patientId: string;
  stepId: StepId;
  files: ManagedFile[];
  title?: string;
  showDownload?: boolean;
  showPreview?: boolean;
  showDelete?: boolean;
  emptyMessage?: string;
  onRefresh?: () => void;
}

export function FileTable({
  patientId,
  stepId,
  files,
  title,
  showDownload = true,
  showPreview = false,
  showDelete = false,
  emptyMessage = "No files yet.",
  onRefresh,
}: FileTableProps) {
  const showError = useToastStore((s) => s.error);
  const [previewing, setPreviewing] = useState<{ name: string; content: string } | null>(null);

  // Mirrors the backend's NonPreviewableExtensions guard in FileSystemService.ReadTextFile —
  // the backend already refuses to read these as text, but hiding the button avoids a round
  // trip that always comes back as a placeholder message.
  const isPreviewable = (name: string) =>
    !/\.(bam|bai|cram|crai|gz|bz2|fastq|fq|pdf)$/i.test(name);

  const handleDownload = async (file: ManagedFile) => {
    try {
      await downloadFile(patientId, stepId, file.name);
    } catch (err) {
      showError("Download failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  const handlePreview = async (file: ManagedFile) => {
    try {
      const content = await previewFile(patientId, stepId, file.name, 200);
      setPreviewing({ name: file.name, content });
    } catch (err) {
      showError("Preview failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  const handleDelete = async (file: ManagedFile) => {
    try {
      await deleteFile(patientId, stepId, file.name);
      onRefresh?.();
    } catch (err) {
      showError("Delete failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  const columns: DataTableColumn<ManagedFile>[] = [
    { key: "name", header: "Name", render: (f) => <span className="font-mono text-small">{f.name}</span> },
    { key: "size", header: "Size", align: "right", render: (f) => <span className="font-mono">{formatBytes(f.sizeBytes)}</span> },
    { key: "kind", header: "Kind", render: (f) => f.fileKind ?? ", " },
    { key: "modified", header: "Modified", render: (f) => formatDate(f.modifiedAt) },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (f) => (
        <div className="flex justify-end gap-3">
          {showPreview && isPreviewable(f.name) && (
            <button type="button" onClick={() => handlePreview(f)} className="text-small text-accent hover:text-accent-hover">
              Preview
            </button>
          )}
          {showDownload && (
            <button type="button" onClick={() => handleDownload(f)} className="text-small text-accent hover:text-accent-hover">
              Download
            </button>
          )}
          {showDelete && (
            <button type="button" onClick={() => handleDelete(f)} className="text-small text-state-failed hover:underline">
              Delete
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-2">
      {title && <h3 className="text-h2 text-ink">{title}</h3>}
      <DataTable
        data={files}
        columns={columns}
        keyExtractor={(f) => f.relativePath}
        emptyMessage={emptyMessage}
      />
      {previewing && (
        <Modal
          isOpen
          onClose={() => setPreviewing(null)}
          title={previewing.name}
          size="lg"
        >
          <pre className="max-h-[60vh] overflow-auto whitespace-pre-wrap break-words rounded-md bg-paper p-4 font-mono text-small text-ink">
            {previewing.content}
          </pre>
        </Modal>
      )}
    </div>
  );
}
