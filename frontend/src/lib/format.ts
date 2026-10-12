const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" })
const integer = new Intl.NumberFormat("pt-BR")
const dateFormatter = new Intl.DateTimeFormat("pt-BR", { timeZone: "UTC" })

/** 1234.5 → "R$ 1.234,50" */
export function formatCurrency(value: number): string {
  return currency.format(value)
}

export function formatInteger(value: number): string {
  return integer.format(value)
}

/** API dates are "yyyy-MM-dd" (no time zone) → "DD/MM/AAAA". */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.slice(0, 10).split("-").map(Number)
  return dateFormatter.format(new Date(Date.UTC(year, month - 1, day)))
}

/** Local calendar date → "yyyy-MM-dd" for the API. */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, "0")
  const d = String(date.getDate()).padStart(2, "0")
  return `${y}-${m}-${d}`
}

/** "Maria da Silva" → "MS" */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return "?"
  const first = parts[0][0]
  const last = parts.length > 1 ? parts[parts.length - 1][0] : ""
  return (first + last).toUpperCase()
}
