export function formatBytes(bytes: number, decimals = 1): string {
  if (!Number.isFinite(bytes) || bytes < 0) return "—";
  if (bytes === 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const exp = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1
  );
  const value = bytes / Math.pow(1024, exp);
  return `${value.toFixed(exp === 0 ? 0 : decimals)} ${units[exp]}`;
}

export function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "—";
  if (seconds < 1) return `${Math.round(seconds * 1000)}ms`;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const mins = Math.floor(seconds / 60);
  const secs = Math.round(seconds % 60);
  if (mins < 60) return `${mins}m ${secs}s`;
  const hours = Math.floor(mins / 60);
  const remMins = mins % 60;
  return `${hours}h ${remMins}m`;
}

export function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function formatRelativeTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  const diffMs = d.getTime() - Date.now();
  const diffSec = Math.round(diffMs / 1000);

  const divisions: [number, Intl.RelativeTimeFormatUnit][] = [
    [60, "second"],
    [60, "minute"],
    [24, "hour"],
    [7, "day"],
    [4.34524, "week"],
    [12, "month"],
    [Number.POSITIVE_INFINITY, "year"],
  ];

  let duration = diffSec;
  const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });
  for (const [amount, unit] of divisions) {
    if (Math.abs(duration) < amount) {
      return rtf.format(Math.round(duration), unit);
    }
    duration /= amount;
  }
  return rtf.format(Math.round(duration), "year");
}

export function formatScore(score: number | undefined, decimals = 3): string {
  if (score === undefined || score === null || Number.isNaN(score)) return "—";
  return score.toFixed(decimals);
}

export function formatPercent(value: number, decimals = 1): string {
  if (!Number.isFinite(value)) return "—";
  return `${(value * 100).toFixed(decimals)}%`;
}

export function truncatePeptide(peptide: string, maxLength = 20): string {
  if (!peptide) return "";
  if (peptide.length <= maxLength) return peptide;
  return `${peptide.slice(0, maxLength)}…`;
}

/**
 * Character-aligns a mutant peptide against its wild-type counterpart and
 * flags which positions differ. The signature visual element of the app
 * (Appendix C.6) — used by CandidateTable, the ranking preview, and
 * ConstructDiagram to pick the mutated residue out in accent color.
 *
 * Peptides are typically the same length (a single substituted residue),
 * but frameshift-derived candidates can differ in length; positions beyond
 * the shorter sequence are treated as mutated (there is no wild-type
 * counterpart to compare against).
 */
export function highlightMutation(
  mutant: string,
  wildType: string
): { char: string; isMutated: boolean }[] {
  const chars = mutant.split("");
  return chars.map((char, i) => ({
    char,
    isMutated: i >= wildType.length || wildType[i] !== char,
  }));
}
