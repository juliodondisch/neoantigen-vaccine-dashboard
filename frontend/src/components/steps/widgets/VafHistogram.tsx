interface VafHistogramProps {
  vafValues: number[];
  binCount?: number;
  height?: number;
}

export function VafHistogram({ vafValues, binCount = 20, height = 180 }: VafHistogramProps) {
  if (vafValues.length === 0) {
    return <p className="text-body text-slate">No VAF data yet.</p>;
  }

  const binWidth = 1 / binCount;
  const bins = new Array(binCount).fill(0);
  for (const v of vafValues) {
    const idx = Math.min(binCount - 1, Math.max(0, Math.floor(v / binWidth)));
    bins[idx] += 1;
  }
  const max = Math.max(1, ...bins);

  return (
    <div>
      <div
        role="img"
        aria-label={`Variant allele frequency distribution across ${vafValues.length} variants`}
        className="flex items-end gap-[2px]"
        style={{ height }}
      >
        {bins.map((count, i) => {
          const binStart = i * binWidth;
          const binEnd = binStart + binWidth;
          return (
            <div
              key={i}
              className="flex-1 rounded-sm bg-accent"
              title={`${(binStart * 100).toFixed(0)}–${(binEnd * 100).toFixed(0)}%: ${count} variant${count === 1 ? "" : "s"}`}
              style={{ height: `${(count / max) * 100}%`, minHeight: count > 0 ? 2 : 0 }}
            />
          );
        })}
      </div>
      <div className="mt-1.5 flex justify-between text-small text-slate">
        <span className="font-mono">0%</span>
        <span className="font-mono">VAF</span>
        <span className="font-mono">100%</span>
      </div>
    </div>
  );
}
