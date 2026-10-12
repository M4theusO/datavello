import { CalendarRange } from "lucide-react"
import { SimpleSelect } from "@/components/SimpleSelect"
import { Input } from "@/components/ui/input"
import { type DateRange, type PeriodPreset, periodLabels } from "@/lib/periods"

export interface PeriodValue {
  preset: PeriodPreset
  /** Only used when preset === "custom". */
  custom: Partial<DateRange>
}

/** Preset list first (the common case), custom range behind the last option. */
export function PeriodFilter({
  value,
  onChange,
  presets,
}: {
  value: PeriodValue
  onChange: (value: PeriodValue) => void
  presets: PeriodPreset[]
}) {
  const options = presets.map((p) => ({ value: p, label: periodLabels[p] }))

  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative">
        <CalendarRange className="pointer-events-none absolute top-1/2 left-2.5 z-10 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <SimpleSelect
          aria-label="Período"
          value={value.preset}
          onChange={(preset) => onChange({ preset: preset as PeriodPreset, custom: value.custom })}
          options={options}
          className="h-9 min-w-48 bg-card pl-8"
        />
      </div>
      {value.preset === "custom" && (
        <div className="flex items-center gap-2">
          <Input
            type="date"
            aria-label="Data inicial"
            className="h-9 w-40 bg-card"
            value={value.custom.from ?? ""}
            max={value.custom.to}
            onChange={(e) => onChange({ ...value, custom: { ...value.custom, from: e.target.value || undefined } })}
          />
          <span className="text-sm text-muted-foreground">até</span>
          <Input
            type="date"
            aria-label="Data final"
            className="h-9 w-40 bg-card"
            value={value.custom.to ?? ""}
            min={value.custom.from}
            onChange={(e) => onChange({ ...value, custom: { ...value.custom, to: e.target.value || undefined } })}
          />
        </div>
      )}
    </div>
  )
}
