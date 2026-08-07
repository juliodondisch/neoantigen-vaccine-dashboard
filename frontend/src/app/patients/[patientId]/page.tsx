"use client";

import { use, useEffect } from "react";
import { TopBar } from "@/components/layout/TopBar";
import { StepSidebar } from "@/components/layout/StepSidebar";
import { StepPanel } from "@/components/steps/StepPanel";
import { Spinner } from "@/components/common/Spinner";
import { usePatientStore } from "@/stores/usePatientStore";
import { useStepStore } from "@/stores/useStepStore";
import { STEP_IDS } from "@/lib/constants/steps";

// NOTE (deviation from spec §13): Next.js 16's App Router resolves `params`
// as a Promise (see node_modules/next/dist/docs/01-app/.../page.md), not the
// synchronous `{ patientId: string }` shown in docs/TECHNICAL_SPEC.md §13,
// which predates that change. Unwrapped here with React's `use()` since this
// is a Client Component. Logged in docs/deviations.md.
interface PageProps {
  params: Promise<{ patientId: string }>;
}

export default function PatientDashboardPage({ params }: PageProps) {
  const { patientId } = use(params);

  const { currentPatient, fetchPatient } = usePatientStore();
  const {
    definitions,
    states,
    selectedStepId,
    fetchDefinitions,
    fetchAllStates,
    selectStep,
  } = useStepStore();

  useEffect(() => {
    void fetchPatient(patientId);
    void fetchDefinitions();
    void fetchAllStates(patientId);
  }, [patientId, fetchPatient, fetchDefinitions, fetchAllStates]);

  useEffect(() => {
    if (!selectedStepId && definitions.length > 0) {
      selectStep(definitions[0].id);
    }
  }, [selectedStepId, definitions, selectStep]);

  const activeStepId = selectedStepId ?? STEP_IDS[0];
  const activeDefinition = definitions.find((d) => d.id === activeStepId);

  return (
    <>
      <TopBar patientName={currentPatient?.name} showBackLink />
      <div className="flex flex-1">
        <StepSidebar
          patientId={patientId}
          definitions={definitions}
          states={states}
          selectedStepId={selectedStepId}
          onSelectStep={selectStep}
        />
        <main className="flex-1 overflow-auto px-8 py-8">
          {activeDefinition ? (
            <StepPanel
              patientId={patientId}
              stepId={activeDefinition.id}
              definition={activeDefinition}
              state={states[activeDefinition.id]}
            />
          ) : (
            <div className="flex items-center justify-center py-24">
              <Spinner size="lg" />
            </div>
          )}
        </main>
      </div>
    </>
  );
}
