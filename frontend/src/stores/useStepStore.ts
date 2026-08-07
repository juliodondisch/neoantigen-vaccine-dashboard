import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import {
  getAllStepStates,
  getJob,
  getStepStatus,
  listStepDefinitions,
  runStep as apiRunStep,
  cancelJob as apiCancelJob,
  validateStep as apiValidateStep,
} from "@/lib/api/steps";
import { listFiles } from "@/lib/api/files";
import { pollUntil } from "@/lib/utils/polling";
import { STEP_DEFINITIONS } from "@/lib/constants/steps";
import { POLL_INTERVAL_MS } from "@/lib/constants/config";
import type {
  JobRecord,
  StepDefinition,
  StepId,
  StepState,
  ValidationResult,
} from "@/types/step";
import type { ManagedFile } from "@/types/file";

interface StepStore {
  definitions: StepDefinition[];
  states: Record<StepId, StepState>;
  selectedStepId: StepId | null;
  activeJobs: Record<StepId, JobRecord>;
  inputFiles: Record<StepId, ManagedFile[]>;
  outputFiles: Record<StepId, ManagedFile[]>;
  validations: Record<StepId, ValidationResult>;
  isLoadingStates: boolean;

  fetchDefinitions: () => Promise<void>;
  fetchAllStates: (patientId: string) => Promise<void>;
  fetchStepStatus: (patientId: string, stepId: StepId) => Promise<void>;
  validateStep: (patientId: string, stepId: StepId) => Promise<ValidationResult>;
  runStep: (
    patientId: string,
    stepId: StepId,
    parameters?: Record<string, unknown>
  ) => Promise<string | null>;
  pollJob: (patientId: string, stepId: StepId, jobId: string) => Promise<void>;
  cancelJob: (patientId: string, stepId: StepId, jobId: string) => Promise<void>;
  selectStep: (stepId: StepId) => void;
  refreshFiles: (patientId: string, stepId: StepId) => Promise<void>;
  reset: () => void;

  getStepState: (stepId: StepId) => StepState | undefined;
  isStepRunning: (stepId: StepId) => boolean;
  canRunStep: (stepId: StepId) => boolean;
}

const emptyRecord = <T>(): Record<StepId, T> => ({} as Record<StepId, T>);

const initialState = {
  definitions: [] as StepDefinition[],
  states: emptyRecord<StepState>(),
  selectedStepId: null as StepId | null,
  activeJobs: emptyRecord<JobRecord>(),
  inputFiles: emptyRecord<ManagedFile[]>(),
  outputFiles: emptyRecord<ManagedFile[]>(),
  validations: emptyRecord<ValidationResult>(),
  isLoadingStates: false,
};

// Persisted to sessionStorage: selectedStepId only, so a refresh keeps you
// on the same step. Everything else is re-derived from the API.
export const useStepStore = create<StepStore>()(
  persist(
    (set, get) => ({
      ...initialState,

      fetchDefinitions: async () => {
        try {
          const definitions = await listStepDefinitions();
          set({ definitions });
        } catch {
          // Backend unreachable ,  fall back to the local, offline copy so
          // panels still render real step content during frontend dev.
          set({ definitions: STEP_DEFINITIONS });
        }
      },

      fetchAllStates: async (patientId: string) => {
        set({ isLoadingStates: true });
        try {
          const states = await getAllStepStates(patientId);
          const byId = emptyRecord<StepState>();
          for (const s of states) byId[s.stepId] = s;
          set({ states: byId, isLoadingStates: false });
        } catch {
          set({ isLoadingStates: false });
        }
      },

      fetchStepStatus: async (patientId: string, stepId: StepId) => {
        try {
          const status = await getStepStatus(patientId, stepId);
          set((state) => ({
            states: { ...state.states, [stepId]: status.state },
            inputFiles: { ...state.inputFiles, [stepId]: status.inputFiles },
            outputFiles: { ...state.outputFiles, [stepId]: status.outputFiles },
            activeJobs: status.activeJob
              ? { ...state.activeJobs, [stepId]: status.activeJob }
              : state.activeJobs,
          }));
        } catch {
          // Leave prior state in place ,  surfaced via toast by the caller.
        }
      },

      validateStep: async (patientId: string, stepId: StepId) => {
        const result = await apiValidateStep(patientId, stepId);
        set((state) => ({ validations: { ...state.validations, [stepId]: result } }));
        return result;
      },

      runStep: async (patientId: string, stepId: StepId, parameters) => {
        const response = await apiRunStep(patientId, stepId, parameters, true);
        if (response.jobId) {
          await get().fetchStepStatus(patientId, stepId);
          void get().pollJob(patientId, stepId, response.jobId);
        }
        return response.jobId ?? null;
      },

      pollJob: async (patientId: string, stepId: StepId, jobId: string) => {
        try {
          await pollUntil(
            () => getJob(patientId, stepId, jobId),
            (job) =>
              job.status === "Succeeded" ||
              job.status === "Failed" ||
              job.status === "Cancelled",
            {
              intervalMs: POLL_INTERVAL_MS,
              onTick: () => {
                // no-op; UI reads activeJobs from the store after each fetch below
              },
            }
          ).then((job) => {
            set((state) => ({ activeJobs: { ...state.activeJobs, [stepId]: job } }));
          });
        } finally {
          await get().fetchStepStatus(patientId, stepId);
        }
      },

      cancelJob: async (patientId: string, stepId: StepId, jobId: string) => {
        await apiCancelJob(patientId, stepId, jobId);
        await get().fetchStepStatus(patientId, stepId);
      },

      selectStep: (stepId: StepId) => set({ selectedStepId: stepId }),

      refreshFiles: async (patientId: string, stepId: StepId) => {
        try {
          const files = await listFiles(patientId, stepId);
          const outputs = files.filter((f) => !f.isUserUploaded);
          const inputs = files.filter((f) => f.isUserUploaded);
          set((state) => ({
            inputFiles: { ...state.inputFiles, [stepId]: inputs },
            outputFiles: { ...state.outputFiles, [stepId]: outputs },
          }));
        } catch {
          // surfaced via toast by the caller
        }
      },

      reset: () => set({ ...initialState, selectedStepId: get().selectedStepId }),

      getStepState: (stepId: StepId) => get().states[stepId],

      isStepRunning: (stepId: StepId) => get().states[stepId]?.status === "Running",

      canRunStep: (stepId: StepId) => {
        const status = get().states[stepId]?.status;
        return status === "Ready" || status === "Completed" || status === "Failed";
      },
    }),
    {
      name: "neoantigen-step-store",
      storage: createJSONStorage(() =>
        typeof window !== "undefined" ? window.sessionStorage : (undefined as never)
      ),
      partialize: (state) => ({ selectedStepId: state.selectedStepId }),
      skipHydration: typeof window === "undefined",
    }
  )
);
