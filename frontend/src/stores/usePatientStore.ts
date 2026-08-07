import { create } from "zustand";
import {
  createPatient as apiCreatePatient,
  deletePatient as apiDeletePatient,
  getPatient,
  listPatients,
  updatePatient as apiUpdatePatient,
} from "@/lib/api/patients";
import { isApiError } from "@/lib/api/client";
import type {
  CreatePatientRequest,
  Patient,
  PatientSummary,
  UpdatePatientRequest,
} from "@/types/patient";

interface PatientStore {
  patients: PatientSummary[];
  currentPatient: Patient | null;
  isLoading: boolean;
  error: string | null;

  fetchPatients: () => Promise<void>;
  fetchPatient: (patientId: string) => Promise<void>;
  createPatient: (request: CreatePatientRequest) => Promise<Patient>;
  updatePatient: (patientId: string, request: UpdatePatientRequest) => Promise<void>;
  deletePatient: (patientId: string, deleteFiles: boolean) => Promise<void>;
  setCurrentPatient: (patient: Patient | null) => void;
  clearError: () => void;
}

function messageOf(err: unknown): string {
  if (isApiError(err)) return err.detail ? `${err.message}: ${err.detail}` : err.message;
  if (err instanceof Error) return err.message;
  return "Unexpected error";
}

// Not persisted — always fetched fresh from the disk-backed API.
export const usePatientStore = create<PatientStore>()((set, get) => ({
  patients: [],
  currentPatient: null,
  isLoading: false,
  error: null,

  fetchPatients: async () => {
    set({ isLoading: true, error: null });
    try {
      const patients = await listPatients();
      set({ patients, isLoading: false });
    } catch (err) {
      set({ isLoading: false, error: messageOf(err) });
    }
  },

  fetchPatient: async (patientId: string) => {
    set({ isLoading: true, error: null });
    try {
      const patient = await getPatient(patientId);
      set({ currentPatient: patient, isLoading: false });
    } catch (err) {
      set({ isLoading: false, error: messageOf(err) });
    }
  },

  createPatient: async (request: CreatePatientRequest) => {
    const patient = await apiCreatePatient(request);
    set({ patients: [...get().patients] });
    return patient;
  },

  updatePatient: async (patientId: string, request: UpdatePatientRequest) => {
    const updated = await apiUpdatePatient(patientId, request);
    set((state) => ({
      currentPatient:
        state.currentPatient?.id === patientId ? updated : state.currentPatient,
    }));
  },

  deletePatient: async (patientId: string, deleteFiles: boolean) => {
    await apiDeletePatient(patientId, deleteFiles);
    set((state) => ({
      patients: state.patients.filter((p) => p.id !== patientId),
    }));
  },

  setCurrentPatient: (patient: Patient | null) => set({ currentPatient: patient }),

  clearError: () => set({ error: null }),
}));
