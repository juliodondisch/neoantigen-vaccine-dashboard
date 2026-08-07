import { create } from "zustand";

export type ToastVariant = "success" | "error" | "info" | "warning";

export interface Toast {
  id: string;
  variant: ToastVariant;
  title: string;
  message?: string;
  createdAt: number;
  durationMs: number;
  persistent: boolean;
}

interface ToastStore {
  toasts: Toast[];

  show: (toast: Omit<Toast, "id" | "createdAt">) => string;
  success: (title: string, message?: string) => string;
  error: (title: string, message?: string) => string;
  info: (title: string, message?: string) => string;
  warning: (title: string, message?: string) => string;
  dismiss: (id: string) => void;
  dismissAll: () => void;
}

function makeId(): string {
  return `toast_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`;
}

// Not persisted.
export const useToastStore = create<ToastStore>()((set, get) => ({
  toasts: [],

  show: (toast) => {
    const id = makeId();
    set((state) => ({
      toasts: [...state.toasts, { ...toast, id, createdAt: Date.now() }],
    }));
    return id;
  },

  // success/info auto-dismiss at 4000ms; errors persist until dismissed , 
  // the Python stderr text is the most valuable thing on screen.
  success: (title, message) => get().show({ variant: "success", title, message, durationMs: 4000, persistent: false }),
  info: (title, message) => get().show({ variant: "info", title, message, durationMs: 4000, persistent: false }),
  warning: (title, message) => get().show({ variant: "warning", title, message, durationMs: 6000, persistent: false }),
  error: (title, message) => get().show({ variant: "error", title, message, durationMs: 0, persistent: true }),

  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })),

  dismissAll: () => set({ toasts: [] }),
}));
