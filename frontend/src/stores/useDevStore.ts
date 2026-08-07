import { create } from "zustand";
import {
  cleanupTestPatients as apiCleanupTestPatients,
  getDiskStatus,
  listTools,
  refreshTools as apiRefreshTools,
  runTests as apiRunTests,
  seedTestPatient as apiSeedTestPatient,
} from "@/lib/api/dev";
import type { DiskStatus, TestRunResult, ToolStatus } from "@/types/api";
import type { Patient } from "@/types/patient";
import type { StepId } from "@/types/step";

interface DevStore {
  toolStatuses: ToolStatus[];
  diskStatus: DiskStatus | null;
  testResults: TestRunResult[];
  isRunningTests: boolean;
  selectedTier: 1 | 2;

  fetchToolStatuses: () => Promise<void>;
  refreshTools: () => Promise<void>;
  fetchDiskStatus: () => Promise<void>;
  seedTestPatient: (seedThroughStepId: StepId) => Promise<Patient>;
  runTests: (tier: 1 | 2, stepIds?: StepId[]) => Promise<void>;
  cleanupTestPatients: () => Promise<void>;
  setTier: (tier: 1 | 2) => void;
}

// Not persisted.
export const useDevStore = create<DevStore>()((set) => ({
  toolStatuses: [],
  diskStatus: null,
  testResults: [],
  isRunningTests: false,
  selectedTier: 1,

  fetchToolStatuses: async () => {
    try {
      const toolStatuses = await listTools();
      set({ toolStatuses });
    } catch {
      // dev page shows an unreachable-backend state instead
    }
  },

  refreshTools: async () => {
    try {
      const toolStatuses = await apiRefreshTools();
      set({ toolStatuses });
    } catch {
      // ignore — caller's toast surfaces the failure
    }
  },

  fetchDiskStatus: async () => {
    try {
      const diskStatus = await getDiskStatus();
      set({ diskStatus });
    } catch {
      // ignore
    }
  },

  seedTestPatient: async (seedThroughStepId: StepId) => {
    return apiSeedTestPatient(seedThroughStepId);
  },

  runTests: async (tier: 1 | 2, stepIds?: StepId[]) => {
    set({ isRunningTests: true });
    try {
      const testResults = await apiRunTests(tier, stepIds);
      set({ testResults, isRunningTests: false });
    } catch {
      set({ isRunningTests: false });
    }
  },

  cleanupTestPatients: async () => {
    await apiCleanupTestPatients();
  },

  setTier: (tier: 1 | 2) => set({ selectedTier: tier }),
}));
