"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { TopBar } from "@/components/layout/TopBar";
import { PatientGrid } from "@/components/patients/PatientGrid";
import { CreatePatientModal } from "@/components/patients/CreatePatientModal";
import { usePatientStore } from "@/stores/usePatientStore";

export default function HomePage() {
  const router = useRouter();
  const { patients, isLoading, error, fetchPatients, clearError } = usePatientStore();
  const [isModalOpen, setIsModalOpen] = useState(false);

  useEffect(() => {
    void fetchPatients();
  }, [fetchPatients]);

  return (
    <>
      <TopBar />
      <main className="mx-auto w-full max-w-[1400px] flex-1 px-8 py-8">
        {error && (
          <div className="mb-6 flex items-center justify-between rounded-md border border-l-[3px] border-rule border-l-state-failed bg-feedback-errorBg px-4 py-3 text-ui text-ink">
            <span>{error}</span>
            <button type="button" onClick={clearError} className="text-small text-accent">
              Dismiss
            </button>
          </div>
        )}
        <PatientGrid
          patients={patients}
          isLoading={isLoading}
          onSelectPatient={(patientId) => router.push(`/patients/${patientId}`)}
          onCreateClick={() => setIsModalOpen(true)}
        />
      </main>
      <CreatePatientModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onCreated={(patient) => {
          setIsModalOpen(false);
          router.push(`/patients/${patient.id}`);
        }}
      />
    </>
  );
}
