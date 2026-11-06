import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { chooseCompany, getActiveCompanyId, preferredCompany, rememberCompany, setActiveCompanyId } from "../activeCompany"

// Vitest runs in Node: provide a minimal in-memory localStorage.
beforeEach(() => {
  const store = new Map<string, string>()
  vi.stubGlobal("localStorage", {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    clear: () => store.clear(),
  })
})

const a = { id: "a", name: "Empresa A" }
const b = { id: "b", name: "Empresa B" }
const c = { id: "c", name: "Empresa C" }

describe("chooseCompany", () => {
  afterEach(() => {
    setActiveCompanyId(null)
    vi.unstubAllGlobals()
  })

  it("uses the server default when nothing was chosen", () => {
    expect(chooseCompany("u1", [a, b], a)).toBe(a)
  })

  it("prefers the remembered company of the same user", () => {
    rememberCompany("u1", "b")
    expect(chooseCompany("u1", [a, b], a)).toBe(b)
    expect(chooseCompany("u2", [a, b], a)).toBe(a)
  })

  it("keeps the tab's current choice over the remembered one", () => {
    rememberCompany("u1", "b")
    setActiveCompanyId("c")
    expect(chooseCompany("u1", [a, b, c], a)).toBe(c)
  })

  it("ignores choices for companies the user no longer belongs to", () => {
    rememberCompany("u1", "gone")
    setActiveCompanyId("also-gone")
    expect(chooseCompany("u1", [a, b], b)).toBe(b)
  })

  it("returns null for a user without companies", () => {
    expect(chooseCompany("u1", [], null)).toBeNull()
  })

  it("stores the preference per user and keeps the tab choice in memory", () => {
    rememberCompany("u1", "a")
    setActiveCompanyId("b")
    expect(preferredCompany("u1")).toBe("a")
    expect(getActiveCompanyId()).toBe("b")
  })
})
