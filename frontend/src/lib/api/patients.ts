import { apiClient } from "./client";
import type {
  CreatePatientRequest,
  Patient,
  PatientSummary,
  UpdatePatientRequest,
} from "@/types/patient";

export async function listPatients(): Promise<PatientSummary[]> {
  return apiClient.get<PatientSummary[]>("/api/patients");
}

export async function getPatient(patientId: string): Promise<Patient> {
  return apiClient.get<Patient>(`/api/patients/${patientId}`);
}

export async function createPatient(request: CreatePatientRequest): Promise<Patient> {
  return apiClient.post<Patient>("/api/patients", request);
}

export async function updatePatient(
  patientId: string,
  request: UpdatePatientRequest
): Promise<Patient> {
  return apiClient.patch<Patient>(`/api/patients/${patientId}`, request);
}

export async function deletePatient(
  patientId: string,
  deleteFiles?: boolean
): Promise<void> {
  return apiClient.delete<void>(`/api/patients/${patientId}`, {
    deleteFiles: String(deleteFiles ?? false),
  });
}

export async function getPatientSummary(patientId: string): Promise<PatientSummary> {
  return apiClient.get<PatientSummary>(`/api/patients/${patientId}/summary`);
}
