import { toIsoDate } from "./format"

/** Same keys as the backend DashboardPeriod enum, plus "custom" and "all". */
export type PeriodPreset =
  | "Last30Days"
  | "ThisMonth"
  | "LastMonth"
  | "Last3Months"
  | "Last6Months"
  | "Last12Months"
  | "ThisYear"
  | "custom"
  | "all"

export const periodLabels: Record<PeriodPreset, string> = {
  Last30Days: "Últimos 30 dias",
  ThisMonth: "Este mês",
  LastMonth: "Mês passado",
  Last3Months: "Últimos 3 meses",
  Last6Months: "Últimos 6 meses",
  Last12Months: "Últimos 12 meses",
  ThisYear: "Este ano",
  custom: "Período personalizado",
  all: "Todo o período",
}

export const dashboardPresets: PeriodPreset[] = [
  "Last30Days",
  "ThisMonth",
  "LastMonth",
  "Last3Months",
  "Last6Months",
  "Last12Months",
  "ThisYear",
  "custom",
]

export interface DateRange {
  from: string // yyyy-MM-dd
  to: string
}

function monthStart(year: number, month: number) {
  return new Date(year, month, 1)
}

function monthEnd(year: number, month: number) {
  return new Date(year, month + 1, 0)
}

/**
 * Concrete dates for a preset, relative to `today`. "Last N months" covers whole calendar
 * months up to the end of the current one, so the backend compares whole months.
 */
export function rangeForPreset(preset: PeriodPreset, today = new Date()): DateRange | null {
  const y = today.getFullYear()
  const m = today.getMonth()
  const range = (from: Date, to: Date): DateRange => ({ from: toIsoDate(from), to: toIsoDate(to) })

  switch (preset) {
    case "Last30Days":
      return range(new Date(y, m, today.getDate() - 29), today)
    case "ThisMonth":
      return range(monthStart(y, m), monthEnd(y, m))
    case "LastMonth":
      return range(monthStart(y, m - 1), monthEnd(y, m - 1))
    case "Last3Months":
      return range(monthStart(y, m - 2), monthEnd(y, m))
    case "Last6Months":
      return range(monthStart(y, m - 5), monthEnd(y, m))
    case "Last12Months":
      return range(monthStart(y, m - 11), monthEnd(y, m))
    case "ThisYear":
      return range(monthStart(y, 0), monthEnd(y, 11))
    default:
      return null
  }
}

export function isPeriodPreset(value: string | null): value is PeriodPreset {
  return value !== null && value in periodLabels
}
