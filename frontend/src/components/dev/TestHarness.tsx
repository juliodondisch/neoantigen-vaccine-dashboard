"use client";

import { useEffect, useState } from "react";
import { useDevStore } from "@/stores/useDevStore";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import { STEP_IDS, STEP_DISPLAY_NAMES } from "@/lib/constants/steps";
import { Button } from "@/components/common/Button";
import { ToolStatusPanel } from "./ToolStatusPanel";
import { TestResultRow } from "./TestResultRow";
import type { StepId } from "@/types/step";

export function TestHarness() {
  const {
    toolStatuses,
    diskStatus,
    testResults,
    isRunningTests,
    selectedTier,
    fetchToolStatuses,
    refreshTools,
    fetchDiskStatus,
    seedTestPatient,
    runTests,
    cleanupTestPatients,
    setTier,
  } = useDevStore();
  const showSuccess = useToastStore((s) => s.success);
  const showError = useToastStore((s) => s.error);
  const [seedThrough, setSeedThrough] = useState<StepId>("07_presentation");

  useEffect(() => {
    void fetchToolStatuses();
    void fetchDiskStatus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSeed = async () => {
    try {
      const patient = await seedTestPatient(seedThrough);
      showSuccess("Seeded test patient", `${patient.name} (through ${seedThrough})`);
    } catch (err) {
      showError("Seed failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  const handleCleanup = async () => {
    try {
      await cleanupTestPatients();
      showSuccess("Test patients cleaned up");
    } catch (err) {
      showError("Cleanup failed", isApiError(err) ? err.detail ?? err.message : undefined);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-display text-ink">Dev test harness</h1>

      <ToolStatusPanel
        statuses={toolStatuses}
        diskStatus={diskStatus}
        onRefresh={() => void refreshTools()}
      />

      <div className="flex flex-wrap items-end gap-4 rounded-md border border-rule bg-surface p-6">
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Seed patient through step</span>
          <select
            value={seedThrough}
            onChange={(e) => setSeedThrough(e.target.value as StepId)}
            className="rounded-md border border-rule-strong px-3 py-2 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            {STEP_IDS.map((id) => (
              <option key={id} value={id}>
                {STEP_DISPLAY_NAMES[id]}
              </option>
            ))}
          </select>
        </label>
        <Button onClick={handleSeed}>Seed test patient</Button>
        <Button variant="danger" onClick={handleCleanup}>
          Clean up test patients
        </Button>
      </div>

      <div className="flex flex-col gap-4 rounded-md border border-rule bg-surface p-6">
        <div className="flex items-center gap-4">
          <span className="text-ui text-ink">Tier</span>
          {([1, 2] as const).map((tier) => (
            <label key={tier} className="flex items-center gap-1.5 text-ui text-ink">
              <input
                type="radio"
                checked={selectedTier === tier}
                onChange={() => setTier(tier)}
                className="accent-accent"
              />
              Tier {tier}
            </label>
          ))}
          <Button
            className="ml-auto"
            isLoading={isRunningTests}
            onClick={() => void runTests(selectedTier)}
          >
            Run tests
          </Button>
        </div>

        <div>
          {testResults.length === 0 ? (
            <p className="text-body text-slate">No test results yet.</p>
          ) : (
            testResults.map((r, i) => <TestResultRow key={`${r.stepId}-${r.testName}-${i}`} result={r} />)
          )}
        </div>
      </div>
    </div>
  );
}
