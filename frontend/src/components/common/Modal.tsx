"use client";

import type React from "react";
import { useEffect, useRef } from "react";
import { cn } from "@/lib/utils/cn";

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  size?: "sm" | "md" | "lg";
}

const SIZE_CLASSES: Record<NonNullable<ModalProps["size"]>, string> = {
  sm: "max-w-sm",
  md: "max-w-lg",
  lg: "max-w-2xl",
};

export function Modal({ isOpen, onClose, title, children, footer, size = "md" }: ModalProps) {
  const closeRef = useRef<HTMLButtonElement>(null);

  // Separate from the key-listener effect below: this one must only fire when the
  // modal transitions open, not on every re-render where `onClose` gets a new
  // identity (e.g. a parent recreating its close handler each render) ,  otherwise
  // it steals focus back to the close button on every keystroke inside the modal.
  useEffect(() => {
    if (!isOpen) return;
    closeRef.current?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink/40 p-4"
      role="presentation"
      onClick={onClose}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        className={cn(
          "w-full rounded-lg border border-rule bg-surface shadow-overlay",
          SIZE_CLASSES[size]
        )}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-rule px-6 py-4">
          <h2 id="modal-title" className="text-h2 text-ink">
            {title}
          </h2>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-sm px-2 py-1 text-slate hover:bg-paper"
          >
            &times;
          </button>
        </div>
        <div className="px-6 py-6">{children}</div>
        {footer && <div className="border-t border-rule px-6 py-4">{footer}</div>}
      </div>
    </div>
  );
}
