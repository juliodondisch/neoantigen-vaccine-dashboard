import { formatBytes, formatDate } from "@/lib/utils/format";
import type { PatientSummary } from "@/types/patient";
import { Button } from "@/components/common/Button";

interface PatientCardProps {
  patient: PatientSummary;
  onClick: () => void;
  onDelete?: () => void;
}

export function PatientCard({ patient, onClick, onDelete }: PatientCardProps) {
  const progressPct =
    patient.totalSteps > 0
      ? Math.round((patient.completedSteps / patient.totalSteps) * 100)
      : 0;

  return (
    <div className="flex flex-col gap-4 rounded-md border border-rule bg-surface p-6 transition-colors hover:border-rule-strong">
      <button
        type="button"
        onClick={onClick}
        className="flex flex-col gap-2 text-left"
      >
        <span className="text-h2 text-ink">{patient.name}</span>
        {patient.cancerType && (
          <span className="text-small text-slate">{patient.cancerType}</span>
        )}
      </button>

      <div>
        <div className="mb-1 flex items-center justify-between text-small text-slate">
          <span>
            {patient.completedSteps} / {patient.totalSteps} steps
          </span>
          <span className="font-mono">{progressPct}%</span>
        </div>
        <div className="h-1.5 w-full overflow-hidden rounded-full bg-rule">
          <div
            className="h-full bg-accent"
            style={{ width: `${progressPct}%` }}
          />
        </div>
      </div>

      <div className="flex items-center justify-between text-small text-slate">
        <span>{formatDate(patient.createdAt)}</span>
        <span className="font-mono">{formatBytes(patient.totalDiskBytes)}</span>
      </div>

      {onDelete && (
        <Button variant="ghost" size="sm" onClick={onDelete} className="self-start">
          Delete
        </Button>
      )}
    </div>
  );
}
