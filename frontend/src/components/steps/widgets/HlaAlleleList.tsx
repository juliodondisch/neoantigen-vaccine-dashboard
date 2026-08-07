"use client";

import { useState } from "react";
import { formatDate, formatPercent } from "@/lib/utils/format";
import { Spinner } from "@/components/common/Spinner";
import type { HlaProfile } from "@/types/candidate";

interface HlaAlleleListProps {
  profile: HlaProfile | null;
  isLoading?: boolean;
  allowManualOverride?: boolean;
  onOverride?: (alleles: string[]) => void;
}

export function HlaAlleleList({
  profile,
  isLoading = false,
  allowManualOverride = false,
  onOverride,
}: HlaAlleleListProps) {
  const [overrideText, setOverrideText] = useState("");

  if (isLoading) {
    return (
      <div className="flex items-center gap-3 py-6">
        <Spinner size="sm" />
        <span className="text-ui text-slate">Typing HLA alleles…</span>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {profile ? (
        <>
          <div>
            <p className="text-micro text-slate">Class I</p>
            <ul className="mt-2 flex flex-wrap gap-2">
              {profile.classIAlleles.map((allele) => (
                <li
                  key={allele}
                  className="flex items-center gap-2 rounded-sm border border-rule bg-paper px-2.5 py-1.5 font-mono text-ui text-ink"
                >
                  {allele}
                  {profile.confidence[allele] !== undefined && (
                    <span className="text-small text-slate">
                      {formatPercent(profile.confidence[allele])}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </div>
          {profile.classIIAlleles.length > 0 && (
            <div>
              <p className="text-micro text-slate">Class II</p>
              <ul className="mt-2 flex flex-wrap gap-2">
                {profile.classIIAlleles.map((allele) => (
                  <li
                    key={allele}
                    className="rounded-sm border border-rule bg-paper px-2.5 py-1.5 font-mono text-ui text-ink"
                  >
                    {allele}
                  </li>
                ))}
              </ul>
            </div>
          )}
          <p className="text-small text-slate">
            {profile.source} &middot; typed {formatDate(profile.typedAt)}
          </p>
        </>
      ) : (
        <p className="text-body text-slate">No HLA type on file yet.</p>
      )}

      {allowManualOverride && onOverride && (
        <div className="flex gap-2 border-t border-rule pt-4">
          <input
            value={overrideText}
            onChange={(e) => setOverrideText(e.target.value)}
            placeholder="HLA-A*02:01, HLA-B*07:02, ..."
            className="flex-1 rounded-md border border-rule-strong px-3 py-2 font-mono text-small text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
          />
          <button
            type="button"
            onClick={() => {
              const alleles = overrideText
                .split(",")
                .map((a) => a.trim())
                .filter(Boolean);
              if (alleles.length) {
                onOverride(alleles);
                setOverrideText("");
              }
            }}
            className="rounded-md border border-rule-strong px-3 py-2 text-small text-ink hover:bg-paper"
          >
            Override
          </button>
        </div>
      )}
    </div>
  );
}
