const compactNumber = new Intl.NumberFormat("pt-BR", { notation: "compact", maximumFractionDigits: 1 })
const monthShort = new Intl.DateTimeFormat("pt-BR", { month: "short", timeZone: "UTC" })
const monthLong = new Intl.DateTimeFormat("pt-BR", { month: "long", year: "numeric", timeZone: "UTC" })
const percent = new Intl.NumberFormat("pt-BR", { style: "percent", maximumFractionDigits: 1 })

/** Axis ticks: 0 / 1,2 mil / 3,4 mi (no currency symbol — the chart title says R$). */
export function formatCompact(value: number): string {
  return compactNumber.format(value)
}

/** Value labels: "R$ 1,2 mil". */
export function formatCompactCurrency(value: number): string {
  return `R$ ${compactNumber.format(value)}`
}

function monthDate(month: string) {
  const [y, m] = month.split("-").map(Number)
  return new Date(Date.UTC(y, m - 1, 1))
}

/** "2026-03" -> "mar/26" */
export function formatMonthShort(month: string): string {
  const date = monthDate(month)
  return `${monthShort.format(date).replace(".", "")}/${String(date.getUTCFullYear()).slice(2)}`
}

/** "2026-03" -> "março de 2026" */
export function formatMonthLong(month: string): string {
  return monthLong.format(monthDate(month))
}

export function formatPercent(value: number): string {
  return percent.format(value)
}

/**
 * Round axis steps (1, 2, 2.5, 5 × 10ⁿ) covering the data and zero, e.g. 0 / 10 mil / 20 mil / 30 mil / 40 mil.
 * Recharts' own "nice" steps can produce values like 8,5 mil.
 */
export function niceScale(values: number[], targetTicks = 4): { domain: [number, number]; ticks: number[] } {
  let min = Math.min(0, ...values)
  let max = Math.max(0, ...values)
  if (min === max) max = min + 1

  const rawStep = (max - min) / targetTicks
  const magnitude = 10 ** Math.floor(Math.log10(rawStep))
  const normalized = rawStep / magnitude
  const step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10) * magnitude

  min = Math.floor(min / step) * step
  max = Math.ceil(max / step) * step
  const ticks: number[] = []
  for (let v = min; v <= max + step / 2; v += step) ticks.push(Math.round(v * 100) / 100)
  return { domain: [min, max], ticks }
}
