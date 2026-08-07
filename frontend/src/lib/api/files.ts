import { apiClient } from "./client";
import { API_BASE_URL } from "@/lib/constants/config";
import type { FileKind, ManagedFile, UploadResponse } from "@/types/file";
import type { StepId } from "@/types/step";

export async function listFiles(
  patientId: string,
  stepId: StepId
): Promise<ManagedFile[]> {
  return apiClient.get<ManagedFile[]>(
    `/api/patients/${patientId}/steps/${stepId}/files`
  );
}

export async function uploadFiles(
  patientId: string,
  stepId: StepId,
  files: File[],
  fileKind?: FileKind,
  onProgress?: (p: number) => void
): Promise<UploadResponse> {
  const formData = new FormData();
  for (const file of files) formData.append("files", file);
  if (fileKind) formData.append("fileKind", fileKind);
  return apiClient.postFormData<UploadResponse>(
    `/api/patients/${patientId}/steps/${stepId}/files/upload`,
    formData,
    onProgress
  );
}

export async function registerFilePath(
  patientId: string,
  stepId: StepId,
  sourcePath: string,
  fileKind?: FileKind,
  copy?: boolean
): Promise<UploadResponse> {
  return apiClient.post<UploadResponse>(
    `/api/patients/${patientId}/steps/${stepId}/files/register`,
    { sourcePath, fileKind, copy }
  );
}

export function getDownloadUrl(
  patientId: string,
  stepId: StepId,
  fileName: string
): string {
  return `${API_BASE_URL}/api/patients/${patientId}/steps/${stepId}/files/${encodeURIComponent(fileName)}/download`;
}

export async function downloadFile(
  patientId: string,
  stepId: StepId,
  fileName: string
): Promise<void> {
  const blob = await apiClient.getBlob(
    `/api/patients/${patientId}/steps/${stepId}/files/${encodeURIComponent(fileName)}/download`
  );
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export async function previewFile(
  patientId: string,
  stepId: StepId,
  fileName: string,
  maxLines?: number
): Promise<string> {
  return apiClient.get<string>(
    `/api/patients/${patientId}/steps/${stepId}/files/${encodeURIComponent(fileName)}/preview`,
    maxLines !== undefined ? { maxLines } : undefined
  );
}

export async function deleteFile(
  patientId: string,
  stepId: StepId,
  fileName: string
): Promise<void> {
  return apiClient.delete<void>(
    `/api/patients/${patientId}/steps/${stepId}/files/${encodeURIComponent(fileName)}`
  );
}
