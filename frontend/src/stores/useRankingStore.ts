import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { previewRanking } from "@/lib/api/steps";
import type { NeoantigenCandidate, RankingWeights } from "@/types/candidate";

const DEFAULT_WEIGHTS: RankingWeights = {
  presentation: 0.2,
  immunogenicity: 0.2,
  agretopicity: 0.2,
  expression: 0.15,
  clonality: 0.15,
  hlaSpread: 0.1,
};

const DEFAULT_TARGET_COUNT = 20;

interface PerPatientRankingConfig {
  weights: RankingWeights;
  targetCount: number;
}

interface RankingStore {
  weights: RankingWeights;
  targetCount: number;
  previewCandidates: NeoantigenCandidate[];
  isPreviewLoading: boolean;
  hasUnsavedChanges: boolean;
  lastCommittedWeights: RankingWeights | null;

  setWeight: (key: keyof RankingWeights, value: number) => void;
  setWeights: (weights: RankingWeights) => void;
  setTargetCount: (count: number) => void;
  fetchPreview: (patientId: string) => Promise<void>;
  commitRanking: (patientId: string) => Promise<void>;
  resetWeights: () => void;
  loadCommittedWeights: (patientId: string) => Promise<void>;

  // Internal — not part of the spec's public interface, needed to key
  // persistence by patient ID from a single Zustand store instance.
  _perPatient: Record<string, PerPatientRankingConfig>;
  _activePatientId: string | null;
}

let debounceTimer: ReturnType<typeof setTimeout> | null = null;

// Persisted to localStorage, keyed by patient ID: weights + targetCount.
// fetchPreview is debounced 300ms on slider change.
export const useRankingStore = create<RankingStore>()(
  persist(
    (set, get) => ({
      weights: DEFAULT_WEIGHTS,
      targetCount: DEFAULT_TARGET_COUNT,
      previewCandidates: [],
      isPreviewLoading: false,
      hasUnsavedChanges: false,
      lastCommittedWeights: null,
      _perPatient: {},
      _activePatientId: null,

      setWeight: (key, value) => {
        const weights = { ...get().weights, [key]: value };
        set((state) => ({
          weights,
          hasUnsavedChanges: true,
          _perPatient: state._activePatientId
            ? {
                ...state._perPatient,
                [state._activePatientId]: {
                  weights,
                  targetCount: state.targetCount,
                },
              }
            : state._perPatient,
        }));
      },

      setWeights: (weights) => {
        set((state) => ({
          weights,
          hasUnsavedChanges: true,
          _perPatient: state._activePatientId
            ? {
                ...state._perPatient,
                [state._activePatientId]: { weights, targetCount: state.targetCount },
              }
            : state._perPatient,
        }));
      },

      setTargetCount: (count) => {
        set((state) => ({
          targetCount: count,
          hasUnsavedChanges: true,
          _perPatient: state._activePatientId
            ? {
                ...state._perPatient,
                [state._activePatientId]: { weights: state.weights, targetCount: count },
              }
            : state._perPatient,
        }));
      },

      fetchPreview: async (patientId: string) => {
        if (debounceTimer) clearTimeout(debounceTimer);
        return new Promise<void>((resolve) => {
          debounceTimer = setTimeout(async () => {
            set({ isPreviewLoading: true });
            try {
              const { weights, targetCount } = get();
              const candidates = await previewRanking(patientId, weights, targetCount);
              set({ previewCandidates: candidates, isPreviewLoading: false });
            } catch {
              set({ isPreviewLoading: false });
            } finally {
              resolve();
            }
          }, 300);
        });
      },

      commitRanking: async (patientId: string) => {
        const { weights } = get();
        const { runStep } = await import("@/lib/api/steps");
        const { targetCount } = get();
        await runStep(patientId, "10_ranking", { weights, targetCount }, false);
        set({ lastCommittedWeights: weights, hasUnsavedChanges: false });
      },

      resetWeights: () => {
        set((state) => ({
          weights: DEFAULT_WEIGHTS,
          targetCount: DEFAULT_TARGET_COUNT,
          hasUnsavedChanges: false,
          _perPatient: state._activePatientId
            ? {
                ...state._perPatient,
                [state._activePatientId]: {
                  weights: DEFAULT_WEIGHTS,
                  targetCount: DEFAULT_TARGET_COUNT,
                },
              }
            : state._perPatient,
        }));
      },

      loadCommittedWeights: async (patientId: string) => {
        const saved = get()._perPatient[patientId];
        set({
          _activePatientId: patientId,
          weights: saved?.weights ?? DEFAULT_WEIGHTS,
          targetCount: saved?.targetCount ?? DEFAULT_TARGET_COUNT,
          hasUnsavedChanges: false,
        });
        try {
          const { getStepSummary } = await import("@/lib/api/steps");
          const summary = await getStepSummary(patientId, "10_ranking");
          const committed = summary?.weights as RankingWeights | undefined;
          if (committed) {
            set({ lastCommittedWeights: committed });
          }
        } catch {
          // no committed ranking yet, or backend unreachable — fine, keep local/defaults
        }
      },
    }),
    {
      name: "neoantigen-ranking-store",
      storage: createJSONStorage(() =>
        typeof window !== "undefined" ? window.localStorage : (undefined as never)
      ),
      partialize: (state) => ({ _perPatient: state._perPatient }),
      skipHydration: typeof window === "undefined",
    }
  )
);
