import { describe, expect, it } from "vitest"
import { formatCurrency, formatDate, initials } from "../format"

describe("format", () => {
  it("formats BRL", () => {
    expect(formatCurrency(1234.5).replace(/\s/g, " ")).toBe("R$ 1.234,50")
  })

  it("formats API dates as DD/MM/AAAA without time zone shifts", () => {
    expect(formatDate("2026-01-01")).toBe("01/01/2026")
    expect(formatDate("2026-12-31")).toBe("31/12/2026")
  })

  it("builds initials", () => {
    expect(initials("Maria da Silva")).toBe("MS")
    expect(initials("  ana ")).toBe("A")
  })
})
