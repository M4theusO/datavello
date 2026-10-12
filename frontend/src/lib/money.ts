/**
 * Parses an amount typed in Brazilian format: "1.234,56", "1234,5", "1234.56" or "R$ 10".
 * Returns null when the text is not a valid positive-or-zero amount with at most 2 decimals.
 */
export function parseMoneyInput(input: string): number | null {
  let text = input.replace(/R\$|\s/g, "")
  if (!text) return null

  const lastComma = text.lastIndexOf(",")
  const lastDot = text.lastIndexOf(".")
  if (lastComma > lastDot) {
    // "1.234,56" -> comma is the decimal separator
    text = text.replace(/\./g, "").replace(",", ".")
  } else if (lastDot > -1 && lastComma > -1) {
    // "1,234.56" -> dot is the decimal separator
    text = text.replace(/,/g, "")
  } else if (lastDot > -1 && /^\d{1,3}(\.\d{3})+$/.test(text)) {
    // "1.234" (only thousand separators)
    text = text.replace(/\./g, "")
  }

  if (!/^\d+(\.\d{1,2})?$/.test(text)) return null
  return Number(text)
}

/** 1234.5 -> "1.234,50" (no currency symbol, for form inputs). */
export function formatMoneyInput(value: number): string {
  return value.toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
