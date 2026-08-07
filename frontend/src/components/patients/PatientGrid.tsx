import type { PatientSummary } from "@/types/patient";
import { Button } from "@/components/common/Button";
import { Spinner } from "@/components/common/Spinner";
import { PatientCard } from "./PatientCard";

interface PatientGridProps {
  patients: PatientSummary[];
  isLoading: boolean;
  onSelectPatient: (patientId: string) => void;
  onCreateClick: () => void;
}

export function PatientGrid({
  patients,
  isLoading,
  onSelectPatient,
  onCreateClick,
}: PatientGridProps) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-display text-ink">Patients</h1>
        <Button onClick={onCreateClick}>New patient</Button>
      </div>

      {patients.length === 0 ? (
        <div className="rounded-md border border-dashed border-rule-strong px-8 py-16 text-center">
          <p className="text-body text-slate">
            No patients yet. Create one to begin the pipeline.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {patients.map((patient) => (
            <PatientCard
              key={patient.id}
              patient={patient}
              onClick={() => onSelectPatient(patient.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
