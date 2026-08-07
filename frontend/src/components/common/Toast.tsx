"use client";

import { useEffect } from "react";
import { cn } from "@/lib/utils/cn";
import type { Toast as ToastType } from "@/stores/useToastStore";

interface ToastProps {
  toast: ToastType;
  onDismiss: (id: string) => void;
}

const VARIANT_BORDER: Record<ToastType["variant"], string> = {
  success: "border-l-state-complete",
  error: "border-l-state-failed",
  warning: "border-l-state-blocked",
  info: "border-l-accent",
};

export function Toast({ toast, onDismiss }: ToastProps) {
  useEffect(() => {
    if (toast.persistent || toast.durationMs <= 0) return;
    const timer = setTimeout(() => onDismiss(toast.id), toast.durationMs);
    return () => clearTimeout(timer);
  }, [toast.id, toast.persistent, toast.durationMs, onDismiss]);

  return (
    <div
      role={toast.variant === "error" ? "alert" : "status"}
      className={cn(
        "w-80 rounded-lg border border-l-[3px] border-rule bg-surface p-4 shadow-overlay",
        VARIANT_BORDER[toast.variant]
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-ui font-medium text-ink">{toast.title}</p>
          {toast.message && (
            <p className="mt-1 whitespace-pre-wrap break-words text-small text-slate">
              {toast.message}
            </p>
          )}
        </div>
        <button
          type="button"
          onClick={() => onDismiss(toast.id)}
          aria-label="Dismiss"
          className="shrink-0 text-slate hover:text-ink"
        >
          &times;
        </button>
      </div>
    </div>
  );
}
