"use client";

import { useEffect, useMemo } from "react";
import { StepExplanation } from "../StepExplanation";
import { CandidateTable } from "../widgets/CandidateTable";
import { WeightSlider } from "../widgets/WeightSlider";
import { Button } from "@/components/common/Button";
import { useRankingStore } from "@/stores/useRankingStore";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import { listFiles } from "@/lib/api/files";
import { useState } from "react";
import type { RankingWeights } from "@/types/candidate";
import type { StepDefinition, StepState } from "@/types/step";

interface PanelProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

const WEIGHT_FIELDS: { key: keyof RankingWeights; label: string; description: string }[] = [
  { key: "presentation", label: "Presentation", description: "How strongly the mutant peptide is predicted to be displayed on HLA." },
  { key: "immunogenicity", label: "Immunogenicity", description: "How likely the displayed peptide is to actually provoke a T-cell response." },
  { key: "agretopicity", label: "Agretopicity", description: "How much more strongly the mutant binds versus its wild-type counterpart." },
  { key: "expression", label: "Expression", description: "How actively the source gene is transcribed, from RNA-seq." },
  { key: "clonality", label: "Clonality", description: "What fraction of tumor cells carry the mutation, from VAF." },
  { key: "hlaSpread", label: "HLA spread", description: "Diversity bonus for covering more of the patient's HLA alleles." },
];

export function RankingPanel({ patientId, definition, state }: PanelProps) {
  const {
    weights,
    targetCount,
    previewCandidates,
    isPreviewLoading,
    hasUnsavedChanges,
    setWeight,
    setTargetCount,
    fetchPreview,
    commitRanking,
    resetWeights,
    loadCommittedWeights,
  } = useRankingStore();
  const showSuccess = useToastStore((s) => s.success);
  const showError = useToastStore((s) => s.error);
  const [hasRna, setHasRna] = useState(false);
  const [isCommitting, setIsCommitting] = useState(false);

  useEffect(() => {
    void loadCommittedWeights(patientId);
    listFiles(patientId, "01_upload")
      .then((files) => setHasRna(files.some((f) => f.fileKind === "rna")))
      .catch(() => setHasRna(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId]);

  useEffect(() => {
    void fetchPreview(patientId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId, weights, targetCount]);

  const alleleCoverage = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const c of previewCandidates) {
      if (c.isSelected) counts[c.hlaAllele] = (counts[c.hlaAllele] ?? 0) + 1;
    }
    return counts;
  }, [previewCandidates]);
  const maxCoverage = Math.max(1, ...Object.values(alleleCoverage), 0);

  const handleCommit = async () => {
    setIsCommitting(true);
    try {
      await commitRanking(patientId);
      showSuccess("Ranking committed", `${targetCount} candidates selected`);
    } catch (err) {
      showError("Could not commit ranking", isApiError(err) ? err.detail ?? err.message : undefined);
    } finally {
      setIsCommitting(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <StepExplanation definition={definition} />

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-[320px_1fr]">
        <div className="flex flex-col gap-6 rounded-md border border-rule bg-surface p-6">
          <h3 className="text-h2 text-ink">Weights</h3>
          {WEIGHT_FIELDS.map((field) => (
            <WeightSlider
              key={field.key}
              label={field.label}
              description={field.description}
              value={weights[field.key]}
              disabled={field.key === "expression" && !hasRna}
              disabledReason="No RNA-seq uploaded in step 1"
              onChange={(v) => setWeight(field.key, v)}
            />
          ))}

          <label className="flex flex-col gap-1.5 border-t border-rule pt-4">
            <span className="text-ui text-ink">Target count</span>
            <input
              type="number"
              min={1}
              max={100}
              value={targetCount}
              onChange={(e) => setTargetCount(Number(e.target.value))}
              className="w-24 rounded-md border border-rule-strong px-3 py-2 font-mono text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent"
            />
          </label>

          <div className="flex gap-3">
            <Button variant="secondary" size="sm" onClick={resetWeights}>
              Reset
            </Button>
            <Button size="sm" onClick={handleCommit} isLoading={isCommitting} disabled={!hasUnsavedChanges && !!state?.lastRunAt}>
              Commit ranking
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-6">
          <div className="rounded-md border border-rule bg-surface p-6">
            <h3 className="mb-4 text-h2 text-ink">HLA allele coverage of selected set</h3>
            {Object.keys(alleleCoverage).length === 0 ? (
              <p className="text-body text-slate">No selection yet.</p>
            ) : (
              <div className="flex flex-col gap-2">
                {Object.entries(alleleCoverage).map(([allele, count]) => (
                  <div key={allele} className="flex items-center gap-3">
                    <span className="w-28 shrink-0 font-mono text-small text-slate">{allele}</span>
                    <div className="h-3 flex-1 overflow-hidden rounded-sm bg-paper">
                      <div className="h-full bg-accent" style={{ width: `${(count / maxCoverage) * 100}%` }} />
                    </div>
                    <span className="w-8 text-right font-mono text-small text-ink">{count}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="rounded-md border border-rule bg-surface p-6">
            <h3 className="mb-4 text-h2 text-ink">Live ranked preview</h3>
            <CandidateTable
              candidates={previewCandidates}
              columns={["rank", "peptide", "allele", "gene", "finalScore"]}
              sortBy="finalRank"
              isLoading={isPreviewLoading}
              emptyMessage="Adjust weights to preview ranking."
            />
          </div>
        </div>
      </div>
    </div>
  );
}
