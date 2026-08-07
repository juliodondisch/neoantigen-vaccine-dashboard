export interface Patient {
  id: string;
  name: string;
  notes?: string;
  cancerType?: string;
  createdAt: string;
  updatedAt: string;
  referenceGenome?: string;
}

export interface PatientSummary {
  id: string;
  name: string;
  cancerType?: string;
  createdAt: string;
  completedSteps: number;
  totalSteps: number;
  furthestStepId?: string;
  totalDiskBytes: number;
}

export interface CreatePatientRequest {
  name: string;
  notes?: string;
  cancerType?: string;
  referenceGenome?: string;
}

export interface UpdatePatientRequest {
  name?: string;
  notes?: string;
  cancerType?: string;
}
