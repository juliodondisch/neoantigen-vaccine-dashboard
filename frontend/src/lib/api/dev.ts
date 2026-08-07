import { apiClient } from "./client";
import type { DiskStatus, TestRunResult, ToolStatus } from "@/types/api";
import type { Patient } from "@/types/patient";
import type { StepId } from "@/types/step";

export async function listTools(): Promise<ToolStatus[]> {
  return apiClient.get<ToolStatus[]>("/api/tools");
}

export async function refreshTools(): Promise<ToolStatus[]> {
  return apiClient.post<ToolStatus[]>("/api/tools/refresh");
}

export async function getDiskStatus(): Promise<DiskStatus> {
  return apiClient.get<DiskStatus>("/api/tools/disk");
}

export async function seedTestPatient(
  seedThroughStepId: StepId,
  useTinyFixtures?: boolean
): Promise<Patient> {
  return apiClient.post<Patient>("/api/dev/tests/seed", {
    seedThroughStepId,
    useTinyFixtures,
  });
}

export async function runTests(
  tier: 1 | 2,
  stepIds?: StepId[],
  patientId?: string
): Promise<TestRunResult[]> {
  return apiClient.post<TestRunResult[]>("/api/dev/tests/run", {
    tier,
    stepIds,
    patientId,
  });
}

export async function cleanupTestPatients(): Promise<void> {
  return apiClient.delete<void>("/api/dev/tests/cleanup");
}
