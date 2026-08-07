export interface PollOptions {
  intervalMs: number;
  maxAttempts?: number;
  timeoutMs?: number;
  onTick?: (attempt: number) => void;
}

/**
 * Repeatedly calls `fn` until `predicate(result)` is true, or a stop
 * condition (maxAttempts / timeoutMs) is hit — in which case the last
 * result is returned as-is (callers decide whether that's an error).
 */
export async function pollUntil<T>(
  fn: () => Promise<T>,
  predicate: (result: T) => boolean,
  options: PollOptions
): Promise<T> {
  const { intervalMs, maxAttempts, timeoutMs, onTick } = options;
  const startedAt = Date.now();
  let attempt = 0;

  while (true) {
    attempt += 1;
    onTick?.(attempt);
    const result = await fn();
    if (predicate(result)) return result;

    if (maxAttempts !== undefined && attempt >= maxAttempts) return result;
    if (timeoutMs !== undefined && Date.now() - startedAt >= timeoutMs) return result;

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
}

/** Cancellable wrapper around pollUntil for use in effects/hooks. */
export function createPoller<T>(
  fn: () => Promise<T>,
  predicate: (result: T) => boolean,
  options: PollOptions
): { start: () => Promise<T>; stop: () => void } {
  let stopped = false;

  const start = async (): Promise<T> => {
    const { intervalMs, maxAttempts, timeoutMs, onTick } = options;
    const startedAt = Date.now();
    let attempt = 0;

    while (!stopped) {
      attempt += 1;
      onTick?.(attempt);
      const result = await fn();
      if (stopped) return result;
      if (predicate(result)) return result;

      if (maxAttempts !== undefined && attempt >= maxAttempts) return result;
      if (timeoutMs !== undefined && Date.now() - startedAt >= timeoutMs) return result;

      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
    return fn();
  };

  return {
    start,
    stop: () => {
      stopped = true;
    },
  };
}
