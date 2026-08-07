import type { ManagedFile } from "./file";

export type StepId =
  | "01_upload" | "02_alignment" | "03_variants" | "04_protein_effects"
  | "05_hla_typing" | "06_candidates" | "07_presentation"
  | "08_immunogenicity" | "09_filtering" | "10_ranking" | "11_vaccine_design";

export type StepStatus =
  | "NotStarted" | "InputsMissing" | "Ready" | "Running" | "Completed" | "Failed";

export type JobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled";

export interface StepDefinition {
  id: StepId;
  order: number;
  displayName: string;
  shortDescription: string;
  longExplanation: string;
  toolName: string;
  requiredInputStepIds: StepId[];
  isUploadStep: boolean;
  hasParameters: boolean;
  producesDownload: boolean;
  requiredTools: string[];
}

export interface StepState {
  stepId: StepId;
  status: StepStatus;
  lastRunAt?: string;
  lastError?: string;
  outputFileCount: number;
  outputBytes: number;
  activeJobId?: string;
  lastSummary?: Record<string, unknown>;
}

export interface StepResult {
  success: boolean;
  stepId: StepId;
  message?: string;
  errorDetail?: string;
  outputFiles: ManagedFile[];
  summary: Record<string, unknown>;
  duration: string;
  completedAt: string;
}

export interface ValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  missingTools: string[];
}

export interface JobRecord {
  jobId: string;
  patientId: string;
  stepId: StepId;
  status: JobStatus;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  result?: StepResult;
  logTail?: string;
  progressPercent: number;
}

export interface StepStatusResponse {
  state: StepState;
  activeJob?: JobRecord;
  inputFiles: ManagedFile[];
  outputFiles: ManagedFile[];
}

export interface RunStepRequest {
  parameters?: Record<string, unknown>;
  async?: boolean;
}

export interface RunStepResponse {
  jobId?: string;
  completed: boolean;
  result?: StepResult;
}
