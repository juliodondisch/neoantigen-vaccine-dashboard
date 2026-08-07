import { cn } from "@/lib/utils/cn";

interface WeightSliderProps {
  label: string;
  description: string;
  value: number;
  min?: number;
  max?: number;
  step?: number;
  disabled?: boolean;
  disabledReason?: string;
  onChange: (value: number) => void;
}

export function WeightSlider({
  label,
  description,
  value,
  min = 0,
  max = 1,
  step = 0.01,
  disabled = false,
  disabledReason,
  onChange,
}: WeightSliderProps) {
  return (
    <div className={cn("flex flex-col gap-1.5", disabled && "opacity-40")}>
      <div className="flex items-baseline justify-between">
        <label className="text-ui text-ink">{label}</label>
        <span className="font-mono text-ui text-ink">{value.toFixed(2)}</span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className={cn(
          "h-1.5 w-full appearance-none rounded-full bg-rule accent-accent",
          "[&::-webkit-slider-thumb]:h-4 [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:appearance-none",
          "[&::-webkit-slider-thumb]:rounded-sm [&::-webkit-slider-thumb]:bg-accent",
          disabled && "cursor-not-allowed"
        )}
      />
      <p className="text-small text-slate">
        {disabled && disabledReason ? disabledReason : description}
      </p>
    </div>
  );
}
