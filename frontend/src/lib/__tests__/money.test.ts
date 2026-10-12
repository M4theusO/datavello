import { describe, expect, it } from "vitest"
import { formatMoneyInput, parseMoneyInput } from "../money"

describe("parseMoneyInput", () => {
  it.each([
    ["1.234,56", 1234.56],
    ["1234,5", 1234.5],
    ["R$ 10", 10],
    ["1,234.56", 1234.56],
    ["1234.56", 1234.56],
    ["1.234", 1234],
    ["0,99", 0.99],
  ])("parses %s", (input, expected) => {
    expect(parseMoneyInput(input)).toBe(expected)
  })

  it.each(["", "abc", "10,555", "1.2.3,4,5", "-10"])("rejects %s", (input) => {
    expect(parseMoneyInput(input)).toBeNull()
  })

  it("formats back to the Brazilian input format", () => {
    expect(formatMoneyInput(1234.5)).toBe("1.234,50")
  })
})
