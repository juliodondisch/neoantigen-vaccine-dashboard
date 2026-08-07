import { cn } from "@/lib/utils/cn";
import type { TestRunResult } from "@/types/api";

interface TestResultRowProps {
  result: TestRunResult;
}

const OUTCOME_STYLE: Record<TestRunResult["outcome"], string> = {
  Passed: "text-state-complete",
  Failed: "text-state-failed",
  Skipped: "text-state-skipped",
};

export function TestResultRow({ result }: TestResultRowProps) {
  return (
    <div className="flex flex-col gap-1 border-b border-rule py-3 last:border-b-0">
      <div className="flex items-center justify-between gap-3">
        <span className="font-mono text-ui text-ink">
          {result.stepId} &middot; {result.testName}
        </span>
        <span className={cn("text-small font-medium", OUTCOME_STYLE[result.outcome])}>
          {result.outcome}
        </span>
      </div>
      <div className="flex items-center justify-between text-small text-slate">
        <span>{result.message ?? result.skipReason ?? ""}</span>
        <span className="font-mono">{result.durationSeconds.toFixed(2)}s</span>
      </div>
      {result.assertions.length > 0 && (
        <ul className="ml-4 list-disc text-small text-slate">
          {result.assertions.map((a, i) => (
            <li key={i}>{a}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
