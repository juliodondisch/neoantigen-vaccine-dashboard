import { cn } from "@/lib/utils/cn";
import type { ConstructElement, VaccineConstruct } from "@/types/candidate";

interface ConstructDiagramProps {
  construct: VaccineConstruct;
  showSequence?: boolean;
}

const ELEMENT_STYLE: Record<ConstructElement["type"], { label: string; color: string }> = {
  "5utr": { label: "5' UTR", color: "var(--data-6)" },
  signal: { label: "Signal", color: "var(--data-5)" },
  neoantigen: { label: "Neoantigen", color: "var(--color-accent)" },
  linker: { label: "Linker", color: "var(--color-rule-strong)" },
  "3utr": { label: "3' UTR", color: "var(--data-6)" },
  polyA: { label: "Poly-A", color: "var(--data-4)" },
};

export function ConstructDiagram({ construct, showSequence = false }: ConstructDiagramProps) {
  const total = construct.totalLengthBp || 1;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex h-10 w-full overflow-hidden rounded-md border border-rule">
        {construct.elements.map((el, i) => {
          const width = ((el.endPosition - el.startPosition) / total) * 100;
          const style = ELEMENT_STYLE[el.type];
          return (
            <div
              key={i}
              title={`${style.label}${el.label ? ` — ${el.label}` : ""} (${el.endPosition - el.startPosition} bp)`}
              className={cn(
                "flex h-full items-center justify-center overflow-hidden text-micro text-white",
                i > 0 && "border-l border-surface"
              )}
              style={{
                width: `${Math.max(width, 1.5)}%`,
                backgroundColor: style.color,
              }}
            >
              {width > 6 && <span className="truncate px-1">{style.label}</span>}
            </div>
          );
        })}
      </div>

      <div className="flex flex-wrap gap-x-6 gap-y-2 text-small text-slate">
        <span>
          Length: <span className="font-mono text-ink">{construct.totalLengthBp} bp</span>
        </span>
        <span>
          Neoantigens: <span className="font-mono text-ink">{construct.peptideOrder.length}</span>
        </span>
        <span>
          Junctional epitopes avoided:{" "}
          <span className="font-mono text-ink">{construct.junctionalEpitopesAvoided}</span>
        </span>
        <span>
          Linker: <span className="font-mono text-ink">{construct.linkerSequence}</span>
        </span>
        <span>
          Poly-A: <span className="font-mono text-ink">{construct.polyATailLength} nt</span>
        </span>
      </div>

      {showSequence && (
        <pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all rounded-md bg-paper p-4 font-mono text-small text-ink">
          {construct.fullSequence}
        </pre>
      )}

      <p className="text-small text-slate">
        {construct.fullSequence.length.toLocaleString()} nucleotides.
      </p>
    </div>
  );
}
