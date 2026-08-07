interface ConsequenceChartProps {
  counts: Record<string, number>;
  height?: number;
}

// Fixed category order + Okabe–Ito data tokens (Appendix C.2) ,  categorical
// hue is assigned by identity, never cycled/re-sorted by value.
const CONSEQUENCE_ORDER: { key: string; label: string; color: string }[] = [
  { key: "missense", label: "Missense", color: "var(--data-1)" },
  { key: "stop_gained", label: "Stop gained", color: "var(--data-2)" },
  { key: "frameshift", label: "Frameshift", color: "var(--data-3)" },
  { key: "inframe_indel", label: "Inframe indel", color: "var(--data-4)" },
  { key: "start_lost", label: "Start lost", color: "var(--data-5)" },
  { key: "other", label: "Other", color: "var(--data-6)" },
];

export function ConsequenceChart({ counts, height = 220 }: ConsequenceChartProps) {
  const entries = CONSEQUENCE_ORDER.map((c) => ({ ...c, value: counts[c.key] ?? 0 }));
  const max = Math.max(1, ...entries.map((e) => e.value));
  const total = entries.reduce((sum, e) => sum + e.value, 0);

  if (total === 0) {
    return <p className="text-body text-slate">No consequence data yet.</p>;
  }

  return (
    <div>
      <div
        role="img"
        aria-label={`Variant consequences: ${entries.map((e) => `${e.label} ${e.value}`).join(", ")}`}
        className="flex flex-col justify-end gap-3"
        style={{ minHeight: height }}
      >
        {entries.map((e) => (
          <div key={e.key} className="flex items-center gap-3">
            <span className="w-32 shrink-0 text-small text-slate">{e.label}</span>
            <div className="h-4 flex-1 overflow-hidden rounded-sm bg-paper" title={`${e.label}: ${e.value}`}>
              <div
                className="h-full rounded-sm"
                style={{ width: `${(e.value / max) * 100}%`, backgroundColor: e.color }}
              />
            </div>
            <span className="w-12 shrink-0 text-right font-mono text-small text-ink">{e.value}</span>
          </div>
        ))}
      </div>
      <details className="mt-3">
        <summary className="cursor-pointer text-small text-accent hover:text-accent-hover">
          View as table
        </summary>
        <table className="mt-2 w-full text-small">
          <thead>
            <tr className="text-left text-micro text-slate">
              <th className="py-1">Consequence</th>
              <th className="py-1 text-right">Count</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((e) => (
              <tr key={e.key} className="border-t border-rule">
                <td className="py-1">{e.label}</td>
                <td className="py-1 text-right font-mono">{e.value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </div>
  );
}
