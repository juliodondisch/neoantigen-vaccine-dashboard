import { cn } from "@/lib/utils/cn";
import type { StepDefinition, StepId, StepState } from "@/types/step";
import { StepSidebarItem } from "./StepSidebarItem";

interface StepSidebarProps {
  patientId: string;
  definitions: StepDefinition[];
  states: Record<StepId, StepState>;
  selectedStepId: StepId | null;
  onSelectStep: (stepId: StepId) => void;
}

export function StepSidebar({
  definitions,
  states,
  selectedStepId,
  onSelectStep,
}: StepSidebarProps) {
  const ordered = [...definitions].sort((a, b) => a.order - b.order);

  return (
    <nav
      aria-label="Pipeline steps"
      className="flex w-[280px] shrink-0 flex-col border-r border-rule bg-surface py-6"
    >
      <div className="relative">
        {/* The step spine ,  Appendix C.6 secondary structural device. */}
        <div
          aria-hidden
          className="absolute left-[19px] top-3 bottom-3 w-px bg-rule"
        />
        <ul className="flex flex-col gap-1 px-4">
          {ordered.map((definition, i) => {
            const state = states[definition.id];
            const prev = i > 0 ? ordered[i - 1] : null;
            const spineComplete =
              prev !== null && states[prev.id]?.status === "Completed";
            return (
              <div key={definition.id} className="relative">
                {i > 0 && (
                  <div
                    aria-hidden
                    className={cn(
                      "absolute left-[3px] -top-1 h-1 w-px",
                      spineComplete ? "bg-state-complete" : "bg-rule"
                    )}
                  />
                )}
                <StepSidebarItem
                  definition={definition}
                  state={state}
                  isSelected={selectedStepId === definition.id}
                  isDisabled={false}
                  onClick={() => onSelectStep(definition.id)}
                />
              </div>
            );
          })}
        </ul>
      </div>
    </nav>
  );
}
