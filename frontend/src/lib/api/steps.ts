import { apiClient } from "./client";
import type {
  JobRecord,
  RunStepResponse,
  StepDefinition,
  StepId,
  StepState,
  StepStatusResponse,
  ValidationResult,
} from "@/types/step";
import type { NeoantigenCandidate, RankingWeights } from "@/types/candidate";

export async function listStepDefinitions(): Promise<StepDefinition[]> {
  // Step definitions are patient-independent (state lives separately in
  // /steps/states); backend exposes this both at /api/steps and under the
  // patient-scoped route in spec §14 ,  confirmed against StepsController.
  return apiClient.get<StepDefinition[]>("/api/steps");
}

export async function getAllStepStates(patientId: string): Promise<StepState[]> {
  return apiClient.get<StepState[]>(`/api/patients/${patientId}/steps/states`);
}

export async function getStepStatus(
  patientId: string,
  stepId: StepId
): Promise<StepStatusResponse> {
  return apiClient.get<StepStatusResponse>(
    `/api/patients/${patientId}/steps/${stepId}`
  );
}

export async function validateStep(
  patientId: string,
  stepId: StepId
): Promise<ValidationResult> {
  return apiClient.get<ValidationResult>(
    `/api/patients/${patientId}/steps/${stepId}/validate`
  );
}

export async function runStep(
  patientId: string,
  stepId: StepId,
  parameters?: Record<string, unknown>,
  async = true
): Promise<RunStepResponse> {
  return apiClient.post<RunStepResponse>(
    `/api/patients/${patientId}/steps/${stepId}/run`,
    { parameters, async }
  );
}

export async function getJob(
  patientId: string,
  stepId: StepId,
  jobId: string
): Promise<JobRecord> {
  return apiClient.get<JobRecord>(
    `/api/patients/${patientId}/steps/${stepId}/jobs/${jobId}`
  );
}

export async function cancelJob(
  patientId: string,
  stepId: StepId,
  jobId: string
): Promise<void> {
  return apiClient.post<void>(
    `/api/patients/${patientId}/steps/${stepId}/jobs/${jobId}/cancel`
  );
}

export async function getStepSummary(
  patientId: string,
  stepId: StepId
): Promise<Record<string, unknown>> {
  return apiClient.get<Record<string, unknown>>(
    `/api/patients/${patientId}/steps/${stepId}/summary`
  );
}

export async function previewRanking(
  patientId: string,
  weights: RankingWeights,
  targetCount: number
): Promise<NeoantigenCandidate[]> {
  return apiClient.post<NeoantigenCandidate[]>(
    `/api/patients/${patientId}/steps/10_ranking/preview`,
    { weights, targetCount }
  );
}
