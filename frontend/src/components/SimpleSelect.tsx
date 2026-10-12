import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { cn } from "@/lib/utils"

export interface SelectOption {
  value: string
  label: string
}

/** Single-value select over a flat option list (labels shown in the trigger). */
export function SimpleSelect({
  id,
  value,
  onChange,
  options,
  placeholder,
  className,
  invalid,
  disabled,
  "aria-label": ariaLabel,
}: {
  id?: string
  value: string | null
  onChange: (value: string) => void
  options: SelectOption[]
  placeholder?: string
  className?: string
  invalid?: boolean
  disabled?: boolean
  "aria-label"?: string
}) {
  return (
    <Select
      items={options}
      value={value || null}
      onValueChange={(next) => next !== null && onChange(next as string)}
      disabled={disabled}
    >
      <SelectTrigger id={id} className={cn("w-full", className)} aria-invalid={invalid || undefined} aria-label={ariaLabel}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
