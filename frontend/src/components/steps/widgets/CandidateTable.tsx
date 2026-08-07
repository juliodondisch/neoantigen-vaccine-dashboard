import { cn } from "@/lib/utils/cn";
import { highlightMutation, formatPercent, formatScore } from "@/lib/utils/format";
import { DataTable, type DataTableColumn } from "@/components/common/DataTable";
import type { NeoantigenCandidate } from "@/types/candidate";

export type CandidateColumn =
  | "rank" | "peptide" | "wildType" | "allele" | "gene"
  | "presentation" | "immunogenicity" | "agretopicity" | "vaf" | "expression" | "finalScore";

interface CandidateTableProps {
  candidates: NeoantigenCandidate[];
  columns?: CandidateColumn[];
  maxRows?: number;
  highlightSelected?: boolean;
  sortBy?: keyof NeoantigenCandidate;
  isLoading?: boolean;
  emptyMessage?: string;
}

const DEFAULT_COLUMNS: CandidateColumn[] = [
  "rank",
  "peptide",
  "wildType",
  "allele",
  "gene",
  "presentation",
  "immunogenicity",
  "finalScore",
];

/** The signature peptide-diff element (Appendix C.6). */
function PeptideDiff({ mutant, wildType }: { mutant: string; wildType: string }) {
  const highlighted = highlightMutation(mutant, wildType);
  return (
    <span className="whitespace-nowrap font-mono text-ui">
      {highlighted.map((c, i) => (
        <span
          key={i}
          className={cn(
            c.isMutated && "rounded-sm bg-accent-muted px-0.5 font-semibold text-accent"
          )}
        >
          {c.char}
        </span>
      ))}
    </span>
  );
}

export function CandidateTable({
  candidates,
  columns = DEFAULT_COLUMNS,
  maxRows,
  highlightSelected = true,
  sortBy,
  isLoading = false,
  emptyMessage = "No candidates yet.",
}: CandidateTableProps) {
  let rows = candidates;
  if (sortBy) {
    // Rank is naturally ascending (1st place first); every other numeric
    // field (scores, VAF, etc.) reads best-first as descending.
    const ascending = sortBy === "finalRank";
    rows = [...rows].sort((a, b) => {
      const av = a[sortBy];
      const bv = b[sortBy];
      if (typeof av === "number" && typeof bv === "number") {
        return ascending ? av - bv : bv - av;
      }
      return String(bv ?? "").localeCompare(String(av ?? ""));
    });
  }
  if (maxRows) rows = rows.slice(0, maxRows);

  const colDefs: Record<CandidateColumn, DataTableColumn<NeoantigenCandidate>> = {
    rank: {
      key: "rank",
      header: "Rank",
      align: "right",
      width: "64px",
      render: (c) => (
        <span className="inline-flex items-center justify-end gap-1.5 font-mono">
          {highlightSelected && c.isSelected && (
            <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-accent" />
          )}
          {c.finalRank ?? ", "}
        </span>
      ),
    },
    peptide: {
      key: "peptide",
      header: "Mutant peptide",
      render: (c) => <PeptideDiff mutant={c.mutantPeptide} wildType={c.wildTypePeptide} />,
    },
    wildType: {
      key: "wildType",
      header: "Wild-type",
      render: (c) => <span className="font-mono text-ui text-slate">{c.wildTypePeptide}</span>,
    },
    allele: {
      key: "allele",
      header: "HLA allele",
      render: (c) => <span className="font-mono text-ui">{c.hlaAllele}</span>,
    },
    gene: {
      key: "gene",
      header: "Gene",
      render: (c) => <span className="font-mono text-ui">{c.geneSymbol}</span>,
    },
    presentation: {
      key: "presentation",
      header: "Presentation",
      align: "right",
      render: (c) => <span className="font-mono">{formatScore(c.presentationScore)}</span>,
    },
    immunogenicity: {
      key: "immunogenicity",
      header: "Immunogenicity",
      align: "right",
      render: (c) => <span className="font-mono">{formatScore(c.immunogenicityScore)}</span>,
    },
    agretopicity: {
      key: "agretopicity",
      header: "Agretopicity",
      align: "right",
      render: (c) => <span className="font-mono">{formatScore(c.agretopicity)}</span>,
    },
    vaf: {
      key: "vaf",
      header: "VAF",
      align: "right",
      render: (c) => <span className="font-mono">{formatPercent(c.vaf)}</span>,
    },
    expression: {
      key: "expression",
      header: "TPM",
      align: "right",
      render: (c) => (
        <span className="font-mono">
          {c.expressionTpm !== undefined ? c.expressionTpm.toFixed(1) : ", "}
        </span>
      ),
    },
    finalScore: {
      key: "finalScore",
      header: "Final score",
      align: "right",
      render: (c) => <span className="font-mono font-semibold">{formatScore(c.finalScore)}</span>,
    },
  };

  return (
    <DataTable
      data={rows}
      columns={columns.map((c) => colDefs[c])}
      keyExtractor={(c) => c.candidateId}
      isLoading={isLoading}
      emptyMessage={emptyMessage}
      maxHeight="560px"
    />
  );
}
