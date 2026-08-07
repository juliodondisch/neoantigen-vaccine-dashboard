import { cn } from "@/lib/utils/cn";
import type { StepDefinition, StepState } from "@/types/step";

interface StepSidebarItemProps {
  definition: StepDefinition;
  state?: StepState;
  isSelected: boolean;
  isDisabled: boolean;
  onClick: () => void;
}

const STATE_DOT: Record<StepState["status"], string> = {
  NotStarted: "bg-state-idle",
  InputsMissing: "bg-state-blocked",
  Ready: "bg-state-ready",
  Running: "bg-state-ready motion-safe:animate-pulse",
  Completed: "bg-state-complete",
  Failed: "bg-state-failed",
};

export function StepSidebarItem({
  definition,
  state,
  isSelected,
  isDisabled,
  onClick,
}: StepSidebarItemProps) {
  const status = state?.status ?? "NotStarted";
  return (
    <li className="relative pl-2">
      <button
        type="button"
        onClick={onClick}
        disabled={isDisabled}
        aria-current={isSelected ? "step" : undefined}
        className={cn(
          "flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-left text-ui transition-colors",
          "disabled:cursor-not-allowed disabled:opacity-40",
          isSelected
            ? "border-l-2 border-accent bg-accent-muted text-ink"
            : "border-l-2 border-transparent text-slate hover:bg-paper hover:text-ink"
        )}
      >
        <span
          aria-hidden
          className={cn(
            "h-2 w-2 shrink-0 rounded-full",
            STATE_DOT[status] ?? "bg-state-idle"
          )}
        />
        <span className="flex-1 truncate">
          <span className="mr-2 font-mono text-small text-slate">{definition.order}</span>
          {definition.displayName}
        </span>
      </button>
    </li>
  );
}
