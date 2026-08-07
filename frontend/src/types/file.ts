export type FileKind = "tumor_dna" | "normal_dna" | "rna" | "output" | "log" | "summary";

export interface ManagedFile {
  name: string;
  relativePath: string;
  sizeBytes: number;
  createdAt: string;
  modifiedAt: string;
  extension: string;
  fileKind?: FileKind;
  isUserUploaded: boolean;
}

export interface UploadResponse {
  success: boolean;
  uploadedFiles: ManagedFile[];
  error?: string;
}

export interface UploadProgress {
  fileName: string;
  loaded: number;
  total: number;
  percent: number;
  status: "pending" | "uploading" | "complete" | "error";
  error?: string;
}
