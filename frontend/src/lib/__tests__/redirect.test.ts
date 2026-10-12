import { describe, expect, it } from "vitest"
import { safeRedirect } from "../redirect"

describe("safeRedirect", () => {
  it("keeps in-app paths", () => {
    expect(safeRedirect("/transacoes?tipo=Income")).toBe("/transacoes?tipo=Income")
  })

  it.each([null, "", "https://evil.example", "//evil.example", "javascript:alert(1)"])("falls back to / for %s", (target) => {
    expect(safeRedirect(target)).toBe("/")
  })
})
