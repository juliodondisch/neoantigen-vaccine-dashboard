import { cn } from "@/lib/utils/cn";
import type { JobStatus, StepStatus } from "@/types/step";

interface StatusBadgeProps {
  status: StepStatus | JobStatus;
  size?: "sm" | "md";
}

const STATUS_META: Record<string, { label: string; dot: string; text: string }> = {
  NotStarted: { label: "Not started", dot: "bg-state-idle", text: "text-state-idle" },
  InputsMissing: { label: "Inputs missing", dot: "bg-state-blocked", text: "text-state-blocked" },
  Ready: { label: "Ready", dot: "bg-state-ready", text: "text-state-ready" },
  Running: { label: "Running", dot: "bg-state-ready motion-safe:animate-pulse", text: "text-state-ready" },
  Completed: { label: "Completed", dot: "bg-state-complete", text: "text-state-complete" },
  Failed: { label: "Failed", dot: "bg-state-failed", text: "text-state-failed" },
  Queued: { label: "Queued", dot: "bg-state-idle", text: "text-state-idle" },
  Succeeded: { label: "Succeeded", dot: "bg-state-complete", text: "text-state-complete" },
  Cancelled: { label: "Cancelled", dot: "bg-state-skipped", text: "text-state-skipped" },
};

export function StatusBadge({ status, size = "md" }: StatusBadgeProps) {
  const meta = STATUS_META[status] ?? {
    label: status,
    dot: "bg-state-idle",
    text: "text-state-idle",
  };
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 font-sans",
        size === "sm" ? "text-small" : "text-ui",
        meta.text
      )}
    >
      <span
        aria-hidden
        className={cn("inline-block h-2 w-2 rounded-full", meta.dot)}
      />
      {meta.label}
    </span>
  );
}
