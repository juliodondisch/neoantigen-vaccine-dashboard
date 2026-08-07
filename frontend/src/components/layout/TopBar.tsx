import type React from "react";
import Link from "next/link";

interface TopBarProps {
  patientName?: string;
  showBackLink?: boolean;
  rightSlot?: React.ReactNode;
}

export function TopBar({ patientName, showBackLink, rightSlot }: TopBarProps) {
  return (
    <header className="flex h-16 items-center justify-between border-b border-rule bg-surface px-8">
      <div className="flex items-center gap-4">
        {showBackLink && (
          <Link
            href="/"
            className="text-ui text-slate hover:text-ink"
            aria-label="Back to patient list"
          >
            &larr; Patients
          </Link>
        )}
        <span className="text-h1 text-ink">
          {patientName ?? "Neoantigen Pipeline"}
        </span>
      </div>
      {rightSlot && <div className="flex items-center gap-3">{rightSlot}</div>}
    </header>
  );
}
