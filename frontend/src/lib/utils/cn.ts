type ClassValue = string | number | null | false | undefined | ClassValue[];

function flatten(input: ClassValue, out: string[]): void {
  if (!input && input !== 0) return;
  if (Array.isArray(input)) {
    for (const item of input) flatten(item, out);
    return;
  }
  out.push(String(input));
}

/** Minimal classnames joiner — no external dependency needed for this app's needs. */
export function cn(...inputs: ClassValue[]): string {
  const out: string[] = [];
  for (const input of inputs) flatten(input, out);
  return out.join(" ");
}
