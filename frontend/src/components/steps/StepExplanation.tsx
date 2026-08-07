"use client";

import { useState } from "react";
import type { StepDefinition } from "@/types/step";

interface StepExplanationProps {
  definition: StepDefinition;
  collapsible?: boolean;
  defaultExpanded?: boolean;
}

export function StepExplanation({
  definition,
  collapsible = true,
  defaultExpanded = true,
}: StepExplanationProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="rounded-md border border-rule bg-surface p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-micro text-slate">
            Step {definition.order} &middot; {definition.toolName}
          </p>
          <h2 className="mt-1 text-h1 text-ink">{definition.displayName}</h2>
        </div>
        {collapsible && (
          <button
            type="button"
            onClick={() => setExpanded((e) => !e)}
            className="shrink-0 text-small text-accent hover:text-accent-hover"
          >
            {expanded ? "Collapse" : "Expand"}
          </button>
        )}
      </div>
      {expanded && (
        <p className="mt-4 max-w-3xl text-body text-ink">
          {definition.longExplanation}
        </p>
      )}
    </div>
  );
}
