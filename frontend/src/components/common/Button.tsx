import type React from "react";
import { cn } from "@/lib/utils/cn";
import { Spinner } from "./Spinner";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  size?: "sm" | "md" | "lg";
  isLoading?: boolean;
  leftIcon?: React.ReactNode;
}

const VARIANT_CLASSES: Record<NonNullable<ButtonProps["variant"]>, string> = {
  primary: "bg-accent text-white border border-accent hover:bg-accent-hover",
  secondary: "bg-transparent text-ink border border-rule-strong hover:bg-paper",
  danger: "bg-transparent text-state-failed border border-state-failed hover:bg-feedback-errorBg",
  ghost: "bg-transparent text-slate border border-transparent hover:bg-paper",
};

const SIZE_CLASSES: Record<NonNullable<ButtonProps["size"]>, string> = {
  sm: "text-small px-3 py-1.5 gap-1.5",
  md: "text-ui px-5 py-2.5 gap-2",
  lg: "text-body px-6 py-3 gap-2",
};

export function Button({
  variant = "primary",
  size = "md",
  isLoading = false,
  leftIcon,
  className,
  children,
  disabled,
  ...rest
}: ButtonProps) {
  return (
    <button
      className={cn(
        "inline-flex items-center justify-center rounded-md font-sans font-medium transition-colors",
        "disabled:opacity-40 disabled:cursor-not-allowed",
        VARIANT_CLASSES[variant],
        SIZE_CLASSES[size],
        className
      )}
      disabled={disabled || isLoading}
      {...rest}
    >
      {isLoading ? (
        <Spinner size="sm" className={variant === "primary" ? "border-white/40 border-t-white" : undefined} />
      ) : (
        leftIcon
      )}
      {children}
    </button>
  );
}
